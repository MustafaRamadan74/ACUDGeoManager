using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class ValveOffPipeRule : IValidationRule
{
    public string RuleCode => "RULE_VALVE_OFF_PIPE";
    public string DisplayName => "Valve Off-Pipe (Disconnected Valve)";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT v.id, COALESCE(v.external_id, v.name) AS asset_name,
                   ROUND((SELECT MIN(ST_Distance(v.geom::geography, p.geom::geography))
                          FROM lines p
                          WHERE p.network_id = v.network_id AND p.feature_type_code = 'PIPE')::numeric, 1) AS dist_m,
                   ST_AsGeoJSON(v.geom) AS geojson
            FROM points v
            WHERE (v.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND v.feature_type_code = 'VALVE'
              AND NOT EXISTS (
                  SELECT 1 FROM lines p
                  WHERE p.network_id = v.network_id
                    AND p.feature_type_code = 'PIPE'
                    AND ST_DWithin(v.geom::geography, p.geom::geography, 1.0)
              );";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("network_id", context.NetworkId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var assetName = reader.GetString(1);
            var distM = reader.IsDBNull(2) ? 999.0m : reader.GetDecimal(2);
            var geojson = reader.IsDBNull(3) ? null : reader.GetString(3);

            issues.Add(new ValidationIssueDto(
                Guid.NewGuid(),
                Guid.Empty,
                RuleCode,
                DefaultSeverity,
                "points",
                id,
                assetName,
                $"Valve asset '{assetName}' is off-pipe (nearest pipe line is {distM}m away, exceeds 1.0m tolerance)",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}
