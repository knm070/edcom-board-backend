namespace Edcom.TaskManager.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrgMember> OrgMembers { get; set; }
    public DbSet<OrgInvite> OrgInvites { get; set; }
    public DbSet<Space> Spaces { get; set; }
    public DbSet<WorkflowStatus> WorkflowStatuses { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TicketTag> TicketTags { get; set; }
    public DbSet<Sprint> Sprints { get; set; }
    public DbSet<Epic> Epics { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // ── RefreshToken ────────────────────────────────────────────────────
        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Organization ────────────────────────────────────────────────────
        modelBuilder.Entity<Organization>()
            .HasIndex(o => o.Slug).IsUnique();

        modelBuilder.Entity<Organization>()
            .HasOne(o => o.CreatedByUser)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── OrgMember ───────────────────────────────────────────────────────
        modelBuilder.Entity<OrgMember>()
            .HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();

        modelBuilder.Entity<OrgMember>()
            .HasOne(m => m.Organization)
            .WithMany(o => o.Members)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrgMember>()
            .HasOne(m => m.User)
            .WithMany(u => u.OrgMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── OrgInvite ───────────────────────────────────────────────────────
        modelBuilder.Entity<OrgInvite>()
            .HasIndex(i => i.Token).IsUnique();

        modelBuilder.Entity<OrgInvite>()
            .HasOne(i => i.Organization)
            .WithMany(o => o.Invites)
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Space ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Space>()
            .HasIndex(s => new { s.OrganizationId, s.Slug }).IsUnique();

        modelBuilder.Entity<Space>()
            .HasOne(s => s.Organization)
            .WithMany(o => o.Spaces)
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Space>()
            .HasOne(s => s.CreatedByUser)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── WorkflowStatus ──────────────────────────────────────────────────
        modelBuilder.Entity<WorkflowStatus>()
            .HasOne(w => w.Space)
            .WithMany(s => s.WorkflowStatuses)
            .HasForeignKey(w => w.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Tag ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Tag>()
            .HasIndex(t => new { t.SpaceId, t.Name }).IsUnique();

        modelBuilder.Entity<Tag>()
            .HasOne(t => t.Space)
            .WithMany(s => s.Tags)
            .HasForeignKey(t => t.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── TicketTag ───────────────────────────────────────────────────────
        modelBuilder.Entity<TicketTag>()
            .HasKey(tt => new { tt.TicketId, tt.TagId });

        modelBuilder.Entity<TicketTag>()
            .HasOne(tt => tt.Ticket)
            .WithMany(t => t.TicketTags)
            .HasForeignKey(tt => tt.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketTag>()
            .HasOne(tt => tt.Tag)
            .WithMany(t => t.TicketTags)
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Sprint ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Sprint>()
            .HasOne(s => s.Space)
            .WithMany(sp => sp.Sprints)
            .HasForeignKey(s => s.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Sprint>()
            .HasOne(s => s.CreatedByUser)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Epic ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Epic>()
            .HasOne(e => e.Space)
            .WithMany(s => s.Epics)
            .HasForeignKey(e => e.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Epic>()
            .HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Ticket ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.SpaceId, t.KeyNumber }).IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasQueryFilter(t => !t.IsDeleted);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Space)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Organization)
            .WithMany()
            .HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Status)
            .WithMany(w => w.Tickets)
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Sprint)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Epic)
            .WithMany(e => e.Tickets)
            .HasForeignKey(t => t.EpicId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Reporter)
            .WithMany(u => u.ReportedTickets)
            .HasForeignKey(t => t.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Assignee)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── TicketComment ────────────────────────────────────────────────────
        modelBuilder.Entity<TicketComment>()
            .HasQueryFilter(c => !c.IsDeleted);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Author)
            .WithMany(u => u.Comments)
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Parent)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── ActivityLog ──────────────────────────────────────────────────────
        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.Ticket)
            .WithMany(t => t.ActivityLogs)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ActivityLog>()
            .HasOne(a => a.Actor)
            .WithMany(u => u.Activities)
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Notification ─────────────────────────────────────────────────────
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Organization)
            .WithMany()
            .HasForeignKey(n => n.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void TrackActionsAt()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.GetType()
                    .GetProperty("CreatedAt")?.SetValue(entry.Entity, DateTime.UtcNow);

            if (entry.State == EntityState.Modified)
                entry.Entity.GetType()
                    .GetProperty("UpdatedAt")?.SetValue(entry.Entity, DateTime.UtcNow);
        }
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        TrackActionsAt();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
