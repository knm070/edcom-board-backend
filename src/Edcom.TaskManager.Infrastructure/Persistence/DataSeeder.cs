using Edcom.TaskManager.Infrastructure.Helpers;

namespace Edcom.TaskManager.Infrastructure.Persistence;

public static class DataSeeder
{
    private const string AdminEmail = "admin@edcom.uz";
    private const string AdminPassword = "User_!@#$%";
    private const string AdminFullName = "System Admin";

    private const string OrgName = "Edcom";
    private const string OrgSlug = "edcom";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        var admin = await SeedAdminAsync(dbContext, passwordHasher, cancellationToken);
        await SeedOrganizationAsync(dbContext, admin.Id, cancellationToken);
    }

    private static async Task<User> SeedAdminAsync(
        AppDbContext dbContext,
        PasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var admin = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == AdminEmail, cancellationToken);

        if (admin is not null)
            return admin;

        admin = new User
        {
            FullName = AdminFullName,
            Email = AdminEmail,
            PasswordHash = passwordHasher.Hash(AdminPassword),
            IsSystemAdmin = true,
            IsActive = true,
        };

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return admin;
    }

    private static async Task SeedOrganizationAsync(
        AppDbContext dbContext,
        long adminId,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.Slug == OrgSlug, cancellationToken);

        if (org is null)
        {
            org = new Organization
            {
                Name = OrgName,
                Slug = OrgSlug,
                IsActive = true,
                CreatedByUserId = adminId,
            };

            dbContext.Organizations.Add(org);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var hasMembership = await dbContext.OrgMembers
            .AnyAsync(m => m.OrganizationId == org.Id && m.UserId == adminId, cancellationToken);

        if (!hasMembership)
        {
            dbContext.OrgMembers.Add(new OrgMember
            {
                OrganizationId = org.Id,
                UserId = adminId,
                Role = OrgRole.OrgManager,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
