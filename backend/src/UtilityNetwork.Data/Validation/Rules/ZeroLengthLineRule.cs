using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class ZeroLengthLineRule : IValidationRule
{
    public string RuleCode => "RULE_ZERO_LENGTH_LINE";
    public string DisplayName => "Zero or Sub-Threshold Length Line";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, COALESCE(external_id, name) AS asset_name,
                   ST_Length(geom::geography) AS len_m,
                   ST_AsGeoJSON(geom) AS geojson
            FROM lines
            WHERE (network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND ST_Length(geom::geography) < 1.0;";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("network_id", context.NetworkId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetGuid(0);
            var assetName = reader.GetString(1);
            var lenM = reader.GetDouble(2);
            var geojson = reader.IsDBNull(3) ? null : reader.GetString(3);

            issues.Add(new ValidationIssueDto(
                Guid.NewGuid(),
                Guid.Empty,
                RuleCode,
                DefaultSeverity,
                "lines",
                id,
                assetName,
                $"Line asset '{assetName}' has near-zero length ({lenM:F2}m < 1.00m threshold)",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}
