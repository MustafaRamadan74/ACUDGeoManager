using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class LayerPermissionRepository : ILayerPermissionRepository
{
    private readonly string _connectionString;

    public LayerPermissionRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<IReadOnlyList<LayerPermissionDto>> GetUserLayerPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 
                p.id AS Id,
                @userId AS UserId,
                ft.code AS FeatureTypeCode,
                ft.display_name AS FeatureTypeName,
                COALESCE(p.can_read, true) AS CanRead,
                COALESCE(p.can_create, false) AS CanCreate,
                COALESCE(p.can_update, false) AS CanUpdate,
                COALESCE(p.can_delete, false) AS CanDelete
            FROM feature_types ft
            LEFT JOIN user_layer_permissions p 
                ON p.feature_type_code = ft.code AND p.user_id = @userId
            ORDER BY ft.code ASC;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<LayerPermissionDto>(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<LayerPermissionDto?> GetUserLayerPermissionAsync(Guid userId, string featureTypeCode, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 
                p.id AS Id,
                p.user_id AS UserId,
                p.feature_type_code AS FeatureTypeCode,
                ft.display_name AS FeatureTypeName,
                p.can_read AS CanRead,
                p.can_create AS CanCreate,
                p.can_update AS CanUpdate,
                p.can_delete AS CanDelete
            FROM user_layer_permissions p
            JOIN feature_types ft ON ft.code = p.feature_type_code
            WHERE p.user_id = @userId AND UPPER(p.feature_type_code) = UPPER(@featureTypeCode);";

        await using var conn = await CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LayerPermissionDto>(
            new CommandDefinition(sql, new { userId, featureTypeCode }, cancellationToken: ct));
    }

    public async Task SetUserLayerPermissionsAsync(Guid userId, IEnumerable<UpdateLayerPermissionRequest> permissions, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO user_layer_permissions (user_id, feature_type_code, can_read, can_create, can_update, can_delete, updated_at)
            VALUES (@userId, @FeatureTypeCode, @CanRead, @CanCreate, @CanUpdate, @CanDelete, NOW())
            ON CONFLICT (user_id, feature_type_code) DO UPDATE
            SET can_read = EXCLUDED.can_read,
                can_create = EXCLUDED.can_create,
                can_update = EXCLUDED.can_update,
                can_delete = EXCLUDED.can_delete,
                updated_at = NOW();";

        await using var conn = await CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        foreach (var p in permissions)
        {
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                userId,
                p.FeatureTypeCode,
                p.CanRead,
                p.CanCreate,
                p.CanUpdate,
                p.CanDelete
            }, transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }
}
