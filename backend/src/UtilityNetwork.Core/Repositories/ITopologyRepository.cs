using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface ITopologyRepository
{
    Task<TopologyStatusDto> GetStatusAsync(Guid networkId, CancellationToken ct = default);
    Task<IReadOnlyList<TopologyStatusDto>> GetAllStatusesAsync(CancellationToken ct = default);
    Task<RebuildTopologyResultDto> RebuildTopologyAsync(Guid networkId, CancellationToken ct = default);
    Task<IReadOnlyList<TopologyNodeDto>> GetNodesAsync(Guid networkId, int limit = 500, CancellationToken ct = default);
    Task<IReadOnlyList<TopologyEdgeDto>> GetEdgesAsync(Guid networkId, int limit = 1000, CancellationToken ct = default);
    Task MarkDirtyAsync(Guid networkId, CancellationToken ct = default);
}

