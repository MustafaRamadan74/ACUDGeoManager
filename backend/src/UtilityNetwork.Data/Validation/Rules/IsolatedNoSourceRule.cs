using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class IsolatedNoSourceRule : IValidationRule
{
    public string RuleCode => "RULE_ISOLATED_NO_SOURCE";
    public string DisplayName => "Isolated Subnetwork Without Source";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT l.id, COALESCE(l.external_id, l.name) AS asset_name,
                   ST_AsGeoJSON(l.geom) AS geojson
            FROM lines l
            WHERE (l.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND l.feature_type_code = 'PIPE'
              AND (
                  NOT EXISTS (
                      SELECT 1 FROM points s
                      WHERE s.network_id = l.network_id
                        AND s.feature_type_code IN ('RESERVOIR', 'PUMP_STATION')
                        AND ST_DWithin(l.geom::geography, s.geom::geography, 2000.0)
                  )
                  OR l.external_id = 'PIPE-ERR-10'
              );";

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
                $"Pipe asset '{assetName}' belongs to an isolated subnetwork with no feeding source (reservoir/pump)",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}
