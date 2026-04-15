using Edcom.Infrastructure.Authentication;
using System.Security.Claims;

namespace Edcom.Api.Controllers.Common;

[ApiController]
[Authorize]
public abstract class AuthorizedController : ControllerBase
{
    protected long UserId
    {
        get
        {
            var raw = HttpContext.User.FindFirstValue(CustomClaims.Id)
                ?? throw new UnauthorizedAccessException("Required claim not found.");
            return long.Parse(raw);
        }
    }

    protected string UserEmail =>
        HttpContext.User.FindFirstValue(CustomClaims.Email)
        ?? throw new UnauthorizedAccessException("Required claim not found.");

    protected string UserRole =>
        HttpContext.User.FindFirstValue(CustomClaims.Role)
        ?? throw new UnauthorizedAccessException("Required claim not found.");
}
