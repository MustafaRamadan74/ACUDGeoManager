using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class ExportService : IExportService
{
    private readonly string _connectionString;
    private static readonly HashSet<int> AllowedSrids = new() { 4326, 22992, 32636 };
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    public ExportService(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private sealed class RawExportRow
    {
        public Guid Id { get; set; }
        public Guid NetworkId { get; set; }
        public string FeatureTypeCode { get; set; } = string.Empty;
        public string GeometryKind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ExternalId { get; set; }
        public int Version { get; set; }
        public bool? IsOpen { get; set; }
        public string? FlowDirection { get; set; }
        public int? DiameterMm { get; set; }
        public string? Material { get; set; }
        public string? AttributesJson { get; set; }
        public string GeomData { get; set; } = string.Empty;
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> ExportGeoJsonAsync(
        ExportFilterRequest filter,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var srid = ValidateSrid(filter.Srid);
        var rows = await QueryRowsAsync(filter, srid, asGeoJson: true, ct);

        var featuresList = new List<Dictionary<string, object?>>();

        foreach (var r in rows)
        {
            JsonElement geomJson;
            try
            {
                using var doc = JsonDocument.Parse(r.GeomData);
                geomJson = doc.RootElement.Clone();
            }
            catch
            {
                continue;
            }

            var props = new Dictionary<string, object?>
            {
                ["id"] = r.Id,
                ["networkId"] = r.NetworkId,
                ["featureTypeCode"] = r.FeatureTypeCode,
                ["geometryKind"] = r.GeometryKind,
                ["name"] = r.Name,
                ["status"] = r.Status,
                ["externalId"] = r.ExternalId,
                ["version"] = r.Version
            };

            if (r.IsOpen.HasValue) props["isOpen"] = r.IsOpen.Value;
            if (!string.IsNullOrEmpty(r.FlowDirection)) props["flowDirection"] = r.FlowDirection;
            if (r.DiameterMm.HasValue) props["diameterMm"] = r.DiameterMm.Value;
            if (!string.IsNullOrEmpty(r.Material)) props["material"] = r.Material;

            if (!string.IsNullOrWhiteSpace(r.AttributesJson))
            {
                try
                {
                    using var attrDoc = JsonDocument.Parse(r.AttributesJson);
                    props["attributes"] = attrDoc.RootElement.Clone();
                }
                catch
                {
                    props["attributes"] = null;
                }
            }

            featuresList.Add(new Dictionary<string, object?>
            {
                ["type"] = "Feature",
                ["geometry"] = geomJson,
                ["properties"] = props
            });
        }

        var collection = new Dictionary<string, object?>
        {
            ["type"] = "FeatureCollection"
        };

        if (srid != 4326)
        {
            collection["crs"] = new Dictionary<string, object>
            {
                ["type"] = "name",
                ["properties"] = new Dictionary<string, string>
                {
                    ["name"] = $"urn:ogc:def:crs:EPSG::{srid}"
                }
            };
        }

        collection["features"] = featuresList;

        var jsonString = JsonSerializer.Serialize(collection, IndentedJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(jsonString);
        var fileName = $"export_{filter.FeatureTypeCode ?? "all"}_{srid}_{DateTime.UtcNow:yyyyMMddHHmmss}.geojson";

        return (bytes, "application/geo+json", fileName);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> ExportCsvAsync(
        ExportFilterRequest filter,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var srid = ValidateSrid(filter.Srid);
        var rows = await QueryRowsAsync(filter, srid, asGeoJson: false, ct);

        var sb = new StringBuilder();
        // CSV Header
        sb.AppendLine("id,externalId,featureTypeCode,geometryKind,name,status,isOpen,flowDirection,diameterMm,material,geom,attributes");

        foreach (var r in rows)
        {
            sb.Append(EscapeCsv(r.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(r.ExternalId)).Append(',');
            sb.Append(EscapeCsv(r.FeatureTypeCode)).Append(',');
            sb.Append(EscapeCsv(r.GeometryKind)).Append(',');
            sb.Append(EscapeCsv(r.Name)).Append(',');
            sb.Append(EscapeCsv(r.Status)).Append(',');
            sb.Append(r.IsOpen.HasValue ? r.IsOpen.Value.ToString().ToLowerInvariant() : "").Append(',');
            sb.Append(EscapeCsv(r.FlowDirection)).Append(',');
            sb.Append(r.DiameterMm?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.Append(EscapeCsv(r.Material)).Append(',');
            sb.Append(EscapeCsv(r.GeomData)).Append(',');
            sb.AppendLine(EscapeCsv(r.AttributesJson));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"export_{filter.FeatureTypeCode ?? "all"}_{srid}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return (bytes, "text/csv; charset=utf-8", fileName);
    }

    private static int ValidateSrid(int srid)
    {
        return AllowedSrids.Contains(srid) ? srid : 4326;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        // Formula Injection protection: prepend single quote if value begins with dangerous formula characters
        var sanitized = value;
        if (sanitized.Length > 0 && (sanitized[0] == '=' || sanitized[0] == '+' || sanitized[0] == '-' || sanitized[0] == '@'))
        {
            sanitized = "'" + sanitized;
        }

        if (sanitized.Contains(',') || sanitized.Contains('"') || sanitized.Contains('\n') || sanitized.Contains('\r'))
        {
            sanitized = "\"" + sanitized.Replace("\"", "\"\"") + "\"";
        }

        return sanitized;
    }

    private async Task<List<RawExportRow>> QueryRowsAsync(
        ExportFilterRequest filter,
        int srid,
        bool asGeoJson,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var geomFunc = asGeoJson
            ? (srid == 4326 ? "ST_AsGeoJSON(p.geom)" : $"ST_AsGeoJSON(ST_Transform(p.geom, {srid}))")
            : (srid == 4326 ? "ST_AsText(p.geom)" : $"ST_AsText(ST_Transform(p.geom, {srid}))");

        var geomFuncLines = asGeoJson
            ? (srid == 4326 ? "ST_AsGeoJSON(l.geom)" : $"ST_AsGeoJSON(ST_Transform(l.geom, {srid}))")
            : (srid == 4326 ? "ST_AsText(l.geom)" : $"ST_AsText(ST_Transform(l.geom, {srid}))");

        var geomFuncPolys = asGeoJson
            ? (srid == 4326 ? "ST_AsGeoJSON(pg.geom)" : $"ST_AsGeoJSON(ST_Transform(pg.geom, {srid}))")
            : (srid == 4326 ? "ST_AsText(pg.geom)" : $"ST_AsText(ST_Transform(pg.geom, {srid}))");

        var bboxClause = "";
        var parameters = new DynamicParameters();

        if (filter.NetworkId.HasValue)
        {
            parameters.Add("networkId", filter.NetworkId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.FeatureTypeCode))
        {
            parameters.Add("featureTypeCode", filter.FeatureTypeCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.Bbox))
        {
            var parts = filter.Bbox.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var minX) &&
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var minY) &&
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var maxX) &&
                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var maxY))
            {
                bboxClause = " AND ST_Intersects(geom, ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326))";
                parameters.Add("minX", minX);
                parameters.Add("minY", minY);
                parameters.Add("maxX", maxX);
                parameters.Add("maxY", maxY);
            }
        }

        var results = new List<RawExportRow>();

        // Query points
        var sqlPoints = $@"
            SELECT p.id, p.network_id AS NetworkId, p.feature_type_code AS FeatureTypeCode, 'POINT' AS GeometryKind,
                   p.name, p.status, p.external_id AS ExternalId, p.version,
                   p.is_open AS IsOpen, NULL AS FlowDirection, NULL AS DiameterMm, NULL AS Material,
                   p.attributes::text AS AttributesJson,
                   {geomFunc} AS GeomData
            FROM points p
            WHERE (@networkId IS NULL OR p.network_id = @networkId)
              AND (@featureTypeCode IS NULL OR p.feature_type_code = @featureTypeCode)
              {bboxClause.Replace("geom", "p.geom")};";

        var points = (await conn.QueryAsync<RawExportRow>(sqlPoints, parameters)).ToList();
        results.AddRange(points);

        // Query lines
        var sqlLines = $@"
            SELECT l.id, l.network_id AS NetworkId, l.feature_type_code AS FeatureTypeCode, 'LINE' AS GeometryKind,
                   l.name, l.status, l.external_id AS ExternalId, l.version,
                   NULL AS IsOpen, l.flow_direction AS FlowDirection, l.diameter_mm AS DiameterMm, l.material AS Material,
                   l.attributes::text AS AttributesJson,
                   {geomFuncLines} AS GeomData
            FROM lines l
            WHERE (@networkId IS NULL OR l.network_id = @networkId)
              AND (@featureTypeCode IS NULL OR l.feature_type_code = @featureTypeCode)
              {bboxClause.Replace("geom", "l.geom")};";

        var lines = (await conn.QueryAsync<RawExportRow>(sqlLines, parameters)).ToList();
        results.AddRange(lines);

        // Query polygons
        var sqlPolygons = $@"
            SELECT pg.id, pg.network_id AS NetworkId, pg.feature_type_code AS FeatureTypeCode, 'POLYGON' AS GeometryKind,
                   pg.name, pg.status, pg.external_id AS ExternalId, pg.version,
                   NULL AS IsOpen, NULL AS FlowDirection, NULL AS DiameterMm, NULL AS Material,
                   pg.attributes::text AS AttributesJson,
                   {geomFuncPolys} AS GeomData
            FROM polygons pg
            WHERE (@networkId IS NULL OR pg.network_id = @networkId)
              AND (@featureTypeCode IS NULL OR pg.feature_type_code = @featureTypeCode)
              {bboxClause.Replace("geom", "pg.geom")};";

        var polygons = (await conn.QueryAsync<RawExportRow>(sqlPolygons, parameters)).ToList();
        results.AddRange(polygons);

        return results;
    }
}
