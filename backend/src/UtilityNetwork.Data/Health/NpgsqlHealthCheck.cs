using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;

namespace UtilityNetwork.Data.Health;

public sealed class NpgsqlHealthCheck : IHealthCheck
{
    private readonly DatabaseOptions _dbOptions;

    public NpgsqlHealthCheck(IOptions<DatabaseOptions> dbOptions)
    {
        _dbOptions = dbOptions.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_dbOptions.DefaultConnection))
        {
            return HealthCheckResult.Unhealthy("Database connection string is missing or empty.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(_dbOptions.DefaultConnection);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("PostGIS database is connected and responsive.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                description: "PostGIS database connection check failed.",
                exception: ex);
        }
    }
}

