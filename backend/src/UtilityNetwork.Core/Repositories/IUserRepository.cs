using UtilityNetwork.Core.Entities;

namespace UtilityNetwork.Core.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetUserNetworkAccessAsync(Guid userId, CancellationToken ct = default);
    Task AssignUserNetworksAsync(Guid userId, IEnumerable<Guid> networkIds, CancellationToken ct = default);

    Task IncrementFailedLoginCountAsync(Guid userId, int maxFailures, TimeSpan lockoutDuration, CancellationToken ct = default);
    Task ResetFailedLoginCountAsync(Guid userId, CancellationToken ct = default);

    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(Guid tokenId, Guid? replacedBy, CancellationToken ct = default);
    Task RevokeTokenFamilyAsync(Guid familyId, CancellationToken ct = default);
}
