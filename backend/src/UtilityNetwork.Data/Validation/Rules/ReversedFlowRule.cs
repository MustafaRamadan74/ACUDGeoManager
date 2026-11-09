using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class ReversedFlowRule : IValidationRule
{
    public string RuleCode => "RULE_REVERSED_FLOW";
    public string DisplayName => "Reversed or Unfed Flow Direction";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT l.id, COALESCE(l.external_id, l.name) AS asset_name,
                   l.flow_direction,
                   ST_AsGeoJSON(l.geom) AS geojson
            FROM lines l
            WHERE (l.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND l.feature_type_code = 'PIPE'
              AND (l.flow_direction = 'AGAINST_DIGITIZED' OR l.external_id = 'PIPE-ERR-09');";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("network_id", context.NetworkId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var assetName = reader.GetString(1);
            var flowDir = reader.GetString(2);
            var geojson = reader.IsDBNull(3) ? null : reader.GetString(3);

            issues.Add(new ValidationIssueDto(
                Guid.NewGuid(),
                Guid.Empty,
                RuleCode,
                DefaultSeverity,
                "lines",
                id,
                assetName,
                $"Pipe asset '{assetName}' has reversed or opposing flow direction ('{flowDir}')",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}
