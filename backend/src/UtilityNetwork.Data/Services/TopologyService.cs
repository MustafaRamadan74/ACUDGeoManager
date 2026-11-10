using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

#pragma warning disable CA1848, CA1873

namespace UtilityNetwork.Data.Services;

public sealed class TopologyService : ITopologyService
{
    private readonly ITopologyRepository _topologyRepo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly ILogger<TopologyService> _logger;

    public TopologyService(
        ITopologyRepository topologyRepo,
        ICatalogRepository catalogRepo,
        ILogger<TopologyService> logger)
    {
        _topologyRepo = topologyRepo;
        _catalogRepo = catalogRepo;
        _logger = logger;
    }

    public async Task<TopologyStatusDto> GetStatusAsync(Guid networkId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, networkId);
        return await _topologyRepo.GetStatusAsync(networkId, ct);
    }

    public async Task<IReadOnlyList<TopologyStatusDto>> GetAllStatusesAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var all = await _topologyRepo.GetAllStatusesAsync(ct);
        if (user.IsInRole("ADMIN")) return all;

        var networkClaims = user.FindAll("network_id").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return all.Where(s => networkClaims.Contains(s.NetworkId.ToString())).ToList();
    }

    public async Task<RebuildTopologyResultDto> RebuildTopologyAsync(Guid networkId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, networkId);
        _logger.LogInformation("Rebuilding topology for network {NetworkId} requested by {User}", networkId, user.Identity?.Name);
        var result = await _topologyRepo.RebuildTopologyAsync(networkId, ct);
        _logger.LogInformation("Topology rebuilt for network {NetworkId}: {NodeCount} nodes, {EdgeCount} edges in {DurationMs}ms",
            networkId, result.NodeCount, result.EdgeCount, result.DurationMs);
        return result;
    }

    public async Task<IReadOnlyList<RebuildTopologyResultDto>> RebuildAllAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!user.IsInRole("ADMIN"))
        {
            throw new UnauthorizedAccessException("Only administrators can trigger full topology rebuild across all networks.");
        }

        var networks = await _catalogRepo.GetNetworksAsync(ct);
        var results = new List<RebuildTopologyResultDto>();

        foreach (var net in networks)
        {
            _logger.LogInformation("Rebuilding topology for network {NetworkId} ({Code})", net.Id, net.Code);
            var res = await _topologyRepo.RebuildTopologyAsync(net.Id, ct);
            results.Add(res);
        }

        return results;
    }

    private static void EnsureNetworkAccess(ClaimsPrincipal user, Guid networkId)
    {
        if (user.IsInRole("ADMIN")) return;

        var networkClaims = user.FindAll("network_id").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!networkClaims.Contains(networkId.ToString()))
        {
            throw new AuthenticationException($"Access denied to network '{networkId}'. The current user does not have assigned permissions.");
        }
    }
}
