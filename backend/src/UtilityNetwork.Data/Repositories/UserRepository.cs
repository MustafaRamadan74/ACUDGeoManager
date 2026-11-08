using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, username, email, password_hash AS PasswordHash, 
                   role::text AS Role, is_active AS IsActive, failed_login_count AS FailedLoginCount, 
                   department AS Department,
                   locked_until AS LockedUntil, last_login_at AS LastLoginAt, created_at AS CreatedAt
            FROM users WHERE id = @id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var dto = await conn.QuerySingleOrDefaultAsync<UserDbDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return dto?.ToUser();
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, username, email, password_hash AS PasswordHash, 
                   role::text AS Role, is_active AS IsActive, failed_login_count AS FailedLoginCount, 
                   department AS Department,
                   locked_until AS LockedUntil, last_login_at AS LastLoginAt, created_at AS CreatedAt
            FROM users WHERE lower(username) = lower(@username);";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var dto = await conn.QuerySingleOrDefaultAsync<UserDbDto>(new CommandDefinition(sql, new { username }, cancellationToken: ct));
        return dto?.ToUser();
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, username, email, password_hash AS PasswordHash, 
                   role::text AS Role, is_active AS IsActive, failed_login_count AS FailedLoginCount, 
                   department AS Department,
                   locked_until AS LockedUntil, last_login_at AS LastLoginAt, created_at AS CreatedAt
            FROM users WHERE lower(email) = lower(@email);";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var dto = await conn.QuerySingleOrDefaultAsync<UserDbDto>(new CommandDefinition(sql, new { email }, cancellationToken: ct));
        return dto?.ToUser();
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, username, email, password_hash AS PasswordHash, 
                   role::text AS Role, is_active AS IsActive, failed_login_count AS FailedLoginCount, 
                   department AS Department,
                   locked_until AS LockedUntil, last_login_at AS LastLoginAt, created_at AS CreatedAt
            FROM users ORDER BY created_at DESC;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var dtoks = await conn.QueryAsync<UserDbDto>(new CommandDefinition(sql, cancellationToken: ct));
        return dtoks.Select(d => d.ToUser()).ToList();
    }

    public async Task<Guid> CreateAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO users (id, username, email, password_hash, role, is_active, failed_login_count, department, created_at)
            VALUES (@Id, @Username, @Email, @PasswordHash, @Role::user_role, @IsActive, @FailedLoginCount, @Department, @CreatedAt)
            RETURNING id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            user.Id,
            user.Username,
            user.Email,
            user.PasswordHash,
            Role = user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            user.FailedLoginCount,
            user.Department,
            user.CreatedAt
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users 
            SET email = @Email, role = @Role::user_role, is_active = @IsActive, department = @Department, last_login_at = @LastLoginAt
            WHERE id = @Id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            user.Id,
            user.Email,
            Role = user.Role.ToString().ToUpperInvariant(),
            user.IsActive,
            user.Department,
            user.LastLoginAt
        }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM users WHERE id = @id;";
        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Guid>> GetUserNetworkAccessAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = "SELECT network_id FROM user_network_access WHERE user_id = @userId;";
        await using var conn = await CreateOpenConnectionAsync(ct);
        var result = await conn.QueryAsync<Guid>(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
        return result.ToList();
    }

    public async Task AssignUserNetworksAsync(Guid userId, IEnumerable<Guid> networkIds, CancellationToken ct = default)
    {
        await using var conn = await CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string deleteSql = "DELETE FROM user_network_access WHERE user_id = @userId;";
        await conn.ExecuteAsync(new CommandDefinition(deleteSql, new { userId }, transaction: tx, cancellationToken: ct));

        const string insertSql = "INSERT INTO user_network_access (user_id, network_id) VALUES (@userId, @networkId) ON CONFLICT DO NOTHING;";
        foreach (var netId in networkIds)
        {
            await conn.ExecuteAsync(new CommandDefinition(insertSql, new { userId, networkId = netId }, transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }

    public async Task IncrementFailedLoginCountAsync(Guid userId, int maxFailures, TimeSpan lockoutDuration, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE users 
            SET failed_login_count = failed_login_count + 1,
                locked_until = CASE 
                    WHEN failed_login_count + 1 >= @maxFailures 
                    THEN now() + (@lockoutSeconds || ' seconds')::interval 
                    ELSE NULL 
                END
            WHERE id = @userId;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new 
        { 
            userId, 
            maxFailures, 
            lockoutSeconds = (int)lockoutDuration.TotalSeconds 
        }, cancellationToken: ct));
    }

    public async Task ResetFailedLoginCountAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = "UPDATE users SET failed_login_count = 0, locked_until = NULL, last_login_at = now() WHERE id = @userId;";
        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
    }

    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO refresh_tokens (id, user_id, token_hash, family_id, expires_at, revoked_at, replaced_by, created_ip, user_agent, created_at)
            VALUES (@Id, @UserId, @TokenHash, @FamilyId, @ExpiresAt, @RevokedAt, @ReplacedBy, @CreatedIp, @UserAgent, @CreatedAt);";

        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, token, cancellationToken: ct));
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, user_id AS UserId, token_hash AS TokenHash, family_id AS FamilyId, 
                   expires_at AS ExpiresAt, revoked_at AS RevokedAt, replaced_by AS ReplacedBy, 
                   created_ip AS CreatedIp, user_agent AS UserAgent, created_at AS CreatedAt
            FROM refresh_tokens WHERE token_hash = @tokenHash;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(new CommandDefinition(sql, new { tokenHash }, cancellationToken: ct));
    }

    public async Task RevokeRefreshTokenAsync(Guid tokenId, Guid? replacedBy, CancellationToken ct = default)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = now(), replaced_by = @replacedBy WHERE id = @tokenId;";
        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { tokenId, replacedBy }, cancellationToken: ct));
    }

    public async Task RevokeTokenFamilyAsync(Guid familyId, CancellationToken ct = default)
    {
        const string sql = "UPDATE refresh_tokens SET revoked_at = now() WHERE family_id = @familyId AND revoked_at IS NULL;";
        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { familyId }, cancellationToken: ct));
    }

    private sealed record UserDbDto(
        Guid Id,
        string Username,
        string Email,
        string PasswordHash,
        string Role,
        bool IsActive,
        int FailedLoginCount,
        string? Department,
        DateTime? LockedUntil,
        DateTime? LastLoginAt,
        DateTime CreatedAt)
    {
        public User ToUser() => new()
        {
            Id = Id,
            Username = Username,
            Email = Email,
            PasswordHash = PasswordHash,
            Role = Enum.TryParse<UserRole>(Role, true, out var r) ? r : UserRole.Viewer,
            IsActive = IsActive,
            FailedLoginCount = FailedLoginCount,
            Department = Department,
            LockedUntil = LockedUntil,
            LastLoginAt = LastLoginAt,
            CreatedAt = CreatedAt
        };
    }
}
