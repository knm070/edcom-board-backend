namespace Edcom.TaskManager.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // TODO: Add DbSets here as you create entities:
    // public DbSet<Example> Examples { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Example composite key:
        // modelBuilder.Entity<ExampleJoin>().HasKey(x => new { x.LeftId, x.RightId });
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
