using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<NetworkDto>> GetNetworksAsync(CancellationToken ct = default);
    Task<NetworkDto?> GetNetworkByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureTypeDto>> GetFeatureTypesAsync(CancellationToken ct = default);
    Task<FeatureTypeDto?> GetFeatureTypeByCodeAsync(string code, CancellationToken ct = default);
}
