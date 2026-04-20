using Edcom.TaskManager.Application.Services.Tag.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TagEntity = Edcom.TaskManager.Domain.Entities.Tag;

namespace Edcom.TaskManager.Application.Services.Tag;

public class TagService(AppDbContext dbContext) : ITagService
{
    public async Task<Result<List<TagResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct)
    {
        var items = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.SpaceId == spaceId && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse
            {
                Id        = t.Id,
                SpaceId   = t.SpaceId,
                Name      = t.Name,
                Color     = t.Color,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<TagResponse>> GetByIdAsync(long id, CancellationToken ct)
    {
        var tag = await dbContext.Tags
            .AsNoTracking()
            .Where(t => t.Id == id && !t.IsDeleted)
            .Select(t => new TagResponse
            {
                Id        = t.Id,
                SpaceId   = t.SpaceId,
                Name      = t.Name,
                Color     = t.Color,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return tag is null ? TagErrors.NotFound : tag;
    }

    public async Task<Result> AddAsync(long spaceId, CreateTagRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return TagErrors.NotFound;

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return TagErrors.Forbidden;

        dbContext.Tags.Add(new TagEntity
        {
            SpaceId = spaceId,
            Name    = request.Name,
            Color   = request.Color,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateTagRequest request, long callerUserId, CancellationToken ct)
    {
        var tag = await dbContext.Tags
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (tag is null) return TagErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == tag.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return TagErrors.Forbidden;

        tag.Name  = request.Name;
        tag.Color = request.Color;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var tag = await dbContext.Tags
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (tag is null) return TagErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == tag.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return TagErrors.Forbidden;

        tag.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
