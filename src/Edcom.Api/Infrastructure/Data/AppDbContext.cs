using Edcom.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrgMember> OrgMembers => Set<OrgMember>();

    // Spaces
    public DbSet<Space> Spaces => Set<Space>();

    // Workflow
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

    // Board
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueAssignee> IssueAssignees => Set<IssueAssignee>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Epic> Epics => Set<Epic>();

    // Cross-org
    public DbSet<CrossOrgTicket> CrossOrgTickets => Set<CrossOrgTicket>();

    // Collaboration
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── User ────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.GoogleId).IsUnique().HasFilter("\"GoogleId\" IS NOT NULL");
            e.HasIndex(x => x.MicrosoftId).IsUnique().HasFilter("\"MicrosoftId\" IS NOT NULL");
        });

        // ── RefreshToken ────────────────────────────────────────
        mb.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Organization ────────────────────────────────────────
        mb.Entity<Organization>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        // ── OrgMember ───────────────────────────────────────────
        mb.Entity<OrgMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrgId, x.UserId }).IsUnique();
            e.Property(x => x.Role).HasConversion<string>();
            e.HasOne(x => x.Organization).WithMany(o => o.Members)
                .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.OrgMemberships)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.InvitedBy).WithMany()
                .HasForeignKey(x => x.InvitedById).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // ── Space ───────────────────────────────────────────────
        mb.Entity<Space>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrgId, x.Type }).IsUnique();
            e.HasIndex(x => x.IssueKeyPrefix).IsUnique();
            e.Property(x => x.Type).HasConversion<string>();
            e.Property(x => x.BoardTemplate).HasConversion<string>();
            e.HasOne(x => x.Organization).WithMany(o => o.Spaces)
                .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── WorkflowStatus ──────────────────────────────────────
        mb.Entity<WorkflowStatus>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Space).WithMany(s => s.WorkflowStatuses)
                .HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
        });

        // ── WorkflowTransition ──────────────────────────────────
        mb.Entity<WorkflowTransition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.FromStatus).WithMany(s => s.TransitionsFrom)
                .HasForeignKey(x => x.FromStatusId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ToStatus).WithMany(s => s.TransitionsTo)
                .HasForeignKey(x => x.ToStatusId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── Sprint ──────────────────────────────────────────────
        mb.Entity<Sprint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Space).WithMany(s => s.Sprints)
                .HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Epic ────────────────────────────────────────────────
        mb.Entity<Epic>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Space).WithMany(s => s.Epics)
                .HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Organization).WithMany()
                .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany()
                .HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Issue ───────────────────────────────────────────────
        mb.Entity<Issue>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SpaceId, x.KeyNumber }).IsUnique();
            e.Property(x => x.Type).HasConversion<string>();
            e.Property(x => x.Priority).HasConversion<string>();
            e.HasOne(x => x.Space).WithMany(s => s.Issues)
                .HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
            // Explicitly map OrgId (non-standard name) so EF Core does NOT create a shadow FK
            e.HasOne(x => x.Organization).WithMany()
                .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Status).WithMany(ws => ws.Issues)
                .HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sprint).WithMany(sp => sp.Issues)
                .HasForeignKey(x => x.SprintId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasOne(x => x.Epic).WithMany(ep => ep.Issues)
                .HasForeignKey(x => x.EpicId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasOne(x => x.Reporter).WithMany(u => u.ReportedIssues)
                .HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── IssueAssignee ───────────────────────────────────────
        mb.Entity<IssueAssignee>(e =>
        {
            e.HasKey(x => new { x.IssueId, x.UserId });
            e.HasOne(x => x.Issue).WithMany(i => i.Assignees)
                .HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── CrossOrgTicket ──────────────────────────────────────
        mb.Entity<CrossOrgTicket>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalIssueId).IsUnique();
            e.HasOne(x => x.ExternalIssue).WithOne(i => i.CrossOrgTicketAsExternal)
                .HasForeignKey<CrossOrgTicket>(x => x.ExternalIssueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.InternalMirrorIssue).WithOne(i => i.CrossOrgTicketAsMirror)
                .HasForeignKey<CrossOrgTicket>(x => x.InternalMirrorIssueId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
            e.HasOne(x => x.CreatorOrg).WithMany()
                .HasForeignKey(x => x.CreatorOrgId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReceiverOrg).WithMany()
                .HasForeignKey(x => x.ReceiverOrgId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Comment ─────────────────────────────────────────────
        mb.Entity<Comment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Issue).WithMany(i => i.Comments)
                .HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(c => c.Replies)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
            e.HasOne(x => x.Author).WithMany()
                .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AuthorOrg).WithMany()
                .HasForeignKey(x => x.AuthorOrgId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ActivityLog ─────────────────────────────────────────
        mb.Entity<ActivityLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Issue).WithMany(i => i.ActivityLogs)
                .HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Actor).WithMany()
                .HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Notification ────────────────────────────────────────
        mb.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Organization).WithMany()
                .HasForeignKey(x => x.OrgId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
