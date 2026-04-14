using System.Text;
using FluentValidation;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Hubs;
using Edcom.Api.Modules.Authorization.Policies;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.Identity.Services;
using Edcom.Api.Modules.Issues.Services;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ─────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// ── Swagger ──────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Edcom API",
        Version = "v1",
        Description = "Cross-organizational workspace platform"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database — PostgreSQL ────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=edcom;Username=postgres;Password=postgres";
    options.UseNpgsql(conn);
});

// ── JWT Authentication ───────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "edcom-super-secret-jwt-signing-key-minimum-32-chars!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"]   ?? "edcom",
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "edcom-client",
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero
        };
        // Allow JWT via query string for SignalR connections
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy(EdcomPolicies.SystemAdminOnly,
        p => p.AddRequirements(new SystemAdminRequirement()));
    opts.AddPolicy(EdcomPolicies.OrgManagerOnly,
        p => p.AddRequirements(new OrgManagerRequirement()));
    opts.AddPolicy(EdcomPolicies.OrgMemberOrAbove,
        p => p.AddRequirements(new OrgMemberOrAboveRequirement()));
    opts.AddPolicy(EdcomPolicies.SpaceAssigned,
        p => p.AddRequirements(new SpaceAssignedRequirement()));
    opts.AddPolicy(EdcomPolicies.CanManageSpace,
        p => p.AddRequirements(new CanManageSpaceRequirement()));
    opts.AddPolicy(EdcomPolicies.CanManageSprint,
        p => p.AddRequirements(new CanManageSprintRequirement()));
    opts.AddPolicy(EdcomPolicies.CanConfigureWorkflow,
        p => p.AddRequirements(new CanConfigureWorkflowRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, SystemAdminHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrgManagerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrgMemberOrAboveHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SpaceAssignedHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CanManageSpaceHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CanManageSprintHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CanConfigureWorkflowHandler>();

// ── SignalR ──────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── MediatR ──────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── FluentValidation ────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ── CORS ─────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("Frontend", policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Accept any localhost origin in dev so Vite port shifts (5173 → 5174+) never break auth
        policy.SetIsOriginAllowed(origin =>
                  Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                  (uri.Host == "localhost" || uri.Host == "127.0.0.1"))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
    else
    {
        var origin = builder.Configuration["Frontend:Url"]
            ?? throw new InvalidOperationException("Frontend:Url must be configured in production.");
        policy.WithOrigins(origin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
}));

// ── Module Services ──────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IWorkflowTransitionService, WorkflowTransitionService>();
builder.Services.AddScoped<ITicketService, TicketService>();

// ── App ──────────────────────────────────────────────────────
var app = builder.Build();

// Global exception handler
app.UseExceptionHandler(err => err.Run(async ctx =>
{
    ctx.Response.ContentType = "application/json";
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, message) = ex switch
    {
        InvalidOperationException   => (400, ex.Message),
        UnauthorizedAccessException => (401, ex.Message),
        KeyNotFoundException        => (404, ex.Message),
        _                           => (500, app.Environment.IsDevelopment()
            ? (ex?.ToString() ?? "An unexpected error occurred.")
            : "An unexpected error occurred.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { error = message, status });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Edcom API v1");
        c.RoutePrefix = "swagger";
    });

    // Auto-apply schema on first run and seed default admin
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    const string adminEmail = "admin@edcom.dev";
    if (!db.Users.Any(u => u.Email == adminEmail))
    {
        db.Users.Add(new User
        {
            Email         = adminEmail,
            FullName      = "System Admin",
            PasswordHash  = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            IsSystemAdmin = true,
            IsActive      = true
        });
        db.SaveChanges();
    }
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<EdcomHub>("/hubs/edcom");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.Run();
