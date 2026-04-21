using Edcom.TaskManager.Application.Services.WorkflowStatus.Contracts;
using Edcom.TaskManager.Domain.Enums;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WfStatusEntity = Edcom.TaskManager.Domain.Entities.WorkflowStatus;

namespace Edcom.TaskManager.Application.Services.WorkflowStatus;

public class WorkflowStatusService(AppDbContext dbContext) : IWorkflowStatusService
{
    public async Task<Result<List<WorkflowStatusResponse>>> GetAllBySpaceAsync(long spaceId, CancellationToken ct)
    {
        var items = await dbContext.WorkflowStatuses
            .AsNoTracking()
            .Where(w => w.SpaceId == spaceId && !w.IsDeleted && w.BaseType != WorkflowStatusBaseType.Backlog)
            .OrderBy(w => w.Position)
            .Select(w => new WorkflowStatusResponse
            {
                Id        = w.Id,
                SpaceId   = w.SpaceId,
                Name      = w.Name,
                Color     = w.Color,
                Position  = w.Position,
                BaseType  = w.BaseType,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<Result<WorkflowStatusResponse>> GetByIdAsync(long id, CancellationToken ct)
    {
        var ws = await dbContext.WorkflowStatuses
            .AsNoTracking()
            .Where(w => w.Id == id && !w.IsDeleted)
            .Select(w => new WorkflowStatusResponse
            {
                Id        = w.Id,
                SpaceId   = w.SpaceId,
                Name      = w.Name,
                Color     = w.Color,
                Position  = w.Position,
                BaseType  = w.BaseType,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
            })
            .SingleOrDefaultAsync(ct);

        return ws is null ? WorkflowStatusErrors.NotFound : ws;
    }

    public async Task<Result> AddAsync(long spaceId, CreateWorkflowStatusRequest request, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return WorkflowStatusErrors.NotFound;

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return WorkflowStatusErrors.Forbidden;

        dbContext.WorkflowStatuses.Add(new WfStatusEntity
        {
            SpaceId  = spaceId,
            Name     = request.Name,
            Color    = request.Color,
            Position = request.Position,
            BaseType = request.BaseType,
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(long id, UpdateWorkflowStatusRequest request, long callerUserId, CancellationToken ct)
    {
        var ws = await dbContext.WorkflowStatuses
            .SingleOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (ws is null) return WorkflowStatusErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == ws.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return WorkflowStatusErrors.Forbidden;

        ws.Name     = request.Name;
        ws.Color    = request.Color;
        ws.Position = request.Position;
        ws.BaseType = request.BaseType;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long callerUserId, CancellationToken ct)
    {
        var ws = await dbContext.WorkflowStatuses
            .SingleOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);
        if (ws is null) return WorkflowStatusErrors.NotFound;

        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == ws.SpaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);

        var isManager = space is not null && await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return WorkflowStatusErrors.Forbidden;

        ws.IsDeleted = true;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReorderAsync(long spaceId, List<long> orderedIds, long callerUserId, CancellationToken ct)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.Id == spaceId && !s.IsDeleted)
            .Select(s => new { s.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (space is null) return WorkflowStatusErrors.NotFound;

        var isManager = await dbContext.OrgMembers
            .AsNoTracking()
            .AnyAsync(m => m.OrganizationId == space.OrganizationId && m.UserId == callerUserId
                        && m.Role == OrgRole.OrgManager && !m.IsDeleted, ct);
        if (!isManager) return WorkflowStatusErrors.Forbidden;

        var statuses = await dbContext.WorkflowStatuses
            .Where(w => w.SpaceId == spaceId && !w.IsDeleted)
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var item = statuses.SingleOrDefault(s => s.Id == orderedIds[i]);
            if (item is not null) item.Position = i + 1;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
