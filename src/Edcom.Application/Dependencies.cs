namespace Edcom.Application;

public static class Dependencies
{
    public static IServiceCollection ConfigureApplication(this IServiceCollection services)
    {
        // Register validators from this assembly
        services.AddValidatorsFromAssemblyContaining(typeof(Dependencies));

        // TODO: Register services here as you add them:
        // services.AddScoped<IExampleService, ExampleService>();

        return services;
    }
}
