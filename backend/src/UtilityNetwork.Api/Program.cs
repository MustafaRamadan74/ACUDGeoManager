using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using UtilityNetwork.Api.Middleware;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Core.Validators;
using UtilityNetwork.Data.Extensions;
using UtilityNetwork.Data.Migrations;

var builder = WebApplication.CreateBuilder(args);

// Structured logging with Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture));

// Strongly typed configuration options
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

// FluentValidation registration
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// Strict JSON serialization & Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddDataInfrastructure();

// Rate Limiting for Auth endpoints
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthRateLimit", opt =>
    {
        opt.PermitLimit = 15;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Authentication & JWT Bearer setup
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretKeyAtLeast32BytesLongForSecurityTestingOnly1234567890!";
var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // local development
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ACUDGeoManager",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ACUDGeoManagerClient",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = JwtRegisteredClaimNames.UniqueName
    };
});

// Authorization with fallback policy: Deny by default
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("Viewer", p => p.RequireRole("VIEWER", "EDITOR", "APPROVER", "ADMIN"));
    options.AddPolicy("Editor", p => p.RequireRole("EDITOR", "APPROVER", "ADMIN"));
    options.AddPolicy("Approver", p => p.RequireRole("APPROVER", "ADMIN"));
    options.AddPolicy("Admin", p => p.RequireRole("ADMIN"));
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Value?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ACUD GeoManager API",
        Version = "v1",
        Description = "Enterprise Web GIS utility network management platform API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// CLI migrations
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    var migrator = app.Services.GetRequiredService<IDatabaseMigrator>();
    var result = migrator.MigrateDatabase();
    if (!result.IsSuccessful)
    {
        Environment.ExitCode = 1;
        return;
    }
    return;
}

// CLI admin bootstrap
if (args.Contains("--bootstrap-admin", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
    await bootstrapper.EnsureAdminCreatedAsync();
    return;
}

// Automatically apply migrations and bootstrap admin in Development
if (app.Environment.IsDevelopment())
{
    var migrator = app.Services.GetRequiredService<IDatabaseMigrator>();
    var migrationResult = migrator.MigrateDatabase();
    if (!migrationResult.IsSuccessful)
    {
        throw new InvalidOperationException($"Development database migration failed: {migrationResult.ErrorMessage}");
    }

    using var scope = app.Services.CreateScope();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
    await bootstrapper.EnsureAdminCreatedAsync();
}

// Pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();

app.UseSwagger(c =>
{
    c.RouteTemplate = "api/docs/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/docs/v1/swagger.json", "ACUD GeoManager API v1");
    c.RoutePrefix = "api/docs";
});

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
