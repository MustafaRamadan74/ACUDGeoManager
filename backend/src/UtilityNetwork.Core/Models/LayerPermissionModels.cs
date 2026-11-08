namespace UtilityNetwork.Core.Models;

public enum LayerAction
{
    Read,
    Create,
    Update,
    Delete
}

public sealed record LayerPermissionDto(
    Guid? Id,
    Guid UserId,
    string FeatureTypeCode,
    string? FeatureTypeName,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record UpdateLayerPermissionRequest(
    string FeatureTypeCode,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record UserLayerPermissionsUpdateRequest(
    IReadOnlyList<UpdateLayerPermissionRequest> Permissions);
