namespace UtilityNetwork.Core.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    int ExpiresInSeconds,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    IReadOnlyList<Guid> AccessibleNetworkIds,
    string? Department = null);

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string Role,
    IReadOnlyList<Guid>? NetworkIds,
    string? Department = null);

public sealed record UpdateUserRequest(
    string? Email,
    string? Role,
    bool? IsActive,
    IReadOnlyList<Guid>? NetworkIds,
    string? Department = null);

public sealed record AssignNetworksRequest(
    IReadOnlyList<Guid> NetworkIds);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    IReadOnlyList<Guid> AccessibleNetworkIds,
    string? Department = null);
