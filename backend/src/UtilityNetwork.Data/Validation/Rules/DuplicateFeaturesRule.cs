using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class DuplicateFeaturesRule : IValidationRule
{
    public string RuleCode => "RULE_DUPLICATE_FEATURE";
    public string DisplayName => "Duplicate Features";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        // 1. Points duplicates (identical or near-identical coordinates < 0.05m)
        const string pointSql = @"
            SELECT b.id, COALESCE(b.external_id, b.name) AS b_name,
                   COALESCE(a.external_id, a.name) AS a_name,
                   ST_AsGeoJSON(b.geom) AS geojson
            FROM points a
            JOIN points b ON a.network_id = b.network_id
                         AND a.feature_type_code = b.feature_type_code
                         AND a.id < b.id
            WHERE (a.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND ST_DWithin(a.geom::geography, b.geom::geography, 0.05);";

        await using (var cmd = new NpgsqlCommand(pointSql, conn))
        {
            cmd.Parameters.AddWithValue("network_id", context.NetworkId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var bName = reader.GetString(1);
                var aName = reader.GetString(2);
                var geojson = reader.IsDBNull(3) ? null : reader.GetString(3);

                issues.Add(new ValidationIssueDto(
                    Guid.NewGuid(),
                    Guid.Empty,
                    RuleCode,
                    DefaultSeverity,
                    "points",
                    id,
                    bName,
                    $"Point feature '{bName}' is a duplicate of '{aName}' with identical coordinates",
                    geojson,
                    IssueStatus.OPEN,
                    null, null, null, null
                ));
            }
        }

        // 2. Polygons duplicates (> 90% footprint overlap of same feature type)
        const string polySql = @"
            SELECT b.id, COALESCE(b.external_id, b.name) AS b_name,
                   COALESCE(a.external_id, a.name) AS a_name,
                   ROUND((ST_Area(ST_Intersection(a.geom, b.geom)) / GREATEST(ST_Area(a.geom), 0.00000001) * 100)::numeric, 1) AS overlap_pct,
                   ST_AsGeoJSON(b.geom) AS geojson
            FROM polygons a
            JOIN polygons b ON a.network_id = b.network_id
                           AND a.feature_type_code = b.feature_type_code
                           AND a.id < b.id
            WHERE (a.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND ST_Intersects(a.geom, b.geom)
              AND (ST_Area(ST_Intersection(a.geom, b.geom)) / GREATEST(ST_Area(a.geom), 0.00000001)) > 0.90;";

        await using (var cmd = new NpgsqlCommand(polySql, conn))
        {
            cmd.Parameters.AddWithValue("network_id", context.NetworkId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var bName = reader.GetString(1);
                var aName = reader.GetString(2);
                var overlapPct = reader.GetDecimal(3);
                var geojson = reader.IsDBNull(4) ? null : reader.GetString(4);

                issues.Add(new ValidationIssueDto(
                    Guid.NewGuid(),
                    Guid.Empty,
                    RuleCode,
                    DefaultSeverity,
                    "polygons",
                    id,
                    bName,
                    $"Polygon feature '{bName}' is a duplicate footprint of '{aName}' ({overlapPct}% spatial overlap)",
                    geojson,
                    IssueStatus.OPEN,
                    null, null, null, null
                ));
            }
        }

        return issues;
    }
}
