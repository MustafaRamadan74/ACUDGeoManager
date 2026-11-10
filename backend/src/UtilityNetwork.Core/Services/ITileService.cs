using System.Security.Claims;

namespace UtilityNetwork.Core.Services;

public interface ITileService
{
    Task<byte[]?> GetVectorTileAsync(Guid? networkId, int z, int x, int y, ClaimsPrincipal user, CancellationToken ct = default);
    Task RefreshMaterializedSummariesAsync(ClaimsPrincipal user, CancellationToken ct = default);
}

