using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct = default);
    Task<UserResponse?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserResponse?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetUserNetworksAsync(Guid id, CancellationToken ct = default);
    Task AssignNetworksAsync(Guid id, IReadOnlyList<Guid> networkIds, CancellationToken ct = default);
}
