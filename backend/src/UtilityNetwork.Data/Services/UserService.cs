using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILayerPermissionRepository _layerPermissionRepository;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        ILayerPermissionRepository layerPermissionRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _layerPermissionRepository = layerPermissionRepository;
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        var responses = new List<UserResponse>(users.Count);

        foreach (var u in users)
        {
            var networks = await _userRepository.GetUserNetworkAccessAsync(u.Id, ct);
            responses.Add(new UserResponse(
                u.Id,
                u.Username,
                u.Email,
                u.Role.ToString().ToUpperInvariant(),
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt,
                networks,
                u.Department));
        }

        return responses;
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _userRepository.GetByIdAsync(id, ct);
        if (u is null)
        {
            return null;
        }

        var networks = await _userRepository.GetUserNetworkAccessAsync(u.Id, ct);
        return new UserResponse(
            u.Id,
            u.Username,
            u.Email,
            u.Role.ToString().ToUpperInvariant(),
            u.IsActive,
            u.LastLoginAt,
            u.CreatedAt,
            networks,
            u.Department);
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var role = Enum.TryParse<UserRole>(request.Role, true, out var parsedRole) ? parsedRole : UserRole.Viewer;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = role,
            Department = request.Department,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var id = await _userRepository.CreateAsync(user, ct);

        if (request.NetworkIds is not null && request.NetworkIds.Count > 0)
        {
            await _userRepository.AssignUserNetworksAsync(id, request.NetworkIds, ct);
        }

        var networks = await _userRepository.GetUserNetworkAccessAsync(id, ct);
        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            null,
            user.CreatedAt,
            networks,
            user.Department);
    }

    public async Task<UserResponse?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var existing = await _userRepository.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return null;
        }

        var role = existing.Role;
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var r))
        {
            role = r;
        }

        var updated = existing with
        {
            Email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email : existing.Email,
            Role = role,
            Department = request.Department ?? existing.Department,
            IsActive = request.IsActive ?? existing.IsActive
        };

        await _userRepository.UpdateAsync(updated, ct);

        if (request.NetworkIds is not null)
        {
            await _userRepository.AssignUserNetworksAsync(id, request.NetworkIds, ct);
        }

        var networks = await _userRepository.GetUserNetworkAccessAsync(id, ct);
        return new UserResponse(
            updated.Id,
            updated.Username,
            updated.Email,
            updated.Role.ToString().ToUpperInvariant(),
            updated.IsActive,
            updated.LastLoginAt,
            updated.CreatedAt,
            networks,
            updated.Department);
    }

    public async Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _userRepository.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return false;
        }

        await _userRepository.DeleteAsync(id, ct);
        return true;
    }

    public Task<IReadOnlyList<Guid>> GetUserNetworksAsync(Guid id, CancellationToken ct = default)
    {
        return _userRepository.GetUserNetworkAccessAsync(id, ct);
    }

    public Task AssignNetworksAsync(Guid id, IReadOnlyList<Guid> networkIds, CancellationToken ct = default)
    {
        return _userRepository.AssignUserNetworksAsync(id, networkIds, ct);
    }

    public Task<IReadOnlyList<LayerPermissionDto>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        return _layerPermissionRepository.GetUserLayerPermissionsAsync(userId, ct);
    }

    public Task SetUserPermissionsAsync(Guid userId, IEnumerable<UpdateLayerPermissionRequest> permissions, CancellationToken ct = default)
    {
        return _layerPermissionRepository.SetUserLayerPermissionsAsync(userId, permissions, ct);
    }
}
