using Edcom.TaskManager.Application;
using Edcom.TaskManager.Infrastructure;
using Edcom.TaskManager.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .ConfigureApplication()
    .ConfigureInfrastructure(builder.Configuration)
    .ConfigureControllers()
    .ConfigureSwagger()
    .ConfigureCors()
    .ConfigureExceptionHandler();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
