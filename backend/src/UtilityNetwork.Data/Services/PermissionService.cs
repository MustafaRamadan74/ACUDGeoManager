using System.Security.Claims;
using UtilityNetwork.Core.Exceptions;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly ILayerPermissionRepository _layerPermissionRepository;
    private readonly IUserRepository _userRepository;

    public PermissionService(
        ILayerPermissionRepository layerPermissionRepository,
        IUserRepository userRepository)
    {
        _layerPermissionRepository = layerPermissionRepository;
        _userRepository = userRepository;
    }

    public async Task EnsureLayerPermissionAsync(
        ClaimsPrincipal user,
        Guid networkId,
        string featureTypeCode,
        LayerAction action,
        CancellationToken ct = default)
    {
        var hasPermission = await HasLayerPermissionAsync(user, networkId, featureTypeCode, action, ct);
        if (!hasPermission)
        {
            throw new ForbiddenException(
                $"Permission denied: user cannot perform '{action}' on layer '{featureTypeCode}' in workspace '{networkId}'.");
        }
    }

    public async Task<bool> HasLayerPermissionAsync(
        ClaimsPrincipal user,
        Guid networkId,
        string featureTypeCode,
        LayerAction action,
        CancellationToken ct = default)
    {
        var role = user.FindFirstValue(ClaimTypes.Role)?.ToUpperInvariant() ?? "VIEWER";

        // 1. Check Role Ceiling: most restrictive wins
        if (!CheckRoleCeiling(role, action))
        {
            return false;
        }

        // 2. Check Workspace Scope
        if (!CheckWorkspaceScope(user, role, networkId))
        {
            return false;
        }

        // 3. Check Layer Permission
        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(idClaim, out var userId))
        {
            return false;
        }

        var perm = await _layerPermissionRepository.GetUserLayerPermissionAsync(userId, featureTypeCode, ct);
        if (perm is not null)
        {
            return action switch
            {
                LayerAction.Read => perm.CanRead,
                LayerAction.Create => perm.CanCreate,
                LayerAction.Update => perm.CanUpdate,
                LayerAction.Delete => perm.CanDelete,
                _ => false
            };
        }

        // If no explicit row exists in user_layer_permissions:
        // Role ceiling already allowed the action.
        return true;
    }

    private static bool CheckRoleCeiling(string role, LayerAction action)
    {
        return role switch
        {
            "ADMIN" => true,
            "EDITOR" => true,
            "VIEWER" => action == LayerAction.Read,
            _ => false
        };
    }

    private static bool CheckWorkspaceScope(ClaimsPrincipal user, string role, Guid networkId)
    {
        // Admin has system-wide workspace scope
        if (role == "ADMIN")
        {
            return true;
        }

        var networkClaims = user.FindAll("network_id")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // If user has no specific network claims assigned, deny access
        if (networkClaims.Count == 0)
        {
            return false;
        }

        return networkClaims.Contains(networkId.ToString());
    }

    public Task<IReadOnlyList<LayerPermissionDto>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        return _layerPermissionRepository.GetUserLayerPermissionsAsync(userId, ct);
    }

    public Task SetUserPermissionsAsync(
        Guid userId,
        IEnumerable<UpdateLayerPermissionRequest> permissions,
        CancellationToken ct = default)
    {
        return _layerPermissionRepository.SetUserLayerPermissionsAsync(userId, permissions, ct);
    }
}
