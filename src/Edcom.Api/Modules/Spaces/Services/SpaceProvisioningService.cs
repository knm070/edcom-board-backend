using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Spaces.Dto;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Spaces.Services;

public interface ISpaceProvisioningService
{
    /// <summary>
    /// Stages dual-space creation for the org in the DbContext.
    /// Does NOT call SaveChanges — caller must do so after.
    /// </summary>
    Task StageAsync(Organization org, CancellationToken ct = default);

    /// <summary>
    /// Sets the board template on a PendingTemplateSelection internal space
    /// and activates it. Saves to database.
    /// </summary>
    Task<SpaceDto> SetInternalTemplateAsync(Guid orgId, Guid spaceId, BoardTemplate template, CancellationToken ct = default);
}

public class SpaceProvisioningService(AppDbContext db) : ISpaceProvisioningService
{
    // JSON constants reused for transition AllowedRoles seeding
    private const string BothRoles      = """["OrgTaskManager","Employer"]""";
    private const string ManagerOnly    = """["OrgTaskManager"]""";
    private const string EmployerAndMgr = """["OrgTaskManager","Employer"]""";

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task StageAsync(Organization org, CancellationToken ct = default)
    {
        var internalPrefix = await UniquePrefixAsync(LettersFromSlug(org.Slug) + "I", ct);
        var externalPrefix = await UniquePrefixAsync(LettersFromSlug(org.Slug) + "X", ct);

        // ── Internal space (awaiting template selection) ──────────────────────
        var internalSpace = new Space
        {
            OrgId          = org.Id,
            Name           = $"{org.Name} Internal",
            Type           = SpaceType.Internal,
            Status         = SpaceStatus.PendingTemplateSelection,
            IssueKeyPrefix = internalPrefix
        };

        // ── External space (fixed workflow, immediately active) ────────────────
        var externalSpace = new Space
        {
            OrgId          = org.Id,
            Name           = $"{org.Name} External",
            Type           = SpaceType.External,
            Status         = SpaceStatus.Active,
            IssueKeyPrefix = externalPrefix
        };

        db.Spaces.AddRange(internalSpace, externalSpace);

        // ── Seed internal workflow: ToDo → InProgress → InReview → Done ───────
        SeedInternalWorkflow(internalSpace.Id);

        // ── Seed system-level external workflow (once, shared across all orgs) ─
        await SeedSystemExternalWorkflowIfNeededAsync(ct);
    }

    public async Task<SpaceDto> SetInternalTemplateAsync(
        Guid orgId, Guid spaceId, BoardTemplate template, CancellationToken ct = default)
    {
        var space = await db.Spaces
            .Include(s => s.WorkflowStatuses.OrderBy(w => w.Position))
            .FirstOrDefaultAsync(s => s.Id == spaceId && s.OrgId == orgId, ct)
            ?? throw new KeyNotFoundException("Internal space not found.");

        if (space.Type != SpaceType.Internal)
            throw new InvalidOperationException("Template can only be set on internal spaces.");

        if (space.Status != SpaceStatus.PendingTemplateSelection)
            throw new InvalidOperationException("Template has already been set for this space.");

        space.BoardTemplate = template;
        space.Status        = SpaceStatus.Active;
        space.UpdatedAt     = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToDto(space);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void SeedInternalWorkflow(Guid spaceId)
    {
        var toDo   = WS(spaceId, "To Do",       "#3B82F6", 0, isInitial: true);
        var inProg = WS(spaceId, "In Progress",  "#F59E0B", 1);
        var inRev  = WS(spaceId, "In Review",    "#8B5CF6", 2);
        var done   = WS(spaceId, "Done",         "#10B981", 3, isTerminal: true);

        db.WorkflowStatuses.AddRange(toDo, inProg, inRev, done);

        db.WorkflowTransitions.AddRange(
            WT(spaceId, toDo.Id,   inProg.Id, BothRoles),
            WT(spaceId, inProg.Id, inRev.Id,  BothRoles),
            WT(spaceId, inRev.Id,  done.Id,   BothRoles)
        );
    }

    private async Task SeedSystemExternalWorkflowIfNeededAsync(CancellationToken ct)
    {
        if (await db.WorkflowStatuses.AnyAsync(w => w.SpaceId == null, ct))
            return; // already seeded

        // Fixed external statuses (SpaceId = null = system-owned)
        var backlog = WS(null, "Backlog",     "#6B7280", 0, isInitial: true);
        var toDo    = WS(null, "To Do",       "#3B82F6", 1);
        var inProg  = WS(null, "In Progress", "#F59E0B", 2);
        var inRev   = WS(null, "In Review",   "#8B5CF6", 3);
        var done    = WS(null, "Done",        "#10B981", 4, isTerminal: true);

        db.WorkflowStatuses.AddRange(backlog, toDo, inProg, inRev, done);

        // Transition rules per spec:
        //   Backlog  → ToDo       : OrgTaskManager only (manual activation)
        //   ToDo     → InProgress : Employer + OrgTaskManager
        //   InProgress → InReview : Employer + OrgTaskManager
        //   InReview → Done       : OrgTaskManager only
        db.WorkflowTransitions.AddRange(
            WT(null, backlog.Id, toDo.Id,   ManagerOnly),
            WT(null, toDo.Id,   inProg.Id,  EmployerAndMgr),
            WT(null, inProg.Id, inRev.Id,   EmployerAndMgr),
            WT(null, inRev.Id,  done.Id,    ManagerOnly)
        );
    }

    // ── Entity factories ──────────────────────────────────────────────────────

    private static WorkflowStatus WS(
        Guid? spaceId, string name, string color, int position,
        bool isInitial = false, bool isTerminal = false) => new()
    {
        SpaceId    = spaceId,
        Name       = name,
        Color      = color,
        Position   = position,
        IsInitial  = isInitial,
        IsTerminal = isTerminal
    };

    private static WorkflowTransition WT(
        Guid? spaceId, Guid fromId, Guid toId, string? allowedRolesJson) => new()
    {
        SpaceId          = spaceId,
        FromStatusId     = fromId,
        ToStatusId       = toId,
        AllowedRolesJson = allowedRolesJson
    };

    // ── Prefix helpers ────────────────────────────────────────────────────────

    private static string LettersFromSlug(string slug)
    {
        var letters = new string(slug.Where(char.IsLetter).ToArray()).ToUpper();
        var base3   = letters[..Math.Min(3, letters.Length)];
        return base3.Length == 0 ? "ORG" : base3;
    }

    private async Task<string> UniquePrefixAsync(string candidate, CancellationToken ct)
    {
        var prefix  = candidate;
        var counter = 2;
        while (await db.Spaces.AnyAsync(s => s.IssueKeyPrefix == prefix, ct))
            prefix = candidate + counter++;
        return prefix;
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    internal static SpaceDto ToDto(Space s) => new(
        s.Id, s.OrgId, s.Name, s.Type.ToString(), s.BoardTemplate?.ToString(),
        s.Status.ToString(), s.IssueKeyPrefix,
        s.Issues?.Count ?? 0,
        s.WorkflowStatuses
            .OrderBy(w => w.Position)
            .Select(w => new WorkflowStatusDto(w.Id, w.Name, w.Color, w.Position, w.IsInitial, w.IsTerminal))
            .ToList(),
        s.CreatedAt);
}
