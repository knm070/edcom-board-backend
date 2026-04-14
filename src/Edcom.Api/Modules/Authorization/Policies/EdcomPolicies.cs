using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Edcom.Api.Modules.Authorization.Policies;

// ── Policy name constants ─────────────────────────────────────────────────────

public static class EdcomPolicies
{
    /// <summary>User.IsSystemAdmin == true.</summary>
    public const string SystemAdminOnly = "SystemAdminOnly";

    /// <summary>User is OrgManager in the org from route (orgId route param).</summary>
    public const string OrgManagerOnly = "OrgManagerOnly";

    /// <summary>User is any OrgRole in the org from route.</summary>
    public const string OrgMemberOrAbove = "OrgMemberOrAbove";

    /// <summary>User is OrgManager of the org OR has SpaceAssignment for the space from route.</summary>
    public const string SpaceAssigned = "SpaceAssigned";

    /// <summary>User is OrgManager OR SpaceManager for the space from route (can edit workflow, etc.).</summary>
    public const string CanManageSpace = "CanManageSpace";

    /// <summary>OrgManager, SpaceManager, or Employer who owns the ticket.</summary>
    public const string CanManageTicket = "CanManageTicket";

    /// <summary>OrgManager or SpaceManager for the space (Employer: denied).</summary>
    public const string CanManageSprint = "CanManageSprint";

    /// <summary>OrgManager or SpaceManager for the space (Employer: denied).</summary>
    public const string CanConfigureWorkflow = "CanConfigureWorkflow";
}

// ── Requirements ─────────────────────────────────────────────────────────────

public sealed class SystemAdminRequirement       : IAuthorizationRequirement { }
public sealed class OrgManagerRequirement        : IAuthorizationRequirement { }
public sealed class OrgMemberOrAboveRequirement  : IAuthorizationRequirement { }
public sealed class SpaceAssignedRequirement     : IAuthorizationRequirement { }
public sealed class CanManageSpaceRequirement    : IAuthorizationRequirement { }
public sealed class CanManageTicketRequirement   : IAuthorizationRequirement { }
public sealed class CanManageSprintRequirement   : IAuthorizationRequirement { }
public sealed class CanConfigureWorkflowRequirement : IAuthorizationRequirement { }

// ── Handlers — simple (no route data needed) ─────────────────────────────────

public sealed class SystemAdminHandler : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, SystemAdminRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) ctx.Succeed(req);
        return Task.CompletedTask;
    }
}

// ── Route-context handlers ────────────────────────────────────────────────────
// These handlers read orgId / spaceId from route values via IHttpContextAccessor.

public sealed class OrgManagerHandler(IHttpContextAccessor http)
    : AuthorizationHandler<OrgManagerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, OrgManagerRequirement req)
    {
        if (TryGetOrgId(out var orgId) &&
            ctx.User.HasOrgRole(orgId, OrgRole.OrgManager))
            ctx.Succeed(req);
        return Task.CompletedTask;

        bool TryGetOrgId(out Guid id)
        {
            id = Guid.Empty;
            var raw = http.HttpContext?.GetRouteValue("orgId")?.ToString();
            return raw is not null && Guid.TryParse(raw, out id);
        }
    }
}

public sealed class OrgMemberOrAboveHandler(IHttpContextAccessor http)
    : AuthorizationHandler<OrgMemberOrAboveRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, OrgMemberOrAboveRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) { ctx.Succeed(req); return Task.CompletedTask; }

        var raw = http.HttpContext?.GetRouteValue("orgId")?.ToString();
        if (raw is not null && Guid.TryParse(raw, out var orgId) &&
            ctx.User.IsMemberOfOrg(orgId))
            ctx.Succeed(req);

        return Task.CompletedTask;
    }
}

public sealed class SpaceAssignedHandler(IHttpContextAccessor http)
    : AuthorizationHandler<SpaceAssignedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, SpaceAssignedRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) { ctx.Succeed(req); return Task.CompletedTask; }

        var orgRaw   = http.HttpContext?.GetRouteValue("orgId")?.ToString();
        var spaceRaw = http.HttpContext?.GetRouteValue("spaceId")?.ToString();

        if (orgRaw   is null || !Guid.TryParse(orgRaw,   out var orgId))   return Task.CompletedTask;
        if (spaceRaw is null || !Guid.TryParse(spaceRaw, out var spaceId)) return Task.CompletedTask;

        if (ctx.User.IsSpaceManagerOrAbove(orgId, spaceId))
            ctx.Succeed(req);

        return Task.CompletedTask;
    }
}

public sealed class CanManageSpaceHandler(IHttpContextAccessor http)
    : AuthorizationHandler<CanManageSpaceRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, CanManageSpaceRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) { ctx.Succeed(req); return Task.CompletedTask; }

        var orgRaw   = http.HttpContext?.GetRouteValue("orgId")?.ToString();
        var spaceRaw = http.HttpContext?.GetRouteValue("spaceId")?.ToString();

        if (orgRaw   is null || !Guid.TryParse(orgRaw,   out var orgId))   return Task.CompletedTask;
        if (spaceRaw is null || !Guid.TryParse(spaceRaw, out var spaceId)) return Task.CompletedTask;

        if (ctx.User.IsSpaceManagerOrAbove(orgId, spaceId))
            ctx.Succeed(req);

        return Task.CompletedTask;
    }
}

public sealed class CanManageSprintHandler(IHttpContextAccessor http)
    : AuthorizationHandler<CanManageSprintRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, CanManageSprintRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) { ctx.Succeed(req); return Task.CompletedTask; }

        var orgRaw   = http.HttpContext?.GetRouteValue("orgId")?.ToString();
        var spaceRaw = http.HttpContext?.GetRouteValue("spaceId")?.ToString();

        if (orgRaw   is null || !Guid.TryParse(orgRaw,   out var orgId))   return Task.CompletedTask;
        if (spaceRaw is null || !Guid.TryParse(spaceRaw, out var spaceId)) return Task.CompletedTask;

        if (ctx.User.IsSpaceManagerOrAbove(orgId, spaceId))
            ctx.Succeed(req);

        return Task.CompletedTask;
    }
}

public sealed class CanConfigureWorkflowHandler(IHttpContextAccessor http)
    : AuthorizationHandler<CanConfigureWorkflowRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, CanConfigureWorkflowRequirement req)
    {
        if (ctx.User.IsSystemAdmin()) { ctx.Succeed(req); return Task.CompletedTask; }

        var orgRaw   = http.HttpContext?.GetRouteValue("orgId")?.ToString();
        var spaceRaw = http.HttpContext?.GetRouteValue("spaceId")?.ToString();

        if (orgRaw   is null || !Guid.TryParse(orgRaw,   out var orgId))   return Task.CompletedTask;
        if (spaceRaw is null || !Guid.TryParse(spaceRaw, out var spaceId)) return Task.CompletedTask;

        if (ctx.User.IsSpaceManagerOrAbove(orgId, spaceId))
            ctx.Succeed(req);

        return Task.CompletedTask;
    }
}
