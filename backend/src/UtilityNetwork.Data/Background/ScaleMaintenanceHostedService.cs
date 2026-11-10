using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UtilityNetwork.Core.Repositories;

#pragma warning disable CA1848, CA1873

namespace UtilityNetwork.Data.Background;

/// <summary>
/// Background worker service providing debounced topology rebuild and periodic materialized summary refreshes for city-scale performance.
/// </summary>
public sealed class ScaleMaintenanceHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScaleMaintenanceHostedService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public ScaleMaintenanceHostedService(
        IServiceProvider serviceProvider,
        ILogger<ScaleMaintenanceHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scale Maintenance Hosted Service started.");

        // Initial brief delay before starting background maintenance
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tileRepo = scope.ServiceProvider.GetRequiredService<ITileRepository>();
                var topologyRepo = scope.ServiceProvider.GetRequiredService<ITopologyRepository>();

                // 1. Refresh materialized summaries
                _logger.LogInformation("Background maintenance: Refreshing materialized views...");
                await tileRepo.RefreshMaterializedSummariesAsync(stoppingToken);

                // 2. Debounced topology check
                var statuses = await topologyRepo.GetAllStatusesAsync(stoppingToken);
                foreach (var status in statuses.Where(s => s.IsDirty))
                {
                    _logger.LogInformation("Background maintenance: Debounced topology rebuild for dirty network {NetworkId}", status.NetworkId);
                    await topologyRepo.RebuildTopologyAsync(status.NetworkId, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Background scale maintenance cycle encountered an error, will retry next interval.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Scale Maintenance Hosted Service stopped.");
    }
}

