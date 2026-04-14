using System.Text.Json;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Spaces.Services;

public interface IWorkflowTransitionService
{
    /// <summary>
    /// Checks whether <paramref name="callerRole"/> may move an issue from
    /// <paramref name="fromStatusId"/> to <paramref name="toStatusId"/> in the given space.
    /// Returns false without throwing.
    /// </summary>
    Task<bool> IsAllowedAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        OrgRole callerRole, CancellationToken ct = default);

    /// <summary>
    /// Same check but throws <see cref="InvalidOperationException"/> on failure.
    /// </summary>
    Task ValidateAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        OrgRole callerRole, CancellationToken ct = default);
}

public class WorkflowTransitionService(AppDbContext db) : IWorkflowTransitionService
{
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> IsAllowedAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        OrgRole callerRole, CancellationToken ct = default)
    {
        // OrgManager has full status override — no transition check needed.
        if (callerRole == OrgRole.OrgManager)
            return true;

        // Every workflow is space-scoped (no global/null-SpaceId workflow).
        var transition = await db.WorkflowTransitions.FirstOrDefaultAsync(
            t => t.SpaceId == space.Id &&
                 t.FromStatusId == fromStatusId &&
                 t.ToStatusId == toStatusId, ct);

        if (transition is null) return false;

        // If AllowedRoles is null/empty, any role is permitted.
        if (string.IsNullOrWhiteSpace(transition.AllowedRolesJson)) return true;

        var allowedRoles = JsonSerializer.Deserialize<List<string>>(
            transition.AllowedRolesJson, _json) ?? [];

        return allowedRoles.Any(r =>
            string.Equals(r, callerRole.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task ValidateAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        OrgRole callerRole, CancellationToken ct = default)
    {
        if (!await IsAllowedAsync(space, fromStatusId, toStatusId, callerRole, ct))
            throw new InvalidOperationException(
                $"Transition not allowed for role '{callerRole}'.");
    }
}
