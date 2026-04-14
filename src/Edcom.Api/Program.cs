using System.Text;
using FluentValidation;
using Edcom.Api.Infrastructure.Data;
using Edcom.Api.Infrastructure.Data.Entities;
using Edcom.Api.Infrastructure.Hubs;
using Edcom.Api.Modules.Authorization.Policies;
using Edcom.Api.Modules.Authorization.Services;
using Edcom.Api.Modules.CrossOrgTickets.Services;
using Edcom.Api.Modules.Identity.Services;
using Edcom.Api.Modules.Spaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ─────────────────────────────────────────────
builder.Services.AddControllers();

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

// ── Database — SQLite for dev, PostgreSQL for prod ───────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(conn) && conn.StartsWith("Host="))
        options.UseNpgsql(conn);
    else
        options.UseSqlite(conn ?? "Data Source=edcom.db");
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
    opts.AddPolicy(EdcomPolicies.SystemAdmin,
        p => p.AddRequirements(new SystemAdminRequirement()));
    opts.AddPolicy(EdcomPolicies.AnyOrgManager,
        p => p.AddRequirements(new AnyOrgManagerRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, SystemAdminHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyOrgManagerHandler>();

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
builder.Services.AddScoped<ISpaceProvisioningService, SpaceProvisioningService>();
builder.Services.AddScoped<IWorkflowTransitionService, WorkflowTransitionService>();
builder.Services.AddScoped<ICrossOrgTicketService, CrossOrgTicketService>();

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

    // Additive column migrations for SQLite (EnsureCreated won't add new columns to existing DBs)
    ApplyDevMigrations(db);

    const string adminEmail = "admin@edcom.dev";
    if (!db.Users.Any(u => u.Email == adminEmail))
    {
        db.Users.Add(new User
        {
            Email        = adminEmail,
            FullName     = "System Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            IsSystemAdmin = true,
            IsActive     = true
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

// ── Dev schema migrations (SQLite only) ──────────────────────
// EnsureCreated won't add columns to existing tables.
// Each entry here is idempotent — safe to run on every startup.
static void ApplyDevMigrations(AppDbContext db)
{
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        conn.Open();

    void AddColumnIfMissing(string table, string column, string sqlDef)
    {
        using var pragmaCmd = conn.CreateCommand();
        pragmaCmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = pragmaCmd.ExecuteReader();
        var found = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }
        reader.Close();

        if (!found)
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlDef}";
            alterCmd.ExecuteNonQuery();
        }
    }

    // Spaces
    AddColumnIfMissing("Spaces", "Status",        "TEXT NOT NULL DEFAULT 'Active'");
    AddColumnIfMissing("Spaces", "BoardTemplate",  "TEXT");
    AddColumnIfMissing("Spaces", "UpdatedAt",      "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");

    // Issues
    AddColumnIfMissing("Issues", "StoryPoints",          "INTEGER");
    AddColumnIfMissing("Issues", "BacklogOrder",         "INTEGER NOT NULL DEFAULT 0");
    AddColumnIfMissing("Issues", "FileAttachmentsJson",  "TEXT");
    AddColumnIfMissing("Issues", "EstimationHours",      "REAL");
    AddColumnIfMissing("Issues", "UpdatedAt",            "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");

    // Sprints
    AddColumnIfMissing("Sprints", "Goal",       "TEXT");
    AddColumnIfMissing("Sprints", "StartDate",  "TEXT");
    AddColumnIfMissing("Sprints", "EndDate",    "TEXT");
    AddColumnIfMissing("Sprints", "UpdatedAt",  "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");

    // New tables — EnsureCreated won't add these to an existing DB
    void ExecIfNotExists(string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    ExecIfNotExists("""
        CREATE TABLE IF NOT EXISTS "Worklogs" (
            "Id"          TEXT NOT NULL PRIMARY KEY,
            "IssueId"     TEXT NOT NULL REFERENCES "Issues"("Id") ON DELETE CASCADE,
            "UserId"      TEXT NOT NULL REFERENCES "Users"("Id"),
            "Hours"       REAL NOT NULL,
            "Description" TEXT,
            "Date"        TEXT NOT NULL,
            "CreatedAt"   TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
        )
        """);

    ExecIfNotExists("""
        CREATE TABLE IF NOT EXISTS "SprintVelocityRecords" (
            "Id"               TEXT NOT NULL PRIMARY KEY,
            "SprintId"         TEXT NOT NULL UNIQUE REFERENCES "Sprints"("Id") ON DELETE CASCADE,
            "SpaceId"          TEXT NOT NULL REFERENCES "Spaces"("Id"),
            "CommittedPoints"  INTEGER NOT NULL DEFAULT 0,
            "CompletedPoints"  INTEGER NOT NULL DEFAULT 0,
            "CompletedAt"      TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
        )
        """);

    conn.Close();
}
