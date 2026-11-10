using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class DanglingLineRule : IValidationRule
{
    public string RuleCode => "RULE_DANGLING_PIPE";
    public string DisplayName => "Dangling Pipe End";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.WARNING;

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
                  (NOT EXISTS (
                      SELECT 1 FROM lines o WHERE o.id <> l.id AND o.network_id = l.network_id
                      AND ST_DWithin(ST_StartPoint(l.geom)::geography, o.geom::geography, 0.5)
                  ) AND NOT EXISTS (
                      SELECT 1 FROM points pt WHERE pt.network_id = l.network_id
                      AND ST_DWithin(ST_StartPoint(l.geom)::geography, pt.geom::geography, 0.5)
                  ))
                  OR
                  (NOT EXISTS (
                      SELECT 1 FROM lines o WHERE o.id <> l.id AND o.network_id = l.network_id
                      AND ST_DWithin(ST_EndPoint(l.geom)::geography, o.geom::geography, 0.5)
                  ) AND NOT EXISTS (
                      SELECT 1 FROM points pt WHERE pt.network_id = l.network_id
                      AND ST_DWithin(ST_EndPoint(l.geom)::geography, pt.geom::geography, 0.5)
                  ))
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
                $"Pipe asset '{assetName}' has a dangling unconnected endpoint without junction or terminal",
                geojson,
                IssueStatus.OPEN,
                null, null, null, null
            ));
        }

        return issues;
    }
}
