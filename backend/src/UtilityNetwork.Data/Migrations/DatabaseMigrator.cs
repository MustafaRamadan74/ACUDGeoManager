using System.Reflection;
using DbUp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UtilityNetwork.Core.Configuration;

namespace UtilityNetwork.Data.Migrations;

public sealed record MigrationResult(bool IsSuccessful, int ScriptsExecuted, string? ErrorMessage);

public interface IDatabaseMigrator
{
    MigrationResult MigrateDatabase();
}

public sealed partial class DatabaseMigrator : IDatabaseMigrator
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IOptions<DatabaseOptions> dbOptions, ILogger<DatabaseMigrator> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Information,
        Message = "Database migration completed successfully. {Count} scripts applied.")]
    private static partial void LogMigrationSuccess(ILogger logger, int count);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Error,
        Message = "Database migration failed: {ErrorMessage}")]
    private static partial void LogMigrationFailed(ILogger logger, string errorMessage, Exception? exception);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Error,
        Message = "Unexpected error occurred during database migration: {Message}")]
    private static partial void LogMigrationException(ILogger logger, string message, Exception? exception);

    public MigrationResult MigrateDatabase()
    {
        if (string.IsNullOrWhiteSpace(_dbOptions.DefaultConnection))
        {
            return new MigrationResult(false, 0, "Database connection string is not configured.");
        }

        try
        {
            EnsureDatabase.For.PostgresqlDatabase(_dbOptions.DefaultConnection);

            var upgrader = DeployChanges.To
                .PostgresqlDatabase(_dbOptions.DefaultConnection)
                .WithScriptsEmbeddedInAssembly(
                    Assembly.GetExecutingAssembly(),
                    scriptName => scriptName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                .LogToNowhere()
                .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                var errorMsg = result.Error?.Message ?? "Unknown DbUp error";
                LogMigrationFailed(_logger, errorMsg, result.Error);
                return new MigrationResult(false, 0, errorMsg);
            }

            var scriptsCount = result.Scripts.Count();
            LogMigrationSuccess(_logger, scriptsCount);
            return new MigrationResult(true, scriptsCount, null);
        }
        catch (Exception ex)
        {
            LogMigrationException(_logger, ex.Message, ex);
            return new MigrationResult(false, 0, ex.Message);
        }
    }
}
