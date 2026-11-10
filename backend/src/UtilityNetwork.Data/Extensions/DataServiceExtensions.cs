using Microsoft.Extensions.DependencyInjection;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Data.Health;
using UtilityNetwork.Data.Migrations;
using UtilityNetwork.Data.Repositories;
using UtilityNetwork.Data.Services;
using UtilityNetwork.Data.UnitOfWork;

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
        services.AddScoped<ILayerPermissionRepository, LayerPermissionRepository>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

        // Unit of Work and Audit History
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped<IHistoryRepository, HistoryRepository>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IUndoRedoRepository, UndoRedoRepository>();
        services.AddScoped<IUndoRedoService, UndoRedoService>();

        // Catalog and Features CRUD
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ISpatialQueryRepository, SpatialQueryRepository>();
        services.AddSingleton<IFeatureValidationService, FeatureValidationService>();
        services.AddScoped<IFeatureRepository, FeatureRepository>();
        services.AddScoped<IFeatureService, FeatureService>();

        // Validation Engine & Rules
        services.AddScoped<IValidationRepository, ValidationRepository>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.InvalidGeometryRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.EmptyGeometryRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.ZeroLengthLineRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.DuplicateFeaturesRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.MissingRequiredAttributeRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.OverlappingPolygonsRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.FeatureOutsideBoundaryRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.ValveOffPipeRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.DanglingLineRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.ReversedFlowRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.IsolatedNoSourceRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.DirectionUnknownRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.DirectionalCycleRule>();
        services.AddTransient<UtilityNetwork.Core.Validation.IValidationRule, UtilityNetwork.Data.Validation.Rules.MissingTerminalFeatureRule>();

        // Import & Export Services
        services.AddSingleton<IImportPreviewStore, MemoryImportPreviewStore>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IImportService, ImportService>();

        // Bookmarks
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<IBookmarkService, BookmarkService>();

        // Analysis & Reporting
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<IReportService, ReportService>();

        // Approvals & Workflow
        services.AddScoped<IPendingChangeRepository, PendingChangeRepository>();
        services.AddScoped<IApprovalService, ApprovalService>();

        // Topology Engine & Network Trace
        services.AddScoped<ITopologyRepository, TopologyRepository>();
        services.AddScoped<ITopologyService, TopologyService>();
        services.AddScoped<ITraceRepository, TraceRepository>();
        services.AddScoped<ITraceService, TraceService>();

        services.AddHealthChecks()
            .AddCheck<NpgsqlHealthCheck>("postgis-db", tags: ["db", "ready"]);

        return services;
    }
}
