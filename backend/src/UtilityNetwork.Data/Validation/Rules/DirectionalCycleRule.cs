using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class DirectionalCycleRule : IValidationRule
{
    public string RuleCode => "RULE_DIRECTIONAL_CYCLE";
    public string DisplayName => "Directional Cycle Detected";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.WARNING;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            WITH RECURSIVE directed_edges AS (
                SELECT
                    te.line_id,
                    CASE WHEN te.direction = 'AGAINST_DIGITIZED' THEN te.to_node ELSE te.from_node END AS u,
                    CASE WHEN te.direction = 'AGAINST_DIGITIZED' THEN te.from_node ELSE te.to_node END AS v
                FROM topology_edges te
                WHERE (te.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
                  AND te.direction IN ('WITH_DIGITIZED', 'AGAINST_DIGITIZED')
            ),
            paths AS (
                SELECT u, v, ARRAY[u] AS visited, ARRAY[line_id] AS edge_path, false AS is_cycle
                FROM directed_edges
                UNION ALL
                SELECT p.u, de.v, p.visited || de.v, p.edge_path || de.line_id, de.v = ANY(p.visited)
                FROM paths p
                JOIN directed_edges de ON p.v = de.u
                WHERE NOT p.is_cycle AND array_length(p.visited, 1) < 15
            ),
            cyclic_lines AS (
                SELECT DISTINCT unnest(edge_path) AS line_id
                FROM paths
                WHERE is_cycle
            )
            SELECT l.id,
                   COALESCE(l.external_id, l.name) AS asset_name,
                   ST_AsGeoJSON(l.geom) AS geojson
            FROM lines l
            JOIN cyclic_lines cl ON l.id = cl.line_id
            WHERE (l.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid);";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("network_id", context.NetworkId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var assetName = reader.GetString(1);
            var geojson = reader.IsDBNull(2) ? null : reader.GetString(2);

            issues.Add(new ValidationIssueDto(
                Guid.NewGuid(),
                Guid.Empty,
                RuleCode,
                DefaultSeverity,
                "lines",
                id,
                assetName,
                $"Pipe asset '{assetName}' participates in a closed directional flow cycle (hydraulic anomaly)",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}

