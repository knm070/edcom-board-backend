using Edcom.TaskManager.Application;
using Edcom.TaskManager.Infrastructure;
using Edcom.TaskManager.Infrastructure.Persistence;
using Edcom.TaskManager.Api;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureApplication()
    .ConfigureInfrastructure(builder.Configuration)
    .ConfigureControllers()
    .ConfigureSwagger()
    .ConfigureCors()
    .ConfigureExceptionHandler();

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Edcom TaskManager API")
            .WithOpenApiRoutePattern("/swagger/v1/swagger.json");
    });
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
