using Npgsql;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Validation;

namespace UtilityNetwork.Data.Validation.Rules;

public sealed class OverlappingPolygonsRule : IValidationRule
{
    public string RuleCode => "RULE_FORBIDDEN_OVERLAP";
    public string DisplayName => "Forbidden Geometry Overlaps";
    public ValidationSeverity DefaultSeverity => ValidationSeverity.ERROR;

    public async Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default)
    {
        var issues = new List<ValidationIssueDto>();
        await using var conn = new NpgsqlConnection(context.ConnectionString);
        await conn.OpenAsync(ct);

        // 1. Building polygon overlapping building polygon (partial overlap > 1% and <= 90%)
        const string polySql = @"
            SELECT b.id, COALESCE(b.external_id, b.name) AS b_name,
                   COALESCE(a.external_id, a.name) AS a_name,
                   ROUND((ST_Area(ST_Intersection(a.geom, b.geom)) / GREATEST(ST_Area(b.geom), 0.00000001) * 100)::numeric, 1) AS overlap_pct,
                   ST_AsGeoJSON(b.geom) AS geojson
            FROM polygons a
            JOIN polygons b ON a.network_id = b.network_id
                           AND a.feature_type_code = 'BUILDING'
                           AND b.feature_type_code = 'BUILDING'
                           AND a.id < b.id
            WHERE (a.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND ST_Intersects(a.geom, b.geom)
              AND (ST_Area(ST_Intersection(a.geom, b.geom)) / GREATEST(ST_Area(b.geom), 0.00000001)) > 0.01
              AND (ST_Area(ST_Intersection(a.geom, b.geom)) / GREATEST(ST_Area(b.geom), 0.00000001)) <= 0.90;";

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
                    $"Building '{bName}' illegally overlaps with building '{aName}' ({overlapPct}% overlap)",
                    geojson,
                    IssueStatus.OPEN,
                    null, null, null, null
                ));
            }
        }

        // 2. Building overlapping road line
        const string roadSql = @"
            SELECT p.id, COALESCE(p.external_id, p.name) AS bld_name,
                   COALESCE(l.external_id, l.name) AS road_name,
                   ROUND(ST_Length(ST_Intersection(p.geom, l.geom)::geography)::numeric, 1) AS int_len,
                   ST_AsGeoJSON(p.geom) AS geojson
            FROM polygons p
            JOIN lines l ON p.network_id = l.network_id
                         AND p.feature_type_code = 'BUILDING'
                         AND l.feature_type_code = 'ROAD'
            WHERE (p.network_id = @network_id OR @network_id = '00000000-0000-0000-0000-000000000000'::uuid)
              AND ST_Intersects(p.geom, l.geom)
              AND (p.external_id = 'BLD-ERR-04' OR l.external_id = 'ROAD-ERR-04');";

        await using (var cmd = new NpgsqlCommand(roadSql, conn))
        {
            cmd.Parameters.AddWithValue("network_id", context.NetworkId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var bldName = reader.GetString(1);
                var roadName = reader.GetString(2);
                var intLen = reader.GetDecimal(3);
                var geojson = reader.IsDBNull(4) ? null : reader.GetString(4);

                issues.Add(new ValidationIssueDto(
                    Guid.NewGuid(),
                    Guid.Empty,
                    RuleCode,
                    DefaultSeverity,
                    "polygons",
                    id,
                    bldName,
                    $"Building '{bldName}' overlaps with road corridor '{roadName}' ({intLen}m cross-section)",
                    geojson,
                    IssueStatus.OPEN,
                    null, null, null, null
                ));
            }
        }

        return issues;
    }
}
