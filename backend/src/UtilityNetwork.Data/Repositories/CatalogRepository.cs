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
