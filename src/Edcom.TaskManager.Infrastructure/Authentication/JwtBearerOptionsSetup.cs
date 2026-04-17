using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Compensation.Infrastructure.Authentication;

public class JwtBearerOptionsSetup(IOptions<JwtOptions> options) : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions = options.Value;

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.SaveToken = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        options.TokenValidationParameters.ValidIssuer = _jwtOptions.Issuer;
        options.TokenValidationParameters.ValidAudience = _jwtOptions.Audience;
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrEmpty(_jwtOptions.Audience);
        options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrEmpty(_jwtOptions.Issuer);
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
    }
}
