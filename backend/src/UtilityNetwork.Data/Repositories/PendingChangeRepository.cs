using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class PendingChangeRepository : IPendingChangeRepository
{
    private readonly string _connectionString;

    public PendingChangeRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<Guid> CreatePendingChangeAsync(
        Guid networkId,
        string featureTypeCode,
        string geometryKind,
        Guid? featureId,
        string operation,
        string name,
        string status,
        string? geometryGeoJson,
        string attributesJson,
        bool? isOpen,
        string? flowDirection,
        int? diameterMm,
        string? material,
        int? targetVersion,
        Guid proposerUserId,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO pending_changes (
                id, network_id, feature_type_code, geometry_kind, feature_id, operation, name, status,
                geom, attributes, is_open, flow_direction, diameter_mm, material, target_version,
                proposer_user_id, proposed_at, review_status
            ) VALUES (
                gen_random_uuid(), @networkId, @featureTypeCode, @geometryKind::geometry_kind, @featureId, @operation, @name, @status,
                CASE WHEN @geometryGeoJson IS NOT NULL THEN ST_SetSRID(ST_GeomFromGeoJSON(@geometryGeoJson), 4326) ELSE NULL END,
                COALESCE(NULLIF(@attributesJson, ''), '{}')::jsonb, @isOpen, @flowDirection, @diameterMm, @material, @targetVersion,
                @proposerUserId, now(), 'PENDING'
            ) RETURNING id;";

        return await conn.QuerySingleAsync<Guid>(new CommandDefinition(sql, new
        {
            networkId,
            featureTypeCode,
            geometryKind,
            featureId,
            operation,
            name,
            status,
            geometryGeoJson,
            attributesJson,
            isOpen,
            flowDirection,
            diameterMm,
            material,
            targetVersion,
            proposerUserId
        }, cancellationToken: ct));
    }

    public async Task<PendingChangeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT pc.id, pc.network_id AS NetworkId, pc.feature_type_code AS FeatureTypeCode,
                   pc.geometry_kind::text AS GeometryKind, pc.feature_id AS FeatureId,
                   pc.operation, pc.name, pc.status, ST_AsGeoJSON(pc.geom) AS GeometryGeoJson,
                   pc.attributes::text AS AttributesJson, pc.is_open AS IsOpen,
                   pc.flow_direction AS FlowDirection, pc.diameter_mm AS DiameterMm,
                   pc.material AS Material, pc.target_version AS TargetVersion,
                   pc.proposer_user_id AS ProposerUserId, u_prop.username AS ProposerUsername,
                   pc.proposed_at AS ProposedAt, pc.review_status AS ReviewStatus,
                   pc.reviewer_user_id AS ReviewerUserId, u_rev.username AS ReviewerUsername,
                   pc.reviewed_at AS ReviewedAt, pc.rejection_reason AS RejectionReason
            FROM pending_changes pc
            JOIN users u_prop ON u_prop.id = pc.proposer_user_id
            LEFT JOIN users u_rev ON u_rev.id = pc.reviewer_user_id
            WHERE pc.id = @id;";

        return await conn.QuerySingleOrDefaultAsync<PendingChangeDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PendingChangeDto>> GetPendingQueueAsync(Guid? networkId = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT pc.id, pc.network_id AS NetworkId, pc.feature_type_code AS FeatureTypeCode,
                   pc.geometry_kind::text AS GeometryKind, pc.feature_id AS FeatureId,
                   pc.operation, pc.name, pc.status, ST_AsGeoJSON(pc.geom) AS GeometryGeoJson,
                   pc.attributes::text AS AttributesJson, pc.is_open AS IsOpen,
                   pc.flow_direction AS FlowDirection, pc.diameter_mm AS DiameterMm,
                   pc.material AS Material, pc.target_version AS TargetVersion,
                   pc.proposer_user_id AS ProposerUserId, u_prop.username AS ProposerUsername,
                   pc.proposed_at AS ProposedAt, pc.review_status AS ReviewStatus,
                   pc.reviewer_user_id AS ReviewerUserId, u_rev.username AS ReviewerUsername,
                   pc.reviewed_at AS ReviewedAt, pc.rejection_reason AS RejectionReason
            FROM pending_changes pc
            JOIN users u_prop ON u_prop.id = pc.proposer_user_id
            LEFT JOIN users u_rev ON u_rev.id = pc.reviewer_user_id
            WHERE pc.review_status = 'PENDING'
              AND (@networkId IS NULL OR pc.network_id = @networkId)
            ORDER BY pc.proposed_at ASC;";

        var rows = await conn.QueryAsync<PendingChangeDto>(new CommandDefinition(sql, new { networkId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> MarkApprovedAsync(Guid id, Guid reviewerUserId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE pending_changes
            SET review_status = 'APPROVED', reviewer_user_id = @reviewerUserId, reviewed_at = now()
            WHERE id = @id AND review_status = 'PENDING';";

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { id, reviewerUserId }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> MarkRejectedAsync(Guid id, Guid reviewerUserId, string reason, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE pending_changes
            SET review_status = 'REJECTED', reviewer_user_id = @reviewerUserId, reviewed_at = now(), rejection_reason = @reason
            WHERE id = @id AND review_status = 'PENDING';";

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { id, reviewerUserId, reason }, cancellationToken: ct));
        return affected > 0;
    }
}
