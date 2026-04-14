using System.Security.Claims;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Results;
using Edcom.Api.Modules.Issues.Dto;

namespace Edcom.Api.Modules.Issues.Services;

/// <summary>
/// Service interface for ticket operations with all business logic enforcement.
/// All methods return Result<T> for explicit error handling.
/// </summary>
public interface ITicketService
{
    /// <summary>
    /// Create a new ticket with role-based restrictions.
    ///
    /// Rules:
    /// - SystemAdmin: FORBIDDEN
    /// - OrgManager: Can create in ANY space in their org, assign to ANY org member
    /// - SpaceManager: Can create ONLY in assigned spaces, assign to ANY org member
    /// - Employer: Can create ONLY assigned to THEMSELVES (auto-set, not selectable)
    /// </summary>
    Task<Result<IssueDetailDto>> CreateAsync(
        Guid spaceId,
        CreateIssueRequest req,
        ClaimsPrincipal user,
        CancellationToken ct);

    /// <summary>
    /// Update a ticket with field-level permission checks.
    ///
    /// Field permissions depend on role:
    /// - Employer: Can edit own tickets only, within workflow transitions, cannot edit type/assignee
    /// - SpaceManager: Can edit any ticket in their space
    /// - OrgManager: Can edit any ticket in their org, full override (bypass workflow)
    /// </summary>
    Task<Result<IssueDetailDto>> UpdateAsync(
        Guid spaceId,
        Guid issueId,
        UpdateIssueRequest req,
        ClaimsPrincipal user,
        CancellationToken ct);

    /// <summary>
    /// Get ticket details (returns full detail with comments, worklogs, activity).
    /// </summary>
    Task<Result<IssueDetailDto>> GetByIdAsync(
        Guid spaceId,
        Guid issueId,
        ClaimsPrincipal user,
        CancellationToken ct);

    /// <summary>
    /// List tickets in space with optional filtering.
    /// Only returns tickets the user is authorized to see (org/space scoped).
    /// </summary>
    Task<Result<List<IssueListDto>>> ListAsync(
        Guid spaceId,
        IssueFilterDto? filter,
        ClaimsPrincipal user,
        CancellationToken ct);

    /// <summary>
    /// Delete (soft-delete) a ticket.
    /// </summary>
    Task<Result<Unit>> DeleteAsync(
        Guid spaceId,
        Guid issueId,
        ClaimsPrincipal user,
        CancellationToken ct);

    /// <summary>
    /// Log time against a ticket.
    /// - Employer: Only their own tickets
    /// - SpaceManager/OrgManager: Any ticket in scope
    /// </summary>
    Task<Result<Unit>> LogTimeAsync(
        Guid spaceId,
        Guid issueId,
        LogTimeRequest req,
        ClaimsPrincipal user,
        CancellationToken ct);
}

/// <summary>Filter options for listing tickets.</summary>
public record IssueFilterDto(
    Guid? SprintId = null,
    Guid? EpicId = null,
    string? Status = null,
    string? Priority = null,
    Guid? AssigneeId = null,
    bool Backlog = false);

/// <summary>Time log entry request.</summary>
public record LogTimeRequest(
    decimal Hours,
    DateTime Date,
    string? Description = null);

/// <summary>Represents a successful operation with no return value.</summary>
public record Unit;
