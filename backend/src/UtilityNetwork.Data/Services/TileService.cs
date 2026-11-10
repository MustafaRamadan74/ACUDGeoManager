using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

#pragma warning disable CA1848, CA1873

namespace UtilityNetwork.Data.Services;

public sealed class TileService : ITileService
{
    private readonly ITileRepository _tileRepo;
    private readonly ILogger<TileService> _logger;

    public TileService(ITileRepository tileRepo, ILogger<TileService> logger)
    {
        _tileRepo = tileRepo;
        _logger = logger;
    }

    public async Task<byte[]?> GetVectorTileAsync(Guid? networkId, int z, int x, int y, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (networkId.HasValue)
        {
            EnsureNetworkAccess(user, networkId.Value);
        }

        return await _tileRepo.GetVectorTileAsync(networkId, z, x, y, ct);
    }

    public async Task RefreshMaterializedSummariesAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!user.IsInRole("ADMIN") && !user.IsInRole("EDITOR"))
        {
            throw new UnauthorizedAccessException("Only administrators and editors can trigger materialized summary refresh.");
        }

        _logger.LogInformation("Refreshing materialized summaries requested by {User}", user.Identity?.Name);
        await _tileRepo.RefreshMaterializedSummariesAsync(ct);
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

