using Edcom.TaskManager.Infrastructure.Authentication;
using Edcom.TaskManager.Infrastructure.Helpers;
using Edcom.TaskManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;

namespace Edcom.TaskManager.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection ConfigureInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .ConfigureDbContext(configuration)
            .ConfigureJwt(configuration);

        services.AddSingleton<PasswordHasher>();

        // TODO: Register brokers here:
        // services.AddScoped<IExampleBroker, ExampleBroker>();
        // services.AddHttpClient<IExampleBroker, ExampleBroker>(...);

        return services;
    }

    private static IServiceCollection ConfigureDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dataSource = new NpgsqlDataSourceBuilder(
            configuration.GetConnectionString("DefaultConnection"))
            .EnableDynamicJson()
            .Build();

        services.AddDbContext<AppDbContext>(options =>
            options
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention());

        return services;
    }

    private static IServiceCollection ConfigureJwt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtProvider, JwtProvider>();

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()!;

        services.ConfigureOptions<JwtBearerOptionsSetup>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        return services;
    }
}
