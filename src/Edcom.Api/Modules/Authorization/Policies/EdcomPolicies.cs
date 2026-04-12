using Edcom.Api.Modules.Authorization.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Edcom.Api.Modules.Authorization.Policies;

// ── Policy name constants ─────────────────────────────────────────────────────
public static class EdcomPolicies
{
    /// <summary>Only system-wide Admins (IsSystemAdmin = true).</summary>
    public const string SystemAdmin = "SystemAdmin";

    /// <summary>Any authenticated user who is an Admin OR an OrgTaskManager in at least one org.</summary>
    public const string AnyOrgManager = "AnyOrgManager";
}

// ── Requirements ─────────────────────────────────────────────────────────────
public sealed class SystemAdminRequirement : IAuthorizationRequirement { }

public sealed class AnyOrgManagerRequirement : IAuthorizationRequirement { }

// ── Handlers ─────────────────────────────────────────────────────────────────
public sealed class SystemAdminHandler
    : AuthorizationHandler<SystemAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, SystemAdminRequirement req)
    {
        if (ctx.User.IsSystemAdmin())
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}

public sealed class AnyOrgManagerHandler
    : AuthorizationHandler<AnyOrgManagerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx, AnyOrgManagerRequirement req)
    {
        if (ctx.User.IsSystemAdmin() ||
            ctx.User.HasAnyOrgRole(Infrastructure.Data.Entities.MemberRole.OrgTaskManager))
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}
