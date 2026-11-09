using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class EmptyGeometryRule : IValidationRule
{
    public string RuleCode => "RULE_EMPTY_GEOM";
    public string DisplayName => "Empty Geometry";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        var tables = new[] { "points", "lines", "polygons" };
        foreach (var tbl in tables)
        {
            var sql = $@"
                SELECT id, COALESCE(external_id, name) AS asset_name
                FROM {tbl}
                WHERE (network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
                  AND ST_IsEmpty(geom);";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("network_id", context.NetworkId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var assetName = reader.GetString(1);

                issues.Add(new ValidationIssueDto(
                    Guid.NewGuid(),
                    Guid.Empty,
                    RuleCode,
                    DefaultSeverity,
                    tbl,
                    id,
                    assetName,
                    $"Asset '{assetName}' has empty geometry",
                    null,
                    IssueStatus.OPEN,
                    null, null, null, null
                ));
            }
        }

        return issues;
    }
}
