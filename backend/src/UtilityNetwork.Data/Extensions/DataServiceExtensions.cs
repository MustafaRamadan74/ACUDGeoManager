using Microsoft.Extensions.DependencyInjection;
using UtilityNetwork.Data.Health;
using UtilityNetwork.Data.Migrations;

namespace UtilityNetwork.Data.Extensions;

public static class DataServiceExtensions
{
    public static IServiceCollection AddDataInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();

        services.AddHealthChecks()
            .AddCheck<NpgsqlHealthCheck>("postgis-db", tags: ["db", "ready"]);

        return services;
    }
}
