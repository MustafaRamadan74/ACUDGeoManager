using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class MissingTerminalFeatureRule : IValidationRule
{
    public string RuleCode => "RULE_MISSING_TERMINAL";
    public string DisplayName => "Terminal Node Without Terminal Feature";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.WARNING;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            WITH node_degrees AS (
                SELECT node_id, count(*) AS degree
                FROM (
                    SELECT from_node AS node_id FROM topology_edges WHERE network_id = @network_id
                    UNION ALL
                    SELECT to_node AS node_id FROM topology_edges WHERE network_id = @network_id
                ) all_ends
                GROUP BY node_id
            ),
            terminal_nodes AS (
                SELECT nd.node_id
                FROM node_degrees nd
                WHERE nd.degree = 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM topology_node_features tnf
                      JOIN points p ON tnf.point_id = p.id
                      JOIN feature_types ft ON p.feature_type_code = ft.code
                      WHERE tnf.node_id = nd.node_id
                        AND (ft.topology_role = 'TERMINAL' OR ft.code IN ('HYDRANT', 'CUSTOMER_METER'))
                  )
            )
            SELECT DISTINCT l.id,
                   COALESCE(l.external_id, l.name) AS asset_name,
                   ST_AsGeoJSON(tn.geom) AS geojson
            FROM terminal_nodes t
            JOIN topology_nodes tn ON t.node_id = tn.id
            JOIN topology_edges te ON (te.from_node = tn.id OR te.to_node = tn.id)
            JOIN lines l ON te.line_id = l.id
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
                $"Network endpoint on pipe '{assetName}' terminates without an end cap, customer meter, or hydrant feature",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}

