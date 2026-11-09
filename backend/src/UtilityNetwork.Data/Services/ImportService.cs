using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed partial class ImportService : IImportService
{
    private readonly string _connectionString;
    private readonly IImportPreviewStore _previewStore;
    private readonly IUnitOfWorkFactory _uowFactory;

    private const int MaxFeatureLimit = 5000;
    private const int MaxContentBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<int> AllowedSrids = new() { 4326, 22992, 32636 };
    private static readonly string[] NewLineSeparators = ["\r\n", "\r", "\n"];
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "PLANNED", "DECOMMISSIONED"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    private sealed class GeomValidationRow
    {
        public bool IsValid { get; set; }
        public string? GeomType { get; set; }
        public string? GeoJson4326 { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public ImportService(
        IOptions<DatabaseOptions> dbOptions,
        IImportPreviewStore previewStore,
        IUnitOfWorkFactory uowFactory)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
        _previewStore = previewStore;
        _uowFactory = uowFactory;
    }

    public async Task<ImportPreviewResultDto> PreviewImportAsync(
        ImportPreviewRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Import content cannot be empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
        {
            throw new ArgumentException("Import content exceeds maximum allowed size of 10 MB.");
        }

        var srid = AllowedSrids.Contains(request.Srid) ? request.Srid : 4326;

        // Fetch valid feature types for network
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var catalogTypes = (await conn.QueryAsync<(string Code, string GeometryKind)>(
            "SELECT code, geometry_kind FROM feature_types WHERE workspace_id = @networkId AND is_active = true;",
            new { networkId = request.NetworkId }
        )).ToDictionary(x => x.Code, x => x.GeometryKind, StringComparer.OrdinalIgnoreCase);

        if (catalogTypes.Count == 0)
        {
            throw new InvalidOperationException($"No active feature types found for network '{request.NetworkId}'.");
        }

        // Parse content: GeoJSON or CSV
        var rawFeatures = ParseRawFeatures(request.Content, request.FeatureTypeCode);

        if (rawFeatures.Count > MaxFeatureLimit)
        {
            throw new ArgumentException($"Import contains {rawFeatures.Count} features, which exceeds the limit of {MaxFeatureLimit}.");
        }

        // Validate each feature with PostGIS
        var validatedFeatures = new List<ParsedImportFeature>();
        var allErrors = new List<ImportRowErrorDto>();

        foreach (var feat in rawFeatures)
        {
            await ValidateAndTransformFeatureAsync(conn, feat, request.NetworkId, srid, catalogTypes, ct);

            if (!feat.IsValid)
            {
                allErrors.AddRange(feat.Errors);
            }

            validatedFeatures.Add(feat);
        }

        var validCount = validatedFeatures.Count(f => f.IsValid && !f.IsDuplicate);
        var invalidCount = validatedFeatures.Count(f => !f.IsValid);
        var duplicateCount = validatedFeatures.Count(f => f.IsValid && f.IsDuplicate);

        var previewToken = Guid.NewGuid();
        _previewStore.Store(previewToken, validatedFeatures, srid);

        var sampleRows = validatedFeatures
            .Take(15)
            .Select(f => new ImportPreviewRowDto(
                f.RowIndex,
                f.Name,
                f.FeatureTypeCode,
                f.GeometryKind,
                GetGeometrySummary(f),
                f.Status,
                f.IsValid,
                f.IsDuplicate,
                f.DuplicateReason,
                f.AttributesJson
            ))
            .ToList();

        return new ImportPreviewResultDto(
            previewToken,
            validatedFeatures.Count,
            validCount,
            invalidCount,
            duplicateCount,
            allErrors,
            sampleRows
        );
    }

    public async Task<ImportCommitResultDto> CommitImportAsync(
        ImportCommitRequest request,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        var stored = _previewStore.Retrieve(request.PreviewToken);
        if (stored == null)
        {
            throw new InvalidOperationException("Import preview session has expired or is invalid. Please preview again.");
        }

        var (features, srid) = stored.Value;

        var eligible = features
            .Where(f => f.IsValid && (!request.SkipDuplicates || !f.IsDuplicate))
            .ToList();

        if (eligible.Count == 0)
        {
            throw new InvalidOperationException("No valid features available to import.");
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdClaim, out var u) ? u : Guid.Empty;
        var username = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("unique_name")?.Value ?? "system";
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? "EDITOR";

        var description = $"Import: {eligible.Count} features (SRID: {srid})";

        var userContext = new UserContext(userId, username, role, Guid.NewGuid().ToString("N"));
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description, "NORMAL", ct);

        var importedIds = new List<Guid>();

        foreach (var f in eligible)
        {
            if (f.GeometryKind.Equals("POINT", StringComparison.OrdinalIgnoreCase))
            {
                const string sql = @"
                    INSERT INTO points (
                        id, network_id, feature_type_code, name, status, is_open, attributes,
                        geom, version, created_by, updated_by, created_at, updated_at, external_id, source
                    ) VALUES (
                        @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status, @IsOpen,
                        COALESCE(@AttributesJson::jsonb, '{}'::jsonb),
                        ST_SetSRID(ST_GeomFromGeoJSON(@WktOrGeoJson), 4326),
                        1, @UserId, @UserId, now(), now(), @ExternalId, 'IMPORT'
                    );";

                await uow.Connection.ExecuteAsync(sql, new
                {
                    f.Id,
                    NetworkId = request.NetworkId,
                    f.FeatureTypeCode,
                    f.Name,
                    f.Status,
                    f.IsOpen,
                    f.AttributesJson,
                    f.WktOrGeoJson,
                    UserId = userId
                }, uow.Transaction);
            }
            else if (f.GeometryKind.Equals("LINE", StringComparison.OrdinalIgnoreCase))
            {
                const string sql = @"
                    INSERT INTO lines (
                        id, network_id, feature_type_code, name, status, flow_direction, diameter_mm, material,
                        attributes, geom, version, created_by, updated_by, created_at, updated_at, external_id, source
                    ) VALUES (
                        @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status,
                        @FlowDirection::flow_direction, @DiameterMm, @Material,
                        COALESCE(@AttributesJson::jsonb, '{}'::jsonb),
                        ST_SetSRID(ST_GeomFromGeoJSON(@WktOrGeoJson), 4326),
                        1, @UserId, @UserId, now(), now(), @ExternalId, 'IMPORT'
                    );";

                await uow.Connection.ExecuteAsync(sql, new
                {
                    f.Id,
                    NetworkId = request.NetworkId,
                    f.FeatureTypeCode,
                    f.Name,
                    f.Status,
                    FlowDirection = f.FlowDirection ?? "WITH_DIGITIZED",
                    f.DiameterMm,
                    f.Material,
                    f.AttributesJson,
                    f.WktOrGeoJson,
                    UserId = userId
                }, uow.Transaction);
            }
            else // POLYGON
            {
                const string sql = @"
                    INSERT INTO polygons (
                        id, network_id, feature_type_code, name, status, attributes,
                        geom, version, created_by, updated_by, created_at, updated_at, external_id, source
                    ) VALUES (
                        @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status,
                        COALESCE(@AttributesJson::jsonb, '{}'::jsonb),
                        ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@WktOrGeoJson), 4326)),
                        1, @UserId, @UserId, now(), now(), @ExternalId, 'IMPORT'
                    );";

                await uow.Connection.ExecuteAsync(sql, new
                {
                    f.Id,
                    NetworkId = request.NetworkId,
                    f.FeatureTypeCode,
                    f.Name,
                    f.Status,
                    f.AttributesJson,
                    f.WktOrGeoJson,
                    UserId = userId
                }, uow.Transaction);
            }

            importedIds.Add(f.Id);
        }

        // Commit transaction and clear user redo stack
        await uow.CommitAsync(ct);
        _previewStore.Remove(request.PreviewToken);

        var skippedCount = features.Count - importedIds.Count;

        return new ImportCommitResultDto(
            uow.ChangeSetId,
            importedIds.Count,
            skippedCount,
            importedIds
        );
    }

    private static List<ParsedImportFeature> ParseRawFeatures(string content, string? defaultType)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return ParseGeoJsonFeatures(content, defaultType);
        }
        return ParseCsvFeatures(content, defaultType);
    }

    private static List<ParsedImportFeature> ParseGeoJsonFeatures(string content, string? defaultType)
    {
        var list = new List<ParsedImportFeature>();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        JsonElement featuresArray;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("features", out var feats) && feats.ValueKind == JsonValueKind.Array)
        {
            featuresArray = feats;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            featuresArray = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("geometry", out _))
        {
            // Single Feature
            var singleFeat = ParseSingleGeoJsonFeature(root, 1, defaultType);
            list.Add(singleFeat);
            return list;
        }
        else
        {
            throw new ArgumentException("Invalid GeoJSON: must be a FeatureCollection or Feature array.");
        }

        var rowIndex = 1;
        foreach (var item in featuresArray.EnumerateArray())
        {
            list.Add(ParseSingleGeoJsonFeature(item, rowIndex++, defaultType));
        }

        return list;
    }

    private static ParsedImportFeature ParseSingleGeoJsonFeature(JsonElement el, int rowIndex, string? defaultType)
    {
        var feat = new ParsedImportFeature { RowIndex = rowIndex };

        if (!el.TryGetProperty("geometry", out var geomEl) || geomEl.ValueKind != JsonValueKind.Object)
        {
            feat.Errors.Add(new ImportRowErrorDto(rowIndex, "geometry", "Missing geometry object."));
            return feat;
        }

        feat.WktOrGeoJson = geomEl.GetRawText();
        if (geomEl.TryGetProperty("type", out var typeEl))
        {
            var gType = typeEl.GetString()?.ToUpperInvariant() ?? "";
            feat.GeometryKind = gType switch
            {
                "POINT" => "POINT",
                "LINESTRING" or "MULTILINESTRING" => "LINE",
                "POLYGON" or "MULTIPOLYGON" => "POLYGON",
                _ => gType
            };
        }

        // Properties
        if (el.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            if (props.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                feat.Name = nameProp.GetString() ?? "";
            }
            if (props.TryGetProperty("featureTypeCode", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                feat.FeatureTypeCode = typeProp.GetString()?.ToUpperInvariant() ?? "";
            }
            if (props.TryGetProperty("status", out var statProp) && statProp.ValueKind == JsonValueKind.String)
            {
                feat.Status = statProp.GetString()?.ToUpperInvariant() ?? "ACTIVE";
            }
            if (props.TryGetProperty("externalId", out var extProp) && extProp.ValueKind == JsonValueKind.String)
            {
                feat.ExternalId = extProp.GetString();
            }
            if (props.TryGetProperty("isOpen", out var openProp) && (openProp.ValueKind == JsonValueKind.True || openProp.ValueKind == JsonValueKind.False))
            {
                feat.IsOpen = openProp.GetBoolean();
            }
            if (props.TryGetProperty("flowDirection", out var flowProp) && flowProp.ValueKind == JsonValueKind.String)
            {
                feat.FlowDirection = flowProp.GetString();
            }
            if (props.TryGetProperty("diameterMm", out var diaProp) && diaProp.TryGetInt32(out var dia))
            {
                feat.DiameterMm = dia;
            }
            if (props.TryGetProperty("material", out var matProp) && matProp.ValueKind == JsonValueKind.String)
            {
                feat.Material = matProp.GetString();
            }

            if (props.TryGetProperty("attributes", out var attrProp) && attrProp.ValueKind == JsonValueKind.Object)
            {
                feat.AttributesJson = attrProp.GetRawText();
            }
        }

        if (string.IsNullOrWhiteSpace(feat.FeatureTypeCode) && !string.IsNullOrWhiteSpace(defaultType))
        {
            feat.FeatureTypeCode = defaultType.ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(feat.Name))
        {
            feat.Name = $"{feat.FeatureTypeCode}_{rowIndex}";
        }

        return feat;
    }

    private static List<ParsedImportFeature> ParseCsvFeatures(string content, string? defaultType)
    {
        var list = new List<ParsedImportFeature>();
        var lines = content.Split(NewLineSeparators, StringSplitOptions.None);
        if (lines.Length <= 1) return list;

        var headerCols = ParseCsvLine(lines[0]);
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headerCols.Count; i++)
        {
            colMap[headerCols[i].Trim()] = i;
        }

        var rowIndex = 1;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            var feat = new ParsedImportFeature { RowIndex = rowIndex++ };

            string GetCol(string key) => colMap.TryGetValue(key, out var idx) && idx < cols.Count ? cols[idx].Trim() : "";

            feat.Name = GetCol("name");
            feat.FeatureTypeCode = GetCol("featureTypeCode");
            feat.Status = GetCol("status");
            feat.ExternalId = string.IsNullOrEmpty(GetCol("externalId")) ? null : GetCol("externalId");
            feat.GeometryKind = GetCol("geometryKind").ToUpperInvariant();
            feat.AttributesJson = string.IsNullOrEmpty(GetCol("attributes")) ? null : GetCol("attributes");

            var geomStr = GetCol("geom");
            if (string.IsNullOrEmpty(geomStr))
            {
                // check for x and y columns
                var xStr = GetCol("x");
                var yStr = GetCol("y");
                if (!string.IsNullOrEmpty(xStr) && !string.IsNullOrEmpty(yStr))
                {
                    geomStr = $"{{\"type\":\"Point\",\"coordinates\":[{xStr},{yStr}]}}";
                    feat.GeometryKind = "POINT";
                }
            }

            feat.WktOrGeoJson = geomStr;

            if (bool.TryParse(GetCol("isOpen"), out var isOpen)) feat.IsOpen = isOpen;
            if (!string.IsNullOrEmpty(GetCol("flowDirection"))) feat.FlowDirection = GetCol("flowDirection");
            if (int.TryParse(GetCol("diameterMm"), out var dia)) feat.DiameterMm = dia;
            if (!string.IsNullOrEmpty(GetCol("material"))) feat.Material = GetCol("material");

            if (string.IsNullOrWhiteSpace(feat.FeatureTypeCode) && !string.IsNullOrWhiteSpace(defaultType))
            {
                feat.FeatureTypeCode = defaultType.ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(feat.Name))
            {
                feat.Name = $"{feat.FeatureTypeCode}_{feat.RowIndex}";
            }

            if (string.IsNullOrWhiteSpace(feat.Status))
            {
                feat.Status = "ACTIVE";
            }

            list.Add(feat);
        }

        return list;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    private static async Task ValidateAndTransformFeatureAsync(
        NpgsqlConnection conn,
        ParsedImportFeature feat,
        Guid networkId,
        int srid,
        Dictionary<string, string> catalogTypes,
        CancellationToken ct)
    {
        // 1. Feature type validation
        if (string.IsNullOrWhiteSpace(feat.FeatureTypeCode) || !catalogTypes.TryGetValue(feat.FeatureTypeCode, out var expectedKind))
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "featureTypeCode", $"Unknown or inactive feature type '{feat.FeatureTypeCode}'."));
            feat.IsValid = false;
            return;
        }

        // 2. Geometry kind match
        if (string.IsNullOrWhiteSpace(feat.GeometryKind))
        {
            feat.GeometryKind = expectedKind;
        }
        else if (!feat.GeometryKind.Equals(expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "geometryKind", $"Geometry kind '{feat.GeometryKind}' does not match catalog feature type '{feat.FeatureTypeCode}' which requires '{expectedKind}'."));
            feat.IsValid = false;
            return;
        }

        // 3. Status validation
        if (!AllowedStatuses.Contains(feat.Status))
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "status", $"Invalid status '{feat.Status}'. Allowed: ACTIVE, INACTIVE, PLANNED, DECOMMISSIONED."));
            feat.IsValid = false;
        }

        // 4. Name validation
        if (string.IsNullOrWhiteSpace(feat.Name) || !NameRegex().IsMatch(feat.Name))
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "name", $"Name '{feat.Name}' contains invalid characters or exceeds 120 chars."));
            feat.IsValid = false;
        }

        // 5. Geometry parsing and PostGIS transformation
        if (string.IsNullOrWhiteSpace(feat.WktOrGeoJson))
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "geometry", "Missing geometry coordinates."));
            feat.IsValid = false;
            return;
        }

        try
        {
            var isWkt = !feat.WktOrGeoJson.TrimStart().StartsWith('{');
            var geomExpr = isWkt
                ? $"ST_GeomFromText(@geom, {srid})"
                : $"ST_SetSRID(ST_GeomFromGeoJSON(@geom), {srid})";

            var sqlGeom = $@"
                WITH parsed AS (
                    SELECT CASE WHEN {srid} = 4326 THEN {geomExpr} ELSE ST_Transform({geomExpr}, 4326) END AS geom_4326
                )
                SELECT ST_IsValid(geom_4326) AS IsValid,
                       ST_GeometryType(geom_4326) AS GeomType,
                       ST_AsGeoJSON(geom_4326) AS GeoJson4326,
                       ST_XMin(geom_4326) AS MinX,
                       ST_YMin(geom_4326) AS MinY,
                       ST_XMax(geom_4326) AS MaxX,
                       ST_YMax(geom_4326) AS MaxY
                FROM parsed;";

            var geomCheck = await conn.QueryFirstOrDefaultAsync<GeomValidationRow>(sqlGeom, new { geom = feat.WktOrGeoJson });

            if (geomCheck == null || !geomCheck.IsValid)
            {
                feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "geometry", "Geometry is invalid according to OGC standards."));
                feat.IsValid = false;
                return;
            }

            feat.WktOrGeoJson = geomCheck.GeoJson4326 ?? string.Empty;

            // Bounds check for Egypt roughly: Lon 24..37, Lat 21..32
            double minX = geomCheck.MinX;
            double minY = geomCheck.MinY;
            double maxX = geomCheck.MaxX;
            double maxY = geomCheck.MaxY;

            if (minX < 24.0 || maxX > 37.5 || minY < 21.0 || maxY > 32.5)
            {
                feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "geometry", $"Transformed WGS84 coordinates ({minX:F3}, {minY:F3}) are outside Egypt geographic bounds."));
                feat.IsValid = false;
                return;
            }

            // 6. Duplicate detection
            // A. Duplicate by external_id
            if (!string.IsNullOrEmpty(feat.ExternalId))
            {
                var table = feat.GeometryKind == "POINT" ? "points" : (feat.GeometryKind == "LINE" ? "lines" : "polygons");
                var dupExt = await conn.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(1) FROM {table} WHERE network_id = @networkId AND external_id = @extId;",
                    new { networkId, extId = feat.ExternalId }
                );

                if (dupExt > 0)
                {
                    feat.IsDuplicate = true;
                    feat.DuplicateReason = $"Feature with external ID '{feat.ExternalId}' already exists in workspace.";
                }
            }

            // B. Duplicate by geometry proximity (for points < 0.1m, for lines/polys exact match)
            if (!feat.IsDuplicate)
            {
                if (feat.GeometryKind == "POINT")
                {
                    var dupGeom = await conn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(1) FROM points
                        WHERE network_id = @networkId AND feature_type_code = @featureTypeCode
                          AND ST_DWithin(geom::geography, ST_SetSRID(ST_GeomFromGeoJSON(@geom), 4326)::geography, 0.1);",
                        new { networkId, featureTypeCode = feat.FeatureTypeCode, geom = feat.WktOrGeoJson }
                    );

                    if (dupGeom > 0)
                    {
                        feat.IsDuplicate = true;
                        feat.DuplicateReason = "Identical point asset already exists at this coordinate (<0.1m tolerance).";
                    }
                }
                else if (feat.GeometryKind == "LINE")
                {
                    var dupGeom = await conn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(1) FROM lines
                        WHERE network_id = @networkId AND feature_type_code = @featureTypeCode
                          AND ST_Equals(geom, ST_SetSRID(ST_GeomFromGeoJSON(@geom), 4326));",
                        new { networkId, featureTypeCode = feat.FeatureTypeCode, geom = feat.WktOrGeoJson }
                    );

                    if (dupGeom > 0)
                    {
                        feat.IsDuplicate = true;
                        feat.DuplicateReason = "Identical line asset with identical geometry already exists.";
                    }
                }
            }

            feat.IsValid = feat.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            feat.Errors.Add(new ImportRowErrorDto(feat.RowIndex, "geometry", $"Geometry parsing error: {ex.Message}"));
            feat.IsValid = false;
        }
    }

    private static string GetGeometrySummary(ParsedImportFeature feat)
    {
        try
        {
            using var doc = JsonDocument.Parse(feat.WktOrGeoJson);
            if (doc.RootElement.TryGetProperty("type", out var t))
            {
                var typeStr = t.GetString() ?? "Unknown";
                if (doc.RootElement.TryGetProperty("coordinates", out var coords) && coords.ValueKind == JsonValueKind.Array)
                {
                    if (typeStr == "Point" && coords.GetArrayLength() >= 2)
                    {
                        return $"Point ({coords[0].GetDouble():F4}, {coords[1].GetDouble():F4})";
                    }
                    return $"{typeStr} ({coords.GetArrayLength()} elements)";
                }
                return typeStr;
            }
        }
        catch
        {
            // fallback
        }
        return feat.GeometryKind;
    }
}
