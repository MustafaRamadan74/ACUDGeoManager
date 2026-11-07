using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed partial class AdminBootstrapService : IAdminBootstrapService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly AdminOptions _adminOptions;
    private readonly ILogger<AdminBootstrapService> _logger;

    public AdminBootstrapService(
        IUserRepository userRepository,
        IPasswordHasherService passwordHasher,
        IOptions<AdminOptions> adminOptions,
        ILogger<AdminBootstrapService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _adminOptions = adminOptions.Value;
        _logger = logger;
    }

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Information,
        Message = "Admin bootstrap: initial administrator '{Username}' successfully created.")]
    private static partial void LogAdminCreated(ILogger logger, string username);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Information,
        Message = "Admin bootstrap: administrator account '{Username}' already exists. Skipping bootstrap.")]
    private static partial void LogAdminAlreadyExists(ILogger logger, string username);

    [LoggerMessage(
        EventId = 303,
        Level = LogLevel.Warning,
        Message = "Admin bootstrap: Admin credentials not provided in configuration. Skipping admin creation.")]
    private static partial void LogAdminConfigMissing(ILogger logger);

    public async Task<bool> EnsureAdminCreatedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_adminOptions.Username) ||
            string.IsNullOrWhiteSpace(_adminOptions.Email) ||
            string.IsNullOrWhiteSpace(_adminOptions.Password))
        {
            LogAdminConfigMissing(_logger);
            return false;
        }

        var existingUser = await _userRepository.GetByUsernameAsync(_adminOptions.Username, ct);
        if (existingUser is not null)
        {
            LogAdminAlreadyExists(_logger, _adminOptions.Username);
            return false;
        }

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = _adminOptions.Username,
            Email = _adminOptions.Email,
            PasswordHash = _passwordHasher.HashPassword(_adminOptions.Password),
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(adminUser, ct);
        LogAdminCreated(_logger, _adminOptions.Username);
        return true;
    }
}
