using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class TopologyRepository : ITopologyRepository
{
    private readonly string _connectionString;

    public TopologyRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<TopologyStatusDto> GetStatusAsync(Guid networkId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT network_id AS ""NetworkId"",
                   is_dirty AS ""IsDirty"",
                   rebuilt_at AS ""LastRebuiltAt"",
                   node_count AS ""NodeCount"",
                   edge_count AS ""EdgeCount"",
                   dirty_feature_count AS ""DirtyFeatureCount""
            FROM topology_state
            WHERE network_id = @networkId;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var status = await conn.QuerySingleOrDefaultAsync<TopologyStatusDto>(
            new CommandDefinition(sql, new { networkId }, cancellationToken: ct));

        if (status != null) return status;

        // Initialize state if not found
        const string insertSql = @"
            INSERT INTO topology_state (network_id, is_dirty, node_count, edge_count, dirty_feature_count)
            VALUES (@networkId, true, 0, 0, 0)
            ON CONFLICT (network_id) DO NOTHING;

            SELECT network_id AS ""NetworkId"",
                   is_dirty AS ""IsDirty"",
                   rebuilt_at AS ""LastRebuiltAt"",
                   node_count AS ""NodeCount"",
                   edge_count AS ""EdgeCount"",
                   dirty_feature_count AS ""DirtyFeatureCount""
            FROM topology_state
            WHERE network_id = @networkId;";

        return await conn.QuerySingleAsync<TopologyStatusDto>(
            new CommandDefinition(insertSql, new { networkId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TopologyStatusDto>> GetAllStatusesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ts.network_id AS ""NetworkId"",
                   ts.is_dirty AS ""IsDirty"",
                   ts.rebuilt_at AS ""LastRebuiltAt"",
                   ts.node_count AS ""NodeCount"",
                   ts.edge_count AS ""EdgeCount"",
                   ts.dirty_feature_count AS ""DirtyFeatureCount""
            FROM topology_state ts
            JOIN networks n ON ts.network_id = n.id
            ORDER BY n.name ASC;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var list = await conn.QueryAsync<TopologyStatusDto>(
            new CommandDefinition(sql, cancellationToken: ct));

        return list.AsList();
    }

    public async Task<RebuildTopologyResultDto> RebuildTopologyAsync(Guid networkId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        const string sql = @"SELECT * FROM fn_rebuild_network_topology(@networkId);";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var row = await conn.QuerySingleAsync<dynamic>(
            new CommandDefinition(sql, new { networkId }, cancellationToken: ct));

        sw.Stop();

        var isDirty = (bool)row.is_dirty;
        var nodeCount = (int)row.node_count;
        var edgeCount = (int)row.edge_count;
        var rebuiltAt = (DateTime)row.rebuilt_at;

        return new RebuildTopologyResultDto(
            NetworkId: networkId,
            IsDirty: isDirty,
            NodeCount: nodeCount,
            EdgeCount: edgeCount,
            RebuiltAt: rebuiltAt,
            DurationMs: sw.ElapsedMilliseconds,
            Message: $"Topology rebuilt successfully: {nodeCount} nodes clustered, {edgeCount} edges created in {sw.ElapsedMilliseconds}ms."
        );
    }

    public async Task<IReadOnlyList<TopologyNodeDto>> GetNodesAsync(Guid networkId, int limit = 500, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tn.id AS ""Id"",
                   tn.network_id AS ""NetworkId"",
                   ST_X(tn.geom) AS ""X"",
                   ST_Y(tn.geom) AS ""Y"",
                   tn.node_role::text AS ""NodeRole"",
                   tn.elevation_m AS ""ElevationM"",
                   COUNT(tnf.point_id)::int AS ""AttachedFeatureCount""
            FROM topology_nodes tn
            LEFT JOIN topology_node_features tnf ON tn.id = tnf.node_id
            WHERE tn.network_id = @networkId
            GROUP BY tn.id, tn.network_id, tn.geom, tn.node_role, tn.elevation_m
            ORDER BY tn.id ASC
            LIMIT @limit;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var nodes = await conn.QueryAsync<TopologyNodeDto>(
            new CommandDefinition(sql, new { networkId, limit }, cancellationToken: ct));

        return nodes.AsList();
    }

    public async Task<IReadOnlyList<TopologyEdgeDto>> GetEdgesAsync(Guid networkId, int limit = 1000, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT te.id AS ""Id"",
                   te.network_id AS ""NetworkId"",
                   te.line_id AS ""LineFeatureId"",
                   te.from_node AS ""FromNodeId"",
                   te.to_node AS ""ToNodeId"",
                   te.direction::text AS ""FlowDirection"",
                   te.length_m AS ""LengthM""
            FROM topology_edges te
            WHERE te.network_id = @networkId
            ORDER BY te.id ASC
            LIMIT @limit;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var edges = await conn.QueryAsync<TopologyEdgeDto>(
            new CommandDefinition(sql, new { networkId, limit }, cancellationToken: ct));

        return edges.AsList();
    }

    public async Task MarkDirtyAsync(Guid networkId, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO topology_state (network_id, is_dirty, dirty_feature_count)
            VALUES (@networkId, true, 1)
            ON CONFLICT (network_id)
            DO UPDATE SET
                is_dirty = true,
                dirty_feature_count = topology_state.dirty_feature_count + 1;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(sql, new { networkId }, cancellationToken: ct));
    }
}

