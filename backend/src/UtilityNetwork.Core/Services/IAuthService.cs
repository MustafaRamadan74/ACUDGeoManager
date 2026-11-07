using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IAuthService
{
    Task<(LoginResponse Response, string RawRefreshToken)> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<(LoginResponse Response, string RawRefreshToken)?> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);

    Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
