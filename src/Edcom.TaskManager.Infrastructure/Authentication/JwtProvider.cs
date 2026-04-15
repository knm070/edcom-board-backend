using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Edcom.TaskManager.Infrastructure.Authentication;

internal sealed class JwtProvider(IOptions<JwtOptions> options) : IJwtProvider
{
    private readonly JwtOptions _options = options.Value;

    public string Generate(long userId, string email, string role)
    {
        var claims = new[]
        {
            new Claim(CustomClaims.Id,    userId.ToString()),
            new Claim(CustomClaims.Email, email),
            new Claim(CustomClaims.Role,  role),
        };

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires     = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer:             _options.Issuer,
            audience:           _options.Audience,
            claims:             claims,
            expires:            expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
