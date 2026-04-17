using Edcom.TaskManager.Application.Services.Auth;
using Edcom.TaskManager.Application.Services.Organization;

namespace Edcom.TaskManager.Application;

public static class Dependencies
{
    public static IServiceCollection ConfigureApplication(this IServiceCollection services)
    {
        // Register validators from this assembly
        services.AddValidatorsFromAssemblyContaining(typeof(Dependencies));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();

        return services;
    }
}
