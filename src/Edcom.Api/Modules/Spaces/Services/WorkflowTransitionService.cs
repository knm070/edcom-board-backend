using System.Text.Json;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Edcom.Api.Modules.Spaces.Services;

public interface IWorkflowTransitionService
{
    /// <summary>
    /// Checks whether <paramref name="callerRole"/> may move an issue from its
    /// current status to <paramref name="toStatusId"/> in the given space.
    /// Returns false without throwing.
    /// </summary>
    Task<bool> IsAllowedAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        MemberRole callerRole, CancellationToken ct = default);

    /// <summary>
    /// Same check but throws <see cref="InvalidOperationException"/> on failure,
    /// with a human-readable message.
    /// </summary>
    Task ValidateAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        MemberRole callerRole, CancellationToken ct = default);
}

public class WorkflowTransitionService(AppDbContext db) : IWorkflowTransitionService
{
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> IsAllowedAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        MemberRole callerRole, CancellationToken ct = default)
    {
        // OrgTaskManagers bypass all transition rules on internal spaces.
        // (External space rules are always enforced — even for managers.)
        if (space.Type == SpaceType.Internal && callerRole == MemberRole.OrgTaskManager)
            return true;

        // Look up the transition record.
        // Internal spaces use space-scoped transitions; external spaces use system-level (SpaceId = null).
        var transition = space.Type == SpaceType.Internal
            ? await db.WorkflowTransitions.FirstOrDefaultAsync(
                  t => t.SpaceId == space.Id &&
                       t.FromStatusId == fromStatusId &&
                       t.ToStatusId == toStatusId, ct)
            : await db.WorkflowTransitions.FirstOrDefaultAsync(
                  t => t.SpaceId == null &&
                       t.FromStatusId == fromStatusId &&
                       t.ToStatusId == toStatusId, ct);

        if (transition is null) return false;

        // If AllowedRolesJson is null/empty, any role is permitted.
        if (string.IsNullOrWhiteSpace(transition.AllowedRolesJson)) return true;

        var allowedRoles = JsonSerializer.Deserialize<List<string>>(
            transition.AllowedRolesJson, _json) ?? [];

        return allowedRoles.Any(r =>
            string.Equals(r, callerRole.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task ValidateAsync(
        Space space, Guid fromStatusId, Guid toStatusId,
        MemberRole callerRole, CancellationToken ct = default)
    {
        if (!await IsAllowedAsync(space, fromStatusId, toStatusId, callerRole, ct))
        {
            var hint = space.Type == SpaceType.Internal
                ? "An OrgTaskManager can override workflow restrictions."
                : "External space transitions are fixed and role-restricted.";

            throw new InvalidOperationException(
                $"Transition not allowed for role '{callerRole}'. {hint}");
        }
    }
}
