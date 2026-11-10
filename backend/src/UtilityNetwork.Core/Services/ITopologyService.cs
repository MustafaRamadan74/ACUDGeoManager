using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface ITopologyService
{
    Task<TopologyStatusDto> GetStatusAsync(Guid networkId, ClaimsPrincipal user, CancellationToken ct = default);
    Task<IReadOnlyList<TopologyStatusDto>> GetAllStatusesAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<RebuildTopologyResultDto> RebuildTopologyAsync(Guid networkId, ClaimsPrincipal user, CancellationToken ct = default);
    Task<IReadOnlyList<RebuildTopologyResultDto>> RebuildAllAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

