using System.Security.Claims;
using System.Text.Json;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Modules.Authorization.Claims;

namespace Edcom.Api.Tests.Authorization;

/// <summary>
/// Fluent builder for creating test <see cref="ClaimsPrincipal"/> instances
/// without hitting a real JWT pipeline.
/// </summary>
internal sealed class ClaimsPrincipalBuilder
{
    private Guid _userId = Guid.NewGuid();
    private bool _isAdmin;
    private readonly List<OrgRoleClaim> _orgRoles = [];

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ClaimsPrincipalBuilder WithUserId(Guid id) { _userId = id; return this; }

    public ClaimsPrincipalBuilder AsSystemAdmin() { _isAdmin = true; return this; }

    public ClaimsPrincipalBuilder WithOrgRole(Guid orgId, OrgRole role, string orgSlug = "test-org")
    {
        _orgRoles.Add(new OrgRoleClaim(orgId, orgSlug, role.ToString()));
        return this;
    }

    public ClaimsPrincipal Build()
    {
        var claims = new List<Claim>
        {
            new(EdcomClaimTypes.UserId,   _userId.ToString()),
            new(EdcomClaimTypes.IsAdmin,  _isAdmin ? "true" : "false"),
            new(EdcomClaimTypes.OrgRoles, JsonSerializer.Serialize(_orgRoles, _jsonOpts)),
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
