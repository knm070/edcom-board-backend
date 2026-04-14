using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Issues.Services;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Edcom.Api.Tests.Issues;

/// <summary>
/// Creates an isolated SQLite in-memory database and a seeded TicketService
/// for each test class that inherits from it. Each test class gets its own DB file.
/// </summary>
public abstract class TicketServiceTestBase : IDisposable
{
    // ── Common IDs ────────────────────────────────────────────────────────────
    protected static readonly Guid OrgId       = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpaceId     = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    protected static readonly Guid StatusTodoId  = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid StatusDoneId  = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    protected static readonly Guid ManagerUserId  = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    protected static readonly Guid EmployerUserId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    protected static readonly Guid OtherUserId    = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly SqliteConnection _connection;
    protected readonly AppDbContext Db;
    protected readonly TicketService Svc;

    protected TicketServiceTestBase()
    {
        // Keep a single open connection so the in-memory DB persists for the lifetime of the test
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
        SeedCoreData();

        var workflow = new WorkflowTransitionService(Db);
        Svc = new TicketService(Db, workflow, NullLogger<TicketService>.Instance);
    }

    // ── Seed ──────────────────────────────────────────────────────────────────

    private void SeedCoreData()
    {
        // Users
        Db.Users.AddRange(
            new User { Id = ManagerUserId,  Email = "manager@test.com",  FullName = "Test Manager",  PasswordHash = "x", IsActive = true },
            new User { Id = EmployerUserId, Email = "employer@test.com", FullName = "Test Employer", PasswordHash = "x", IsActive = true },
            new User { Id = OtherUserId,    Email = "other@test.com",    FullName = "Other User",    PasswordHash = "x", IsActive = true }
        );

        // Org — CreatedById must reference an existing user; use ManagerUserId
        var org = new Organization
        {
            Id          = OrgId,
            Name        = "Test Org",
            Slug        = "test-org",
            CreatedById = ManagerUserId,
        };
        Db.Organizations.Add(org);

        // Org memberships
        Db.OrgMembers.AddRange(
            new OrgMember { OrgId = OrgId, UserId = ManagerUserId,  Role = OrgRole.OrgManager },
            new OrgMember { OrgId = OrgId, UserId = EmployerUserId, Role = OrgRole.Employer },
            new OrgMember { OrgId = OrgId, UserId = OtherUserId,    Role = OrgRole.Employer }
        );

        // Space
        var space = new Space
        {
            Id             = SpaceId,
            OrgId          = OrgId,
            Name           = "Test Space",
            IssueKeyPrefix = "TST",
            BoardType      = BoardType.Kanban,
        };
        Db.Spaces.Add(space);

        // Workflow statuses
        Db.WorkflowStatuses.AddRange(
            new WorkflowStatus { Id = StatusTodoId, SpaceId = SpaceId, Name = "To Do",  Color = "#6B7280", Position = 1 },
            new WorkflowStatus { Id = StatusDoneId, SpaceId = SpaceId, Name = "Done",   Color = "#22C55E", Position = 2, IsDoneStatus = true }
        );

        // Allow transition from To Do → Done for all roles
        Db.WorkflowTransitions.Add(new WorkflowTransition
        {
            Id           = Guid.NewGuid(),
            SpaceId      = SpaceId,
            FromStatusId = StatusTodoId,
            ToStatusId   = StatusDoneId,
            AllowedRolesJson = null,  // null = any role allowed
        });

        Db.SaveChanges();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a ticket owned by the given userId in the shared space.</summary>
    protected Issue SeedIssue(Guid reporterId, Guid? assigneeId = null)
    {
        var issue = new Issue
        {
            SpaceId    = SpaceId,
            OrgId      = OrgId,
            KeyNumber  = 1,
            Title      = "Seeded issue",
            Type       = IssueType.Task,
            Priority   = IssuePriority.Medium,
            StatusId   = StatusTodoId,
            ReporterId = reporterId,
        };
        Db.Issues.Add(issue);

        if (assigneeId.HasValue)
        {
            Db.Set<IssueAssignee>().Add(new IssueAssignee
            {
                IssueId = issue.Id,
                UserId  = assigneeId.Value,
            });
        }

        Db.SaveChanges();
        return issue;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
