using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class UndoRedoRepository : IUndoRedoRepository
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "points", "lines", "polygons"
    };

    private readonly string _connectionString;

    public UndoRedoRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<UndoRedoStackDto> GetUserStackAsync(Guid userId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var undoStack = await GetStackByStatusAsync(connection, userId, "APPLIED", 30, ct);
        var redoStack = await GetStackByStatusAsync(connection, userId, "UNDONE", 30, ct);

        return new UndoRedoStackDto(
            CanUndo: undoStack.Count > 0,
            CanRedo: redoStack.Count > 0,
            UndoStack: undoStack,
            RedoStack: redoStack);
    }

    private static async Task<IReadOnlyList<ChangeSetSummaryDto>> GetStackByStatusAsync(
        NpgsqlConnection connection,
        Guid userId,
        string status,
        int limit,
        CancellationToken ct)
    {
        var orderBy = status == "UNDONE"
            ? "ORDER BY cs.undone_at DESC NULLS LAST, cs.created_at DESC"
            : "ORDER BY cs.created_at DESC";

        var sql = $@"
            SELECT 
                cs.id,
                cs.user_id,
                cs.description,
                cs.status::text AS status,
                cs.created_at,
                cs.undone_at
            FROM change_sets cs
            WHERE cs.user_id = @userId AND cs.status = @status::change_set_status
            {orderBy}
            LIMIT @limit;";

        var changeSets = (await connection.QueryAsync<ChangeSetRow>(
            new CommandDefinition(sql, new { userId, status, limit }, cancellationToken: ct))).ToList();

        if (changeSets.Count == 0)
        {
            return Array.Empty<ChangeSetSummaryDto>();
        }

        var csIds = changeSets.Select(c => c.id).ToArray();
        const string auditSql = @"
            SELECT 
                change_set_id,
                record_id,
                table_name,
                action::text AS action,
                old_data->>'name' AS old_name,
                new_data->>'name' AS new_name
            FROM audit_log
            WHERE change_set_id = ANY(@csIds)
            ORDER BY id ASC;";

        var auditRows = (await connection.QueryAsync<AuditSummaryRow>(
            new CommandDefinition(auditSql, new { csIds }, cancellationToken: ct))).ToList();

        var auditByCs = auditRows.GroupBy(a => a.change_set_id).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ChangeSetSummaryDto>(changeSets.Count);
        foreach (var cs in changeSets)
        {
            var actions = auditByCs.TryGetValue(cs.id, out var list) ? list : new List<AuditSummaryRow>();
            var affectedFeatures = actions
                .Select(a => $"{a.new_name ?? a.old_name ?? a.record_id.ToString()} ({a.action})")
                .Distinct()
                .Take(5)
                .ToList();

            result.Add(new ChangeSetSummaryDto(
                Id: cs.id,
                UserId: cs.user_id,
                Description: string.IsNullOrWhiteSpace(cs.description) ? "Network edit" : cs.description,
                Status: cs.status,
                CreatedAt: cs.created_at,
                UndoneAt: cs.undone_at,
                ActionsCount: actions.Count,
                AffectedFeatures: affectedFeatures));
        }

        return result;
    }

    public async Task<UndoResultDto> UndoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // 1. Fetch latest APPLIED change set for user
        const string findLatestSql = @"
            SELECT id, description, created_at, status::text AS status
            FROM change_sets
            WHERE user_id = @userId AND status = 'APPLIED'::change_set_status
            ORDER BY created_at DESC
            LIMIT 1
            FOR UPDATE;";

        var changeSet = await connection.QuerySingleOrDefaultAsync<ChangeSetRow>(
            new CommandDefinition(findLatestSql, new { userId }, transaction: transaction, cancellationToken: ct));

        if (changeSet == null)
        {
            throw new InvalidOperationException("No actions available to undo.");
        }

        // 2. Fetch all audit rows for this change set in reverse chronological order
        const string fetchAuditSql = @"
            SELECT id, table_name, record_id, action::text, old_data::text, new_data::text
            FROM audit_log
            WHERE change_set_id = @changeSetId
            ORDER BY id DESC;";

        var auditRows = (await connection.QueryAsync<AuditLogRow>(
            new CommandDefinition(fetchAuditSql, new { changeSetId = changeSet.id }, transaction: transaction, cancellationToken: ct))).ToList();

        if (auditRows.Count == 0)
        {
            throw new InvalidOperationException("No audit records found for the latest change set.");
        }

        // 3. Conflict detection
        var conflicts = new List<ConflictingFeatureDto>();
        foreach (var row in auditRows)
        {
            if (!AllowedTables.Contains(row.table_name))
            {
                throw new InvalidOperationException($"Unsupported table in change set: {row.table_name}");
            }

            var current = await GetCurrentFeatureStateAsync(connection, transaction, row.table_name, row.record_id, ct);
            var action = row.action.ToUpperInvariant();

            if (action == "INSERT")
            {
                // Action created feature; current feature must exist with matching version
                var expectedVersion = ExtractVersion(row.new_data);
                var featureName = ExtractName(row.new_data) ?? row.record_id.ToString();

                if (current == null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, featureName, expectedVersion, null, "Feature was deleted after creation"));
                }
                else if (current.Version != expectedVersion)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature was modified after creation"));
                }
            }
            else if (action == "UPDATE")
            {
                // Action updated feature; current feature must exist with matching version (from new_data)
                var expectedVersion = ExtractVersion(row.new_data);
                var featureName = ExtractName(row.new_data) ?? ExtractName(row.old_data) ?? row.record_id.ToString();

                if (current == null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, featureName, expectedVersion, null, "Feature was deleted after update"));
                }
                else if (current.Version != expectedVersion)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature version mismatch"));
                }
            }
            else if (action == "DELETE")
            {
                // Action deleted feature; feature should NOT exist currently
                var expectedVersion = ExtractVersion(row.old_data);
                var featureName = ExtractName(row.old_data) ?? row.record_id.ToString();

                if (current != null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature already exists"));
                }
            }
        }

        if (conflicts.Count > 0)
        {
            await transaction.RollbackAsync(ct);
            throw new UndoRedoConflictException(conflicts);
        }

        // 4. Set session configs to reason = UNDO
        await SetSessionConfigAsync(connection, transaction, userId, username, role, "UNDO", ct);

        // 5. Invert actions
        foreach (var row in auditRows)
        {
            var action = row.action.ToUpperInvariant();
            if (action == "INSERT")
            {
                // Invert INSERT -> DELETE
                var deleteSql = $"DELETE FROM {row.table_name} WHERE id = @recordId;";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { recordId = row.record_id }, transaction: transaction, cancellationToken: ct));
            }
            else if (action == "UPDATE")
            {
                // Invert UPDATE -> restore old_data
                await RestoreFeatureUpdateAsync(connection, transaction, row.table_name, row.record_id, row.old_data!, username, ct);
            }
            else if (action == "DELETE")
            {
                // Invert DELETE -> re-insert old_data
                await RestoreFeatureInsertAsync(connection, transaction, row.table_name, row.old_data!, username, ct);
            }
        }

        // 6. Update change_sets status = UNDONE, undone_at = now()
        const string updateChangeSetSql = @"
            UPDATE change_sets
            SET status = 'UNDONE'::change_set_status, undone_at = now()
            WHERE id = @changeSetId;";

        await connection.ExecuteAsync(new CommandDefinition(updateChangeSetSql, new { changeSetId = changeSet.id }, transaction: transaction, cancellationToken: ct));

        await transaction.CommitAsync(ct);

        return new UndoResultDto(
            ChangeSetId: changeSet.id,
            Description: changeSet.description,
            RevertedActionsCount: auditRows.Count,
            Status: "UNDONE");
    }

    public async Task<RedoResultDto> RedoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        // 1. Fetch latest UNDONE change set for user
        const string findLatestSql = @"
            SELECT id, description, created_at, status::text AS status
            FROM change_sets
            WHERE user_id = @userId AND status = 'UNDONE'::change_set_status
            ORDER BY undone_at DESC NULLS LAST, created_at DESC
            LIMIT 1
            FOR UPDATE;";

        var changeSet = await connection.QuerySingleOrDefaultAsync<ChangeSetRow>(
            new CommandDefinition(findLatestSql, new { userId }, transaction: transaction, cancellationToken: ct));

        if (changeSet == null)
        {
            throw new InvalidOperationException("No actions available to redo.");
        }

        // 2. Fetch all audit rows for this change set in forward chronological order
        const string fetchAuditSql = @"
            SELECT id, table_name, record_id, action::text, old_data::text, new_data::text
            FROM audit_log
            WHERE change_set_id = @changeSetId
            ORDER BY id ASC;";

        var auditRows = (await connection.QueryAsync<AuditLogRow>(
            new CommandDefinition(fetchAuditSql, new { changeSetId = changeSet.id }, transaction: transaction, cancellationToken: ct))).ToList();

        if (auditRows.Count == 0)
        {
            throw new InvalidOperationException("No audit records found for the undone change set.");
        }

        // 3. Conflict detection on Redo
        var conflicts = new List<ConflictingFeatureDto>();
        foreach (var row in auditRows)
        {
            if (!AllowedTables.Contains(row.table_name))
            {
                throw new InvalidOperationException($"Unsupported table in change set: {row.table_name}");
            }

            var current = await GetCurrentFeatureStateAsync(connection, transaction, row.table_name, row.record_id, ct);
            var action = row.action.ToUpperInvariant();

            if (action == "INSERT")
            {
                // Action originally created feature; Undo deleted it, so it should not exist currently
                var expectedVersion = ExtractVersion(row.new_data);
                var featureName = ExtractName(row.new_data) ?? row.record_id.ToString();

                if (current != null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature already exists"));
                }
            }
            else if (action == "UPDATE")
            {
                // Action originally updated feature; Undo restored it to old_data; current version must match old_data version
                var expectedVersion = ExtractVersion(row.old_data);
                var featureName = ExtractName(row.old_data) ?? ExtractName(row.new_data) ?? row.record_id.ToString();

                if (current == null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, featureName, expectedVersion, null, "Feature was deleted"));
                }
                else if (current.Version != expectedVersion)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature version mismatch before redo"));
                }
            }
            else if (action == "DELETE")
            {
                // Action originally deleted feature; Undo re-created it with old_data; current version must match old_data version
                var expectedVersion = ExtractVersion(row.old_data);
                var featureName = ExtractName(row.old_data) ?? row.record_id.ToString();

                if (current == null)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, featureName, expectedVersion, null, "Feature was already deleted"));
                }
                else if (current.Version != expectedVersion)
                {
                    conflicts.Add(new ConflictingFeatureDto(row.table_name, row.record_id, current.Name, expectedVersion, current.Version, "Feature version mismatch before redo"));
                }
            }
        }

        if (conflicts.Count > 0)
        {
            await transaction.RollbackAsync(ct);
            throw new UndoRedoConflictException(conflicts);
        }

        // 4. Set session configs to reason = REDO
        await SetSessionConfigAsync(connection, transaction, userId, username, role, "REDO", ct);

        // 5. Re-apply actions
        foreach (var row in auditRows)
        {
            var action = row.action.ToUpperInvariant();
            if (action == "INSERT")
            {
                // Re-apply INSERT -> insert new_data
                await RestoreFeatureInsertAsync(connection, transaction, row.table_name, row.new_data!, username, ct);
            }
            else if (action == "UPDATE")
            {
                // Re-apply UPDATE -> update to new_data
                await RestoreFeatureUpdateAsync(connection, transaction, row.table_name, row.record_id, row.new_data!, username, ct);
            }
            else if (action == "DELETE")
            {
                // Re-apply DELETE -> delete record
                var deleteSql = $"DELETE FROM {row.table_name} WHERE id = @recordId;";
                await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { recordId = row.record_id }, transaction: transaction, cancellationToken: ct));
            }
        }

        // 6. Update change_sets status = APPLIED, undone_at = NULL
        const string updateChangeSetSql = @"
            UPDATE change_sets
            SET status = 'APPLIED'::change_set_status, undone_at = NULL
            WHERE id = @changeSetId;";

        await connection.ExecuteAsync(new CommandDefinition(updateChangeSetSql, new { changeSetId = changeSet.id }, transaction: transaction, cancellationToken: ct));

        await transaction.CommitAsync(ct);

        return new RedoResultDto(
            ChangeSetId: changeSet.id,
            Description: changeSet.description,
            ReappliedActionsCount: auditRows.Count,
            Status: "APPLIED");
    }

    private static async Task SetSessionConfigAsync(
        NpgsqlConnection connection,
        DbTransaction transaction,
        Guid userId,
        string username,
        string role,
        string reason,
        CancellationToken ct)
    {
        const string setConfigSql = @"
            SELECT set_config('app.change_set_id', '', true),
                   set_config('app.user_id', @userId, true),
                   set_config('app.username', @username, true),
                   set_config('app.role', @role, true),
                   set_config('app.reason', @reason, true);";

        await connection.ExecuteAsync(new CommandDefinition(
            setConfigSql,
            new { userId = userId.ToString(), username, role, reason },
            transaction: transaction,
            cancellationToken: ct));
    }

    private static async Task<FeatureStateRow?> GetCurrentFeatureStateAsync(
        NpgsqlConnection connection,
        DbTransaction transaction,
        string tableName,
        Guid recordId,
        CancellationToken ct)
    {
        var sql = $"SELECT id, name, version FROM {tableName} WHERE id = @recordId;";
        return await connection.QuerySingleOrDefaultAsync<FeatureStateRow>(
            new CommandDefinition(sql, new { recordId }, transaction: transaction, cancellationToken: ct));
    }

    private static async Task RestoreFeatureInsertAsync(
        NpgsqlConnection connection,
        DbTransaction transaction,
        string tableName,
        string json,
        string username,
        CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetGuid();
        var networkId = root.GetProperty("network_id").GetGuid();
        var featureTypeCode = root.GetProperty("feature_type_code").GetString()!;
        var name = root.GetProperty("name").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var attributes = root.GetProperty("attributes").GetRawText();
        var geomElem = root.GetProperty("geom");
        var geomJson = geomElem.ValueKind == JsonValueKind.String ? geomElem.GetString()! : geomElem.GetRawText();
        var version = root.GetProperty("version").GetInt32();
        var createdBy = root.TryGetProperty("created_by", out var cb) ? cb.GetString() ?? username : username;

        if (string.Equals(tableName, "points", StringComparison.OrdinalIgnoreCase))
        {
            bool? isOpen = root.TryGetProperty("is_open", out var op) && op.ValueKind != JsonValueKind.Null ? op.GetBoolean() : null;
            long? nodeId = root.TryGetProperty("node_id", out var nd) && nd.ValueKind != JsonValueKind.Null ? nd.GetInt64() : null;

            const string insertPointSql = @"
                INSERT INTO points (id, network_id, feature_type_code, name, status, attributes, geom, is_open, node_id, version, created_by, updated_by, created_at, updated_at)
                VALUES (@id, @networkId, @featureTypeCode, @name, @status::feature_status, @attributes::jsonb, (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END), @isOpen, @nodeId, @version, @createdBy, @username, now(), now());";

            await connection.ExecuteAsync(new CommandDefinition(
                insertPointSql,
                new { id, networkId, featureTypeCode, name, status, attributes, geomJson, isOpen, nodeId, version, createdBy, username },
                transaction: transaction,
                cancellationToken: ct));
        }
        else if (string.Equals(tableName, "lines", StringComparison.OrdinalIgnoreCase))
        {
            var flowDirection = root.TryGetProperty("flow_direction", out var fd) && fd.ValueKind != JsonValueKind.Null ? fd.GetString() ?? "BIDIRECTIONAL" : "BIDIRECTIONAL";
            double? diameterMm = root.TryGetProperty("diameter_mm", out var dm) && dm.ValueKind != JsonValueKind.Null ? dm.GetDouble() : null;
            string? material = root.TryGetProperty("material", out var mat) && mat.ValueKind != JsonValueKind.Null ? mat.GetString() : null;
            long? fromNodeId = root.TryGetProperty("from_node_id", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetInt64() : null;
            long? toNodeId = root.TryGetProperty("to_node_id", out var tn) && tn.ValueKind != JsonValueKind.Null ? tn.GetInt64() : null;

            const string insertLineSql = @"
                INSERT INTO lines (id, network_id, feature_type_code, name, status, attributes, geom, flow_direction, diameter_mm, material, from_node_id, to_node_id, version, created_by, updated_by, created_at, updated_at)
                VALUES (@id, @networkId, @featureTypeCode, @name, @status::feature_status, @attributes::jsonb, (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END), @flowDirection::flow_direction, @diameterMm, @material, @fromNodeId, @toNodeId, @version, @createdBy, @username, now(), now());";

            await connection.ExecuteAsync(new CommandDefinition(
                insertLineSql,
                new { id, networkId, featureTypeCode, name, status, attributes, geomJson, flowDirection, diameterMm, material, fromNodeId, toNodeId, version, createdBy, username },
                transaction: transaction,
                cancellationToken: ct));
        }
        else if (string.Equals(tableName, "polygons", StringComparison.OrdinalIgnoreCase))
        {
            const string insertPolySql = @"
                INSERT INTO polygons (id, network_id, feature_type_code, name, status, attributes, geom, version, created_by, updated_by, created_at, updated_at)
                VALUES (@id, @networkId, @featureTypeCode, @name, @status::feature_status, @attributes::jsonb, (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END), @version, @createdBy, @username, now(), now());";

            await connection.ExecuteAsync(new CommandDefinition(
                insertPolySql,
                new { id, networkId, featureTypeCode, name, status, attributes, geomJson, version, createdBy, username },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    private static async Task RestoreFeatureUpdateAsync(
        NpgsqlConnection connection,
        DbTransaction transaction,
        string tableName,
        Guid recordId,
        string json,
        string username,
        CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var networkId = root.GetProperty("network_id").GetGuid();
        var featureTypeCode = root.GetProperty("feature_type_code").GetString()!;
        var name = root.GetProperty("name").GetString()!;
        var status = root.GetProperty("status").GetString()!;
        var attributes = root.GetProperty("attributes").GetRawText();
        var geomElem = root.GetProperty("geom");
        var geomJson = geomElem.ValueKind == JsonValueKind.String ? geomElem.GetString()! : geomElem.GetRawText();
        var version = root.GetProperty("version").GetInt32();

        if (string.Equals(tableName, "points", StringComparison.OrdinalIgnoreCase))
        {
            bool? isOpen = root.TryGetProperty("is_open", out var op) && op.ValueKind != JsonValueKind.Null ? op.GetBoolean() : null;
            long? nodeId = root.TryGetProperty("node_id", out var nd) && nd.ValueKind != JsonValueKind.Null ? nd.GetInt64() : null;

            const string updatePointSql = @"
                UPDATE points SET
                    network_id = @networkId,
                    feature_type_code = @featureTypeCode,
                    name = @name,
                    status = @status::feature_status,
                    attributes = @attributes::jsonb,
                    geom = (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END),
                    is_open = @isOpen,
                    node_id = @nodeId,
                    version = @version,
                    updated_by = @username,
                    updated_at = now()
                WHERE id = @recordId;";

            await connection.ExecuteAsync(new CommandDefinition(
                updatePointSql,
                new { recordId, networkId, featureTypeCode, name, status, attributes, geomJson, isOpen, nodeId, version, username },
                transaction: transaction,
                cancellationToken: ct));
        }
        else if (string.Equals(tableName, "lines", StringComparison.OrdinalIgnoreCase))
        {
            var flowDirection = root.TryGetProperty("flow_direction", out var fd) && fd.ValueKind != JsonValueKind.Null ? fd.GetString() ?? "BIDIRECTIONAL" : "BIDIRECTIONAL";
            double? diameterMm = root.TryGetProperty("diameter_mm", out var dm) && dm.ValueKind != JsonValueKind.Null ? dm.GetDouble() : null;
            string? material = root.TryGetProperty("material", out var mat) && mat.ValueKind != JsonValueKind.Null ? mat.GetString() : null;
            long? fromNodeId = root.TryGetProperty("from_node_id", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetInt64() : null;
            long? toNodeId = root.TryGetProperty("to_node_id", out var tn) && tn.ValueKind != JsonValueKind.Null ? tn.GetInt64() : null;

            const string updateLineSql = @"
                UPDATE lines SET
                    network_id = @networkId,
                    feature_type_code = @featureTypeCode,
                    name = @name,
                    status = @status::feature_status,
                    attributes = @attributes::jsonb,
                    geom = (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END),
                    flow_direction = @flowDirection::flow_direction,
                    diameter_mm = @diameterMm,
                    material = @material,
                    from_node_id = @fromNodeId,
                    to_node_id = @toNodeId,
                    version = @version,
                    updated_by = @username,
                    updated_at = now()
                WHERE id = @recordId;";

            await connection.ExecuteAsync(new CommandDefinition(
                updateLineSql,
                new { recordId, networkId, featureTypeCode, name, status, attributes, geomJson, flowDirection, diameterMm, material, fromNodeId, toNodeId, version, username },
                transaction: transaction,
                cancellationToken: ct));
        }
        else if (string.Equals(tableName, "polygons", StringComparison.OrdinalIgnoreCase))
        {
            const string updatePolySql = @"
                UPDATE polygons SET
                    network_id = @networkId,
                    feature_type_code = @featureTypeCode,
                    name = @name,
                    status = @status::feature_status,
                    attributes = @attributes::jsonb,
                    geom = (CASE WHEN @geomJson LIKE '{%' THEN ST_SetSRID(ST_GeomFromGeoJSON(@geomJson), 4326) ELSE @geomJson::geometry END),
                    version = @version,
                    updated_by = @username,
                    updated_at = now()
                WHERE id = @recordId;";

            await connection.ExecuteAsync(new CommandDefinition(
                updatePolySql,
                new { recordId, networkId, featureTypeCode, name, status, attributes, geomJson, version, username },
                transaction: transaction,
                cancellationToken: ct));
        }
    }

    private static int ExtractVersion(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 1;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("version", out var prop))
            {
                return prop.GetInt32();
            }
        }
        catch
        {
            // fallback
        }
        return 1;
    }

    private static string? ExtractName(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("name", out var prop))
            {
                return prop.GetString();
            }
        }
        catch
        {
            // fallback
        }
        return null;
    }

    private sealed class ChangeSetRow
    {
        public Guid id { get; set; }
        public Guid? user_id { get; set; }
        public string description { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public DateTime created_at { get; set; }
        public DateTime? undone_at { get; set; }
    }

    private sealed class AuditSummaryRow
    {
        public Guid change_set_id { get; set; }
        public Guid record_id { get; set; }
        public string table_name { get; set; } = string.Empty;
        public string action { get; set; } = string.Empty;
        public string? old_name { get; set; }
        public string? new_name { get; set; }
    }

    private sealed class AuditLogRow
    {
        public long id { get; set; }
        public string table_name { get; set; } = string.Empty;
        public Guid record_id { get; set; }
        public string action { get; set; } = string.Empty;
        public string? old_data { get; set; }
        public string? new_data { get; set; }
    }

    private sealed class FeatureStateRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
    }
}
