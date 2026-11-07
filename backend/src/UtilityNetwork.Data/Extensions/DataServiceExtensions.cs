using Microsoft.Extensions.DependencyInjection;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Data.Health;
using UtilityNetwork.Data.Migrations;
using UtilityNetwork.Data.Repositories;
using UtilityNetwork.Data.Services;

namespace UtilityNetwork.Data.Extensions;

public static class DataServiceExtensions
{
    public static IServiceCollection AddDataInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

        services.AddHealthChecks()
            .AddCheck<NpgsqlHealthCheck>("postgis-db", tags: ["db", "ready"]);

        return services;
    }
}
