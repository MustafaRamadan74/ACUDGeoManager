using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface ILayerPermissionRepository
{
    Task<IReadOnlyList<LayerPermissionDto>> GetUserLayerPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<LayerPermissionDto?> GetUserLayerPermissionAsync(Guid userId, string featureTypeCode, CancellationToken ct = default);
    Task SetUserLayerPermissionsAsync(Guid userId, IEnumerable<UpdateLayerPermissionRequest> permissions, CancellationToken ct = default);
}
