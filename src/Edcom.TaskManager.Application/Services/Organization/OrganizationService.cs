using Edcom.TaskManager.Application.Services.Organization.Contracts;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrganizationEntity = Edcom.TaskManager.Domain.Entities.Organization;

namespace Edcom.TaskManager.Application.Services.Organization;

public class OrganizationService(AppDbContext dbContext) : IOrganizationService
{
    public async Task<Result<List<OrganizationResponse>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await dbContext.Organizations
            .AsNoTracking()
            .Where(o => !o.IsDeleted)
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
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(long id, CancellationToken cancellationToken)
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
            })
            .SingleOrDefaultAsync(cancellationToken);

        return org is null ? OrganizationErrors.NotFound : org;
    }

    public async Task<Result> AddAsync(
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
            Description     = request.Description,
            LogoUrl         = request.LogoUrl,
            IsActive        = true,
            CreatedByUserId = createdByUserId,
        };

        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
