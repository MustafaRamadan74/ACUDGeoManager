using System.Security.Claims;
using UtilityNetwork.Core.Entities;

namespace UtilityNetwork.Core.Services;

public interface ITokenService
{
    (string Token, int ExpiresInSeconds) GenerateAccessToken(User user, IReadOnlyList<Guid> accessibleNetworkIds);
    (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(Guid userId, string? ipAddress, string? userAgent, Guid? familyId = null);
    string HashToken(string rawToken);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
