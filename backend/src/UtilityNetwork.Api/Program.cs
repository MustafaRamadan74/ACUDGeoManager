using System.Globalization;
using Serilog;
using UtilityNetwork.Api.Middleware;
using UtilityNetwork.Core.Configuration;
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

// Configuration binding
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

// Services
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddDataInfrastructure();

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

// Swagger / OpenAPI specification at /api/docs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ACUD GeoManager API",
        Version = "v1",
        Description = "Enterprise Web GIS utility network management platform API"
    });
});

var app = builder.Build();

// Execute CLI migration if requested via command-line arguments
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

// Automatically apply migrations on startup in Development environment
if (app.Environment.IsDevelopment())
{
    var migrator = app.Services.GetRequiredService<IDatabaseMigrator>();
    var migrationResult = migrator.MigrateDatabase();
    if (!migrationResult.IsSuccessful)
    {
        throw new InvalidOperationException($"Development database migration failed: {migrationResult.ErrorMessage}");
    }
}

// Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Centralized error handling
app.UseExceptionHandler();

// Swagger Documentation at /api/docs
app.UseSwagger(c =>
{
    c.RouteTemplate = "api/docs/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/docs/v1/swagger.json", "ACUD GeoManager API v1");
    c.RoutePrefix = "api/docs";
});

app.UseCors();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
