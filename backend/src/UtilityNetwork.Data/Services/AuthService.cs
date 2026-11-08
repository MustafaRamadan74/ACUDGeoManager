using System.Security.Authentication;
using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, ct);
        if (user is null)
        {
            throw new InvalidCredentialException("Invalid username or password.");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            throw new AuthenticationException($"Account is temporarily locked due to excessive failed attempts. Please try again in {remaining} minute(s).");
        }

        if (!user.IsActive)
        {
            throw new InvalidCredentialException("Invalid username or password.");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            await _userRepository.IncrementFailedLoginCountAsync(user.Id, MaxFailedAttempts, LockoutDuration, ct);
            throw new InvalidCredentialException("Invalid username or password.");
        }

        await _userRepository.ResetFailedLoginCountAsync(user.Id, ct);

        var networks = await _userRepository.GetUserNetworkAccessAsync(user.Id, ct);
        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, networks);
        var (rawRefreshToken, tokenEntity) = _tokenService.GenerateRefreshToken(user.Id, ipAddress, userAgent);

        await _userRepository.SaveRefreshTokenAsync(tokenEntity, ct);

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            networks,
            user.Department);

        return (new LoginResponse(accessToken, expiresInSeconds, userDto), rawRefreshToken);
    }

    public async Task<(LoginResponse Response, string RawRefreshToken)?> RefreshTokenAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var tokenHash = _tokenService.HashToken(rawRefreshToken);
        var token = await _userRepository.GetRefreshTokenByHashAsync(tokenHash, ct);
        if (token is null)
        {
            return null;
        }

        // Reuse detection: If token is already revoked, compromise detected -> revoke entire family
        if (token.RevokedAt.HasValue)
        {
            await _userRepository.RevokeTokenFamilyAsync(token.FamilyId, ct);
            return null;
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(token.UserId, ct);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        // Rotate token
        var (newRawToken, newTokenEntity) = _tokenService.GenerateRefreshToken(
            user.Id,
            ipAddress,
            userAgent,
            familyId: token.FamilyId);

        await _userRepository.RevokeRefreshTokenAsync(token.Id, newTokenEntity.Id, ct);
        await _userRepository.SaveRefreshTokenAsync(newTokenEntity, ct);

        var networks = await _userRepository.GetUserNetworkAccessAsync(user.Id, ct);
        var (accessToken, expiresInSeconds) = _tokenService.GenerateAccessToken(user, networks);

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            networks,
            user.Department);

        return (new LoginResponse(accessToken, expiresInSeconds, userDto), newRawToken);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var tokenHash = _tokenService.HashToken(rawRefreshToken);
        var token = await _userRepository.GetRefreshTokenByHashAsync(tokenHash, ct);
        if (token is not null && !token.RevokedAt.HasValue)
        {
            await _userRepository.RevokeRefreshTokenAsync(token.Id, null, ct);
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        var networks = await _userRepository.GetUserNetworkAccessAsync(userId, ct);
        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            networks);
    }
}
