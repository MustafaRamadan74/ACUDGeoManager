namespace UtilityNetwork.Core.Repositories;

public interface ITileRepository
{
    Task<byte[]?> GetVectorTileAsync(Guid? networkId, int z, int x, int y, CancellationToken ct = default);
    Task RefreshMaterializedSummariesAsync(CancellationToken ct = default);
}

