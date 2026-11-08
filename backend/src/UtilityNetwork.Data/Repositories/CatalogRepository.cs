using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly string _connectionString;

    public CatalogRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<IReadOnlyList<NetworkDto>> GetNetworksAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id AS Id, code AS Code, name AS Name, domain AS Domain, 
                   is_directed AS IsDirected, snap_tolerance_m AS SnapToleranceM, 
                   created_at AS CreatedAt
            FROM networks ORDER BY name ASC;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var items = await conn.QueryAsync<NetworkDto>(new CommandDefinition(sql, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<NetworkDto?> GetNetworkByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id AS Id, code AS Code, name AS Name, domain AS Domain, 
                   is_directed AS IsDirected, snap_tolerance_m AS SnapToleranceM, 
                   created_at AS CreatedAt
            FROM networks WHERE id = @id;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<NetworkDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FeatureTypeDto>> GetFeatureTypesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT code AS Code, network_domain AS NetworkDomain, geometry_kind::text AS GeometryKind, 
                   display_name AS DisplayName, topology_role::text AS TopologyRole, 
                   attribute_schema::text AS AttributeSchemaJson, style::text AS StyleJson, 
                   is_active AS IsActive
            FROM feature_types ORDER BY code ASC;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<FeatureTypeDbDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<FeatureTypeDto?> GetFeatureTypeByCodeAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT code AS Code, network_domain AS NetworkDomain, geometry_kind::text AS GeometryKind, 
                   display_name AS DisplayName, topology_role::text AS TopologyRole, 
                   attribute_schema::text AS AttributeSchemaJson, style::text AS StyleJson, 
                   is_active AS IsActive
            FROM feature_types WHERE upper(code) = upper(@code);";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<FeatureTypeDbDto>(new CommandDefinition(sql, new { code }, cancellationToken: ct));
        return row?.ToDto();
    }

    public async Task<RichWorkspaceSummaryDto?> GetNetworkSummaryAsync(Guid networkId, CancellationToken ct = default)
    {
        var network = await GetNetworkByIdAsync(networkId, ct);
        if (network == null) return null;

        const string sqlCounts = @"
            SELECT feature_type_code AS FeatureTypeCode, status AS Status, count(*)::int AS Count
            FROM (
                SELECT feature_type_code, status FROM points WHERE network_id = @networkId
                UNION ALL
                SELECT feature_type_code, status FROM lines WHERE network_id = @networkId
                UNION ALL
                SELECT feature_type_code, status FROM polygons WHERE network_id = @networkId
            ) f
            GROUP BY feature_type_code, status
            ORDER BY feature_type_code ASC, status ASC;";

        const string sqlLines = @"
            SELECT feature_type_code, COALESCE(SUM(ST_Length(geom::geography)), 0.0) AS length_m
            FROM lines
            WHERE network_id = @networkId
            GROUP BY feature_type_code;";

        const string sqlPolygons = @"
            SELECT feature_type_code, COALESCE(SUM(ST_Area(geom::geography)), 0.0) AS area_sqm
            FROM polygons
            WHERE network_id = @networkId
            GROUP BY feature_type_code;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var countRows = (await conn.QueryAsync<TypeStatusCountDto>(new CommandDefinition(sqlCounts, new { networkId }, cancellationToken: ct))).ToList();
        var lineRows = (await conn.QueryAsync<(string FeatureTypeCode, double LengthM)>(new CommandDefinition(sqlLines, new { networkId }, cancellationToken: ct))).ToList();
        var polygonRows = (await conn.QueryAsync<(string FeatureTypeCode, double AreaSqm)>(new CommandDefinition(sqlPolygons, new { networkId }, cancellationToken: ct))).ToList();

        var total = countRows.Sum(r => r.Count);
        var byType = countRows.GroupBy(r => r.FeatureTypeCode)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));
        var byStatus = countRows.GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));

        var lengthsByType = lineRows.ToDictionary(r => r.FeatureTypeCode, r => Math.Round(r.LengthM, 2));
        var areasByType = polygonRows.ToDictionary(r => r.FeatureTypeCode, r => Math.Round(r.AreaSqm, 2));

        var totalLength = lengthsByType.Values.Sum();
        var totalArea = areasByType.Values.Sum();

        return new RichWorkspaceSummaryDto(
            WorkspaceId: network.Id,
            WorkspaceName: network.Name,
            Domain: network.Domain,
            TotalFeatures: total,
            TotalLengthM: Math.Round(totalLength, 2),
            TotalAreaSqm: Math.Round(totalArea, 2),
            CountsByType: byType,
            CountsByStatus: byStatus,
            LengthsByTypeM: lengthsByType,
            AreasByTypeSqm: areasByType,
            Details: countRows);
    }

    private sealed record FeatureTypeDbDto(
        string Code,
        string NetworkDomain,
        string GeometryKind,
        string DisplayName,
        string TopologyRole,
        string AttributeSchemaJson,
        string StyleJson,
        bool IsActive)
    {
        public FeatureTypeDto ToDto() => new(
            Code,
            NetworkDomain,
            GeometryKind,
            DisplayName,
            TopologyRole,
            JsonDocument.Parse(string.IsNullOrWhiteSpace(AttributeSchemaJson) ? "{}" : AttributeSchemaJson).RootElement.Clone(),
            JsonDocument.Parse(string.IsNullOrWhiteSpace(StyleJson) ? "{}" : StyleJson).RootElement.Clone(),
            IsActive);
    }
}
