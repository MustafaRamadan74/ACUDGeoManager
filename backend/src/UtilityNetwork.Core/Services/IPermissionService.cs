using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IPermissionService
{
    Task EnsureLayerPermissionAsync(
        ClaimsPrincipal user,
        Guid networkId,
        string featureTypeCode,
        LayerAction action,
        CancellationToken ct = default);

    Task<bool> HasLayerPermissionAsync(
        ClaimsPrincipal user,
        Guid networkId,
        string featureTypeCode,
        LayerAction action,
        CancellationToken ct = default);

    Task<IReadOnlyList<LayerPermissionDto>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);

    Task SetUserPermissionsAsync(
        Guid userId,
        IEnumerable<UpdateLayerPermissionRequest> permissions,
        CancellationToken ct = default);
}

