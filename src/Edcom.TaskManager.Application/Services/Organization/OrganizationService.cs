using Edcom.TaskManager.Application.Services.Organization.Contracts;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrganizationEntity = Edcom.TaskManager.Domain.Entities.Organization;

namespace Edcom.TaskManager.Application.Services.Organization;

public class OrganizationService(AppDbContext dbContext) : IOrganizationService
{
    private async Task<bool> IsMemberAsync(long orgId, long callerUserId, CancellationToken ct)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, ct);
        if (isAdmin) return true;

        return await dbContext.OrgMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == orgId && m.UserId == callerUserId && !m.IsDeleted, ct);
    }

    public async Task<Result<List<OrganizationResponse>>> GetAllAsync(long callerUserId, CancellationToken cancellationToken)
    {
        var isAdmin = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Id == callerUserId && u.IsSystemAdmin && !u.IsDeleted, cancellationToken);

        var items = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => !o.IsDeleted && (isAdmin || o.Members.Any(m => m.UserId == callerUserId && !m.IsDeleted)))
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationResponse
            {
                Id              = o.Id,
                Name            = o.Name,
                Slug            = o.Slug,
                Description     = o.Description,
                LogoUrl         = o.LogoUrl,
                IsActive        = o.IsActive,
                CreatedByUserId = o.CreatedByUserId,
                CreatedAt       = o.CreatedAt,
                UpdatedAt       = o.UpdatedAt,
                MemberCount     = o.Members.Count(m => !m.IsDeleted),
                SpaceCount      = o.Spaces.Count(s => !s.IsDeleted),
                IssueCount      = o.Spaces.Where(s => !s.IsDeleted).SelectMany(s => s.Tickets).Count(t => !t.IsDeleted),
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(long id, long callerUserId, CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == id && !o.IsDeleted)
            .Select(o => new OrganizationResponse
            {
                Id              = o.Id,
                Name            = o.Name,
                Slug            = o.Slug,
                Description     = o.Description,
                LogoUrl         = o.LogoUrl,
                IsActive        = o.IsActive,
                CreatedByUserId = o.CreatedByUserId,
                CreatedAt       = o.CreatedAt,
                UpdatedAt       = o.UpdatedAt,
                MemberCount     = o.Members.Count(m => !m.IsDeleted),
                SpaceCount      = o.Spaces.Count(s => !s.IsDeleted),
                IssueCount      = o.Spaces.Where(s => !s.IsDeleted).SelectMany(s => s.Tickets).Count(t => !t.IsDeleted),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (org is null) return OrganizationErrors.NotFound;
        if (!await IsMemberAsync(id, callerUserId, cancellationToken)) return OrganizationErrors.Forbidden;
        return org;
    }

    public async Task<Result<OrganizationResponse>> AddAsync(
        CreateOrganizationRequest request,
        long createdByUserId,
        CancellationToken cancellationToken)
    {
        var slugTaken = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(o => o.Slug == request.Slug && !o.IsDeleted, cancellationToken);

        if (slugTaken)
            return OrganizationErrors.SlugAlreadyExists;

        var org = new OrganizationEntity
        {
            Name            = request.Name,
            Slug            = request.Slug,
            LogoUrl         = request.LogoUrl,
            IsActive        = true,
            CreatedByUserId = createdByUserId,
        };

        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new OrganizationResponse
        {
            Id              = org.Id,
            Name            = org.Name,
            Slug            = org.Slug,
            Description     = org.Description,
            LogoUrl         = org.LogoUrl,
            IsActive        = org.IsActive,
            CreatedByUserId = org.CreatedByUserId,
            CreatedAt       = org.CreatedAt,
        };
    }

    public async Task<Result> UpdateAsync(
        long id,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

        if (org is null)
            return OrganizationErrors.NotFound;

        if (org.Slug != request.Slug)
        {
            var slugTaken = await dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(
                    o => o.Slug == request.Slug && o.Id != id && !o.IsDeleted,
                    cancellationToken);

            if (slugTaken)
                return OrganizationErrors.SlugAlreadyExists;
        }

        org.Name        = request.Name;
        org.Slug        = request.Slug;
        org.Description = request.Description;
        org.LogoUrl     = request.LogoUrl;
        org.IsActive    = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.Id == id && !o.IsDeleted, cancellationToken);

        if (org is null)
            return OrganizationErrors.NotFound;

        org.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
