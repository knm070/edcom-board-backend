using System.Security.Claims;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Authorization.Extensions;
using Edcom.Api.Modules.Issues.Dto;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Issues.Services;

/// <summary>
/// Service for ticket operations with role-scoped business logic enforcement.
/// </summary>
public class TicketService : ITicketService
{
    private readonly AppDbContext _db;
    private readonly IWorkflowTransitionService _workflowService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        AppDbContext db,
        IWorkflowTransitionService workflowService,
        ILogger<TicketService> logger)
    {
        _db = db;
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new ticket with role-based restrictions.
    /// </summary>
    public async Task<Result<IssueDetailDto>> CreateAsync(
        Guid spaceId,
        CreateIssueRequest req,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<IssueDetailDto>.NotFound("Space");

        // ── Business Rule: SystemAdmin cannot create tickets ──────────────────

        if (user.IsSystemAdmin())
            return Result<IssueDetailDto>.Unauthorized("System Admins cannot create tickets");

        // ── Authorization: Determine role in this org ────────────────────────

        if (user.GetOrgRole(space.OrgId) is not OrgRole userRole)
            return Result<IssueDetailDto>.Unauthorized("You are not a member of this organization");

        // ── Business Rule: Validate assignee list per role ──────────────────

        var assigneeIds = req.AssigneeIds ?? [];

        if (userRole == OrgRole.Employer)
        {
            // Employer can ONLY create tickets assigned to themselves
            if (assigneeIds.Count > 0 && assigneeIds.Any(id => id != userId))
                return Result<IssueDetailDto>.Failure(
                    "EMPLOYER_CANNOT_ASSIGN_OTHERS",
                    "You can only create tickets assigned to yourself");

            // Auto-assign to self
            assigneeIds = [userId];
        }

        // OrgManager and SpaceManager can assign to any org member

        // ── Business Rule: Validate Type per role ───────────────────────────

        if (!Enum.TryParse<IssueType>(req.Type, out var type))
            return Result<IssueDetailDto>.Failure(
                "INVALID_TYPE",
                $"Invalid issue type: {req.Type}");

        // ── Validate other fields ────────────────────────────────────────────

        if (!Enum.TryParse<IssuePriority>(req.Priority, out var priority))
            return Result<IssueDetailDto>.Failure(
                "INVALID_PRIORITY",
                $"Invalid priority: {req.Priority}");

        var status = await _db.WorkflowStatuses.FindAsync([req.StatusId], ct);
        if (status is null || status.SpaceId != spaceId)
            return Result<IssueDetailDto>.NotFound("Status");

        // ── Create issue ────────────────────────────────────────────────────

        var issue = new Issue
        {
            SpaceId      = spaceId,
            OrgId        = space.OrgId,
            Title        = req.Title,
            Description  = req.Description,
            Type         = type,
            Priority     = priority,
            StatusId     = req.StatusId,
            SprintId     = req.SprintId,
            EpicId       = req.EpicId,
            ReporterId   = userId,
            StoryPoints  = req.StoryPoints,
            DueDate      = req.DueDate,
            CreatedAt    = DateTime.UtcNow,
        };

        // Atomic increment of issue counter
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Spaces\" SET \"IssueCounter\" = \"IssueCounter\" + 1, \"UpdatedAt\" = {DateTime.UtcNow} WHERE \"Id\" = {spaceId}",
            ct);
        await _db.Entry(space).ReloadAsync(ct);
        issue.KeyNumber = space.IssueCounter;

        _db.Issues.Add(issue);

        // Add assignees
        foreach (var assigneeId in assigneeIds)
        {
            _db.Set<IssueAssignee>().Add(new IssueAssignee
            {
                IssueId      = issue.Id,
                UserId       = assigneeId,
                AssignedById = userId,
                AssignedAt   = DateTime.UtcNow,
            });
        }

        // Log activity
        _db.Set<ActivityLog>().Add(new ActivityLog
        {
            IssueId    = issue.Id,
            ActorId    = userId,
            Action     = "CreatedTicket",
            CreatedAt  = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Ticket {Key} created by {UserId}", $"{space.IssueKeyPrefix}-{issue.KeyNumber}", userId);

        var full = await LoadFullIssue(issue.Id, ct);
        return Result<IssueDetailDto>.Success(ToDetailDto(full, space.IssueKeyPrefix));
    }

    /// <summary>
    /// Update a ticket with field-level permission enforcement.
    /// </summary>
    public async Task<Result<IssueDetailDto>> UpdateAsync(
        Guid spaceId,
        Guid issueId,
        UpdateIssueRequest req,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<IssueDetailDto>.NotFound("Space");

        var issue = await _db.Issues.FindAsync([issueId], ct);
        if (issue is null || issue.SpaceId != spaceId)
            return Result<IssueDetailDto>.NotFound("Ticket");

        if (user.GetOrgRole(space.OrgId) is not OrgRole userRole)
            return Result<IssueDetailDto>.Unauthorized("You are not a member of this organization");

        var isOwner = issue.ReporterId == userId;

        // ── Field-level permission checks ────────────────────────────────────

        // Title
        if (req.Title != null)
        {
            if (!CanEditField("Title", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot edit the Title field on this ticket");
            issue.Title = req.Title;
        }

        // Description
        if (req.Description != null)
        {
            if (!CanEditField("Description", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot edit the Description field on this ticket");
            issue.Description = req.Description;
        }

        // Priority
        if (req.Priority != null)
        {
            if (!CanEditField("Priority", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot edit the Priority field on this ticket");
            if (!Enum.TryParse<IssuePriority>(req.Priority, out var priority))
                return Result<IssueDetailDto>.Failure("INVALID_PRIORITY", "Invalid priority");
            issue.Priority = priority;
        }

        // Type
        if (req.Type != null)
        {
            if (userRole == OrgRole.Employer && !new[] { "Task", "Bug", "Story" }.Contains(req.Type))
                return Result<IssueDetailDto>.Failure(
                    "INVALID_TYPE_FOR_EMPLOYER",
                    "Employers can only set Type to Task, Bug, or Story");
            if (!Enum.TryParse<IssueType>(req.Type, out var type))
                return Result<IssueDetailDto>.Failure("INVALID_TYPE", "Invalid type");
            issue.Type = type;
        }

        // Status (with workflow validation)
        if (req.StatusId.HasValue)
        {
            var newStatus = await _db.WorkflowStatuses.FindAsync([req.StatusId.Value], ct);
            if (newStatus is null || newStatus.SpaceId != spaceId)
                return Result<IssueDetailDto>.NotFound("Status");

            // OrgManager can bypass workflow
            if (userRole != OrgRole.OrgManager)
            {
                var allowedTransition = await _workflowService.IsAllowedAsync(
                    space, issue.StatusId, req.StatusId.Value, userRole, ct);
                if (!allowedTransition)
                    return Result<IssueDetailDto>.Conflict(
                        $"Transition from current status to {newStatus.Name} is not allowed for your role");
            }

            issue.StatusId = req.StatusId.Value;
        }

        // Other fields (limited for Employer)
        if (req.SprintId.HasValue)
        {
            if (!CanEditField("SprintId", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot change the Sprint assignment");
            issue.SprintId = req.SprintId;
        }

        if (req.EpicId.HasValue)
        {
            if (!CanEditField("EpicId", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot change the Epic assignment");
            issue.EpicId = req.EpicId;
        }

        if (req.StoryPoints.HasValue)
        {
            if (!CanEditField("StoryPoints", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot change Story Points");
            issue.StoryPoints = req.StoryPoints;
        }

        if (req.DueDate.HasValue)
        {
            if (!CanEditField("DueDate", userRole, isOwner))
                return Result<IssueDetailDto>.Unauthorized("You cannot change the Due Date");
            issue.DueDate = req.DueDate;
        }

        issue.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Ticket {Id} updated by {UserId}", issueId, userId);

        var full = await LoadFullIssue(issueId, ct);
        return Result<IssueDetailDto>.Success(ToDetailDto(full, space.IssueKeyPrefix));
    }

    public async Task<Result<IssueDetailDto>> GetByIdAsync(
        Guid spaceId,
        Guid issueId,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<IssueDetailDto>.NotFound("Space");

        if (!user.IsMemberOfOrg(space.OrgId))
            return Result<IssueDetailDto>.Unauthorized("You are not a member of this organization");

        var issue = await LoadFullIssue(issueId, ct);
        if (issue?.SpaceId != spaceId)
            return Result<IssueDetailDto>.NotFound("Ticket");

        return Result<IssueDetailDto>.Success(ToDetailDto(issue, space.IssueKeyPrefix));
    }

    public async Task<Result<List<IssueListDto>>> ListAsync(
        Guid spaceId,
        IssueFilterDto? filter,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<List<IssueListDto>>.NotFound("Space");

        if (!user.IsMemberOfOrg(space.OrgId))
            return Result<List<IssueListDto>>.Unauthorized("You are not a member of this organization");

        var query = _db.Issues
            .AsNoTracking()
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Where(i => i.SpaceId == spaceId && i.DeletedAt == null);

        filter ??= new IssueFilterDto();

        if (filter.Backlog)
            query = query.Where(i => i.SprintId == null);
        else if (filter.SprintId.HasValue)
            query = query.Where(i => i.SprintId == filter.SprintId);

        if (filter.EpicId.HasValue)
            query = query.Where(i => i.EpicId == filter.EpicId);
        if (filter.Status != null)
            query = query.Where(i => i.Status.Name == filter.Status);
        if (filter.Priority != null && Enum.TryParse<IssuePriority>(filter.Priority, out var p))
            query = query.Where(i => i.Priority == p);
        if (filter.AssigneeId.HasValue)
            query = query.Where(i => i.Assignees.Any(a => a.UserId == filter.AssigneeId));

        var issues = await query
            .OrderBy(i => i.BacklogOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(ct);

        var dtos = issues.Select(i => ToListDto(i, space.IssueKeyPrefix)).ToList();
        return Result<List<IssueListDto>>.Success(dtos);
    }

    public async Task<Result<Unit>> DeleteAsync(
        Guid spaceId,
        Guid issueId,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<Unit>.NotFound("Space");

        var userRole = user.GetOrgRole(space.OrgId);
        if (userRole is null)
            return Result<Unit>.Unauthorized("You are not a member of this organization");

        var issue = await _db.Issues.FindAsync([issueId], ct);
        if (issue is null || issue.SpaceId != spaceId)
            return Result<Unit>.NotFound("Ticket");

        issue.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<Unit>.Success(new Unit());
    }

    public async Task<Result<Unit>> LogTimeAsync(
        Guid spaceId,
        Guid issueId,
        LogTimeRequest req,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var space = await _db.Spaces.FindAsync([spaceId], ct);
        if (space is null)
            return Result<Unit>.NotFound("Space");

        if (user.GetOrgRole(space.OrgId) is not OrgRole userRole)
            return Result<Unit>.Unauthorized("You are not a member of this organization");

        var issue = await _db.Issues.FindAsync([issueId], ct);
        if (issue is null || issue.SpaceId != spaceId)
            return Result<Unit>.NotFound("Ticket");

        // Employer can only log time for themselves
        if (userRole == OrgRole.Employer && issue.ReporterId != userId)
            return Result<Unit>.Unauthorized("You can only log time on your own tickets");

        var worklog = new Worklog
        {
            IssueId      = issueId,
            UserId       = userId,
            Hours        = req.Hours,
            Date         = req.Date,
            Description  = req.Description,
            CreatedAt    = DateTime.UtcNow,
        };

        _db.Set<Worklog>().Add(worklog);

        _db.Set<ActivityLog>().Add(new ActivityLog
        {
            IssueId    = issueId,
            ActorId    = userId,
            Action     = "LoggedTime",
            Metadata   = $"{req.Hours} hours",
            CreatedAt  = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return Result<Unit>.Success(new Unit());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private bool CanEditField(string fieldName, OrgRole role, bool isOwner) =>
        role switch
        {
            OrgRole.OrgManager   => true,    // Managers can edit any field
            OrgRole.SpaceManager => true,    // SpaceManagers can edit any field
            OrgRole.Employer     => isOwner, // Employers can edit only own tickets
            _ => false,
        };

    private async Task<Issue> LoadFullIssue(Guid issueId, CancellationToken ct) =>
        await _db.Issues
            .Include(i => i.Status)
            .Include(i => i.Assignees).ThenInclude(a => a.User)
            .Include(i => i.Reporter)
            .Include(i => i.Comments.Where(c => c.DeletedAt == null)).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
        ?? throw new KeyNotFoundException("Issue not found");

    private static IssueListDto ToListDto(Issue i, string prefix) => new(
        i.Id, $"{prefix}-{i.KeyNumber}", i.Title, i.Type.ToString(), i.Priority.ToString(),
        i.Status.Name, i.Status.Color, i.StatusId,
        i.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.FullName, a.User.AvatarUrl)).ToList(),
        i.SprintId, i.EpicId, i.StoryPoints, i.DueDate, i.CreatedAt);

    private static IssueDetailDto ToDetailDto(Issue i, string prefix) => new(
        i.Id, $"{prefix}-{i.KeyNumber}", i.Title, i.Description, i.Type.ToString(),
        i.Priority.ToString(), i.Status.Name, i.Status.Color, i.StatusId,
        i.Assignees.Select(a => new AssigneeDto(a.UserId, a.User.FullName, a.User.AvatarUrl)).ToList(),
        new AssigneeDto(i.ReporterId, i.Reporter.FullName, i.Reporter.AvatarUrl),
        i.SprintId, i.EpicId, i.StoryPoints, i.DueDate,
        i.Comments.OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.AuthorId, c.Author.FullName,
                c.Author.AvatarUrl, c.Body, c.ParentId, c.CreatedAt)).ToList(),
        i.CreatedAt, i.UpdatedAt);
}
