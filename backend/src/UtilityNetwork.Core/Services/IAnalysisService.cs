using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

/// <summary>
/// Service providing server-side spatial analysis: geodesic buffers, service areas, spatial joins, and statistics.
/// </summary>
public interface IAnalysisService
{
    Task<BufferAnalysisResultDto> RunBufferAnalysisAsync(BufferAnalysisRequest request, CancellationToken ct = default);
    Task<ServiceAreaResultDto> RunServiceAreaAnalysisAsync(ServiceAreaRequest request, CancellationToken ct = default);
    Task<SpatialJoinResultDto> RunSpatialJoinAsync(SpatialJoinRequest request, CancellationToken ct = default);
    Task<NetworkStatisticsDto> GetNetworkStatisticsAsync(Guid networkId, CancellationToken ct = default);
}
