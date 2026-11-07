using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly byte[] _keyBytes;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(_jwtOptions.Secret) || _jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret must be at least 32 characters long for cryptographic security.");
        }
        _keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.Secret);
    }

    public (string Token, int ExpiresInSeconds) GenerateAccessToken(User user, IReadOnlyList<Guid> accessibleNetworkIds)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString().ToUpperInvariant()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var netId in accessibleNetworkIds)
        {
            claims.Add(new Claim("network_id", netId.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);
        var expiresInSeconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

        return (tokenString, expiresInSeconds);
    }

    public (string RawToken, RefreshToken TokenEntity) GenerateRefreshToken(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        Guid? familyId = null)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var rawToken = Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
            CreatedIp = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        return (rawToken, entity);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_keyBytes),
            ValidateLifetime = false // Here we want to extract claims from expired token
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
