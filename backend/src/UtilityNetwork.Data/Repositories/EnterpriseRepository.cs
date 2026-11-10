using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class EnterpriseRepository : IEnterpriseRepository
{
    private readonly string _connectionString;

    public EnterpriseRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    // Attachments
    public async Task<IReadOnlyList<FeatureAttachmentDto>> GetAttachmentsByFeatureAsync(Guid featureId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, feature_id AS FeatureId, feature_table AS FeatureTable,
                   file_name AS FileName, content_type AS ContentType,
                   file_size_bytes AS FileSizeBytes, file_path AS FilePath,
                   description AS Description, uploaded_by AS UploadedBy,
                   uploaded_at AS UploadedAt
            FROM feature_attachments
            WHERE feature_id = @featureId
            ORDER BY uploaded_at DESC;";

        var rows = await conn.QueryAsync<FeatureAttachmentDto>(new CommandDefinition(sql, new { featureId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FeatureAttachmentDto?> GetAttachmentByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, feature_id AS FeatureId, feature_table AS FeatureTable,
                   file_name AS FileName, content_type AS ContentType,
                   file_size_bytes AS FileSizeBytes, file_path AS FilePath,
                   description AS Description, uploaded_by AS UploadedBy,
                   uploaded_at AS UploadedAt
            FROM feature_attachments
            WHERE id = @id;";

        return await conn.QuerySingleOrDefaultAsync<FeatureAttachmentDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<FeatureAttachmentDto> AddAttachmentAsync(FeatureAttachmentDto attachment, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO feature_attachments (id, feature_id, feature_table, file_name, content_type, file_size_bytes, file_path, description, uploaded_by, uploaded_at)
            VALUES (@Id, @FeatureId, @FeatureTable, @FileName, @ContentType, @FileSizeBytes, @FilePath, @Description, @UploadedBy, @UploadedAt);";

        await conn.ExecuteAsync(new CommandDefinition(sql, attachment, cancellationToken: ct));
        return attachment;
    }

    public async Task<bool> DeleteAttachmentAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"DELETE FROM feature_attachments WHERE id = @id;";
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return affected > 0;
    }

    // Relationships
    public async Task<IReadOnlyList<FeatureRelationshipDto>> GetRelationshipsByFeatureAsync(Guid featureId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT 
                r.id,
                r.source_feature_id AS SourceFeatureId,
                r.source_table AS SourceTable,
                r.target_feature_id AS TargetFeatureId,
                r.target_table AS TargetTable,
                r.relationship_type AS RelationshipType,
                r.description,
                r.created_by AS CreatedBy,
                r.created_at AS CreatedAt,
                COALESCE(sp.name, sl.name, spoly.name) AS SourceFeatureName,
                COALESCE(tp.name, tl.name, tpoly.name) AS TargetFeatureName,
                COALESCE(sp.feature_type_code, sl.feature_type_code, spoly.feature_type_code) AS SourceFeatureTypeCode,
                COALESCE(tp.feature_type_code, tl.feature_type_code, tpoly.feature_type_code) AS TargetFeatureTypeCode
            FROM feature_relationships r
            LEFT JOIN points sp ON r.source_table = 'points' AND r.source_feature_id = sp.id
            LEFT JOIN lines sl ON r.source_table = 'lines' AND r.source_feature_id = sl.id
            LEFT JOIN polygons spoly ON r.source_table = 'polygons' AND r.source_feature_id = spoly.id
            LEFT JOIN points tp ON r.target_table = 'points' AND r.target_feature_id = tp.id
            LEFT JOIN lines tl ON r.target_table = 'lines' AND r.target_feature_id = tl.id
            LEFT JOIN polygons tpoly ON r.target_table = 'polygons' AND r.target_feature_id = tpoly.id
            WHERE r.source_feature_id = @featureId OR r.target_feature_id = @featureId
            ORDER BY r.created_at DESC;";

        var rows = await conn.QueryAsync<FeatureRelationshipDto>(new CommandDefinition(sql, new { featureId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FeatureRelationshipDto> AddRelationshipAsync(FeatureRelationshipDto relationship, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO feature_relationships (id, source_feature_id, source_table, target_feature_id, target_table, relationship_type, description, created_by, created_at)
            VALUES (@Id, @SourceFeatureId, @SourceTable, @TargetFeatureId, @TargetTable, @RelationshipType, @Description, @CreatedBy, @CreatedAt);";

        await conn.ExecuteAsync(new CommandDefinition(sql, relationship, cancellationToken: ct));
        return relationship;
    }

    public async Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"DELETE FROM feature_relationships WHERE id = @id;";
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return affected > 0;
    }

    // Edit Locking
    public async Task<FeatureLockDto?> GetLockAsync(Guid featureId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT 
                feature_id AS FeatureId,
                feature_table AS FeatureTable,
                locked_by_user_id AS LockedByUserId,
                locked_by_username AS LockedByUsername,
                locked_at AS LockedAt,
                expires_at AS ExpiresAt,
                client_session_id AS ClientSessionId
            FROM feature_edit_locks
            WHERE feature_id = @featureId AND expires_at > now();";

        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { featureId }, cancellationToken: ct));
        if (row is null)
        {
            return null;
        }

        Guid lockedUserId = row.lockedbyuserid;
        return new FeatureLockDto(
            row.featureid,
            row.featuretable,
            lockedUserId,
            row.lockedbyusername,
            (DateTimeOffset)row.lockedat,
            (DateTimeOffset)row.expiresat,
            row.clientsessionid,
            IsLocked: true,
            IsLockedByCurrentUser: currentUserId.HasValue && lockedUserId == currentUserId.Value
        );
    }

    public async Task<(bool Success, FeatureLockDto LockInfo)> TryAcquireLockAsync(
        Guid featureId,
        string featureTable,
        Guid userId,
        string username,
        int ttlMinutes,
        string? clientSessionId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. Purge expired locks for this feature
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM feature_edit_locks WHERE feature_id = @featureId AND expires_at <= now();",
            new { featureId },
            cancellationToken: ct));

        // 2. Check if locked by someone else
        var existing = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            @"SELECT feature_id, feature_table, locked_by_user_id, locked_by_username, locked_at, expires_at, client_session_id 
              FROM feature_edit_locks 
              WHERE feature_id = @featureId AND expires_at > now();",
            new { featureId },
            cancellationToken: ct));

        if (existing is not null)
        {
            Guid activeUserId = existing.locked_by_user_id;
            if (activeUserId != userId)
            {
                // Locked by someone else
                return (false, new FeatureLockDto(
                    existing.feature_id,
                    existing.feature_table,
                    activeUserId,
                    existing.locked_by_username,
                    (DateTimeOffset)existing.locked_at,
                    (DateTimeOffset)existing.expires_at,
                    existing.client_session_id,
                    IsLocked: true,
                    IsLockedByCurrentUser: false
                ));
            }

            // Same user renewing lock
            const string updateSql = @"
                UPDATE feature_edit_locks
                SET expires_at = now() + (@ttlMinutes || ' minutes')::interval,
                    client_session_id = COALESCE(@clientSessionId, client_session_id)
                WHERE feature_id = @featureId
                RETURNING feature_id, feature_table, locked_by_user_id, locked_by_username, locked_at, expires_at, client_session_id;";

            var updated = await conn.QuerySingleAsync<dynamic>(new CommandDefinition(updateSql, new { featureId, ttlMinutes, clientSessionId }, cancellationToken: ct));
            return (true, new FeatureLockDto(
                updated.feature_id,
                updated.feature_table,
                updated.locked_by_user_id,
                updated.locked_by_username,
                (DateTimeOffset)updated.locked_at,
                (DateTimeOffset)updated.expires_at,
                updated.client_session_id,
                IsLocked: true,
                IsLockedByCurrentUser: true
            ));
        }

        // 3. Insert fresh lock
        const string insertSql = @"
            INSERT INTO feature_edit_locks (feature_id, feature_table, locked_by_user_id, locked_by_username, locked_at, expires_at, client_session_id)
            VALUES (@featureId, @featureTable, @userId, @username, now(), now() + (@ttlMinutes || ' minutes')::interval, @clientSessionId)
            ON CONFLICT (feature_id) DO UPDATE
            SET locked_by_user_id = EXCLUDED.locked_by_user_id,
                locked_by_username = EXCLUDED.locked_by_username,
                locked_at = EXCLUDED.locked_at,
                expires_at = EXCLUDED.expires_at,
                client_session_id = EXCLUDED.client_session_id
            RETURNING feature_id, feature_table, locked_by_user_id, locked_by_username, locked_at, expires_at, client_session_id;";

        var inserted = await conn.QuerySingleAsync<dynamic>(new CommandDefinition(insertSql, new { featureId, featureTable, userId, username, ttlMinutes, clientSessionId }, cancellationToken: ct));
        return (true, new FeatureLockDto(
            inserted.feature_id,
            inserted.feature_table,
            inserted.locked_by_user_id,
            inserted.locked_by_username,
            (DateTimeOffset)inserted.locked_at,
            (DateTimeOffset)inserted.expires_at,
            inserted.client_session_id,
            IsLocked: true,
            IsLockedByCurrentUser: true
        ));
    }

    public async Task<bool> ReleaseLockAsync(Guid featureId, Guid userId, bool isAdmin = false, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            DELETE FROM feature_edit_locks
            WHERE feature_id = @featureId AND (@isAdmin = true OR locked_by_user_id = @userId);";

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { featureId, userId, isAdmin }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> RefreshLockAsync(Guid featureId, Guid userId, int ttlMinutes, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE feature_edit_locks
            SET expires_at = now() + (@ttlMinutes || ' minutes')::interval
            WHERE feature_id = @featureId AND locked_by_user_id = @userId AND expires_at > now();";

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { featureId, userId, ttlMinutes }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task ClearExpiredLocksAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM feature_edit_locks WHERE expires_at <= now();", cancellationToken: ct));
    }

    // Data Quality
    public async Task<IReadOnlyList<LayerDataQualityDto>> GetDataQualityReportAsync(Guid? networkId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT 
                feature_type_code AS FeatureTypeCode,
                display_name AS DisplayName,
                geometry_kind AS GeometryKind,
                total_count AS TotalCount,
                valid_geom_count AS ValidGeomCount,
                open_issues_count AS OpenIssuesCount,
                completeness_pct AS CompletenessPct,
                attachments_count AS AttachmentsCount,
                relationships_count AS RelationshipsCount
            FROM fn_get_data_quality_dashboard(@networkId);";

        var rows = await conn.QueryAsync<LayerDataQualityDto>(new CommandDefinition(sql, new { networkId }, cancellationToken: ct));
        return rows.ToList();
    }
}
