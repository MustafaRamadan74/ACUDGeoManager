using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class AnalysisService : IAnalysisService
{
    private readonly string _connectionString;

    public AnalysisService(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<BufferAnalysisResultDto> RunBufferAnalysisAsync(BufferAnalysisRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        string? geoJson = request.GeometryGeoJson;

        if (string.IsNullOrWhiteSpace(geoJson) && request.FeatureId.HasValue)
        {
            const string findGeomSql = @"
                SELECT ST_AsGeoJSON(geom) FROM (
                    SELECT geom FROM points WHERE id = @featureId
                    UNION ALL
                    SELECT geom FROM lines WHERE id = @featureId
                    UNION ALL
                    SELECT geom FROM polygons WHERE id = @featureId
                ) g LIMIT 1;";

            geoJson = await conn.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(findGeomSql, new { featureId = request.FeatureId.Value }, cancellationToken: ct));
        }

        if (string.IsNullOrWhiteSpace(geoJson))
        {
            throw new ArgumentException("Valid GeometryGeoJson or FeatureId must be provided.");
        }

        var radius = Math.Max(1.0, Math.Min(request.DistanceMeters, 50000.0));

        // 1. Calculate Buffer Geometry
        const string bufferSql = @"
            SELECT ST_AsGeoJSON(ST_Buffer(ST_SetSRID(ST_GeomFromGeoJSON(@geoJson), 4326)::geography, @radius)::geometry);";

        var bufferPolygon = await conn.QuerySingleAsync<string>(
            new CommandDefinition(bufferSql, new { geoJson, radius }, cancellationToken: ct));

        // 2. Query intersecting features
        const string intersectSql = @"
            WITH buf AS (
                SELECT ST_Buffer(ST_SetSRID(ST_GeomFromGeoJSON(@geoJson), 4326)::geography, @radius)::geometry AS geom
            )
            SELECT p.id, p.feature_type_code AS FeatureTypeCode, p.name, 'POINT' AS GeometryKind,
                   ST_AsGeoJSON(p.geom) AS GeometryGeoJson, p.status, NULL::float AS LengthMeters, NULL::float AS AreaSqM
            FROM points p, buf
            WHERE ST_Intersects(p.geom, buf.geom)
              AND (@networkId IS NULL OR p.network_id = @networkId)
              AND (@typesCount = 0 OR p.feature_type_code = ANY(@featureTypes))
            UNION ALL
            SELECT l.id, l.feature_type_code AS FeatureTypeCode, l.name, 'LINE' AS GeometryKind,
                   ST_AsGeoJSON(l.geom) AS GeometryGeoJson, l.status, ST_Length(l.geom::geography)::float AS LengthMeters, NULL::float AS AreaSqM
            FROM lines l, buf
            WHERE ST_Intersects(l.geom, buf.geom)
              AND (@networkId IS NULL OR l.network_id = @networkId)
              AND (@typesCount = 0 OR l.feature_type_code = ANY(@featureTypes))
            UNION ALL
            SELECT poly.id, poly.feature_type_code AS FeatureTypeCode, poly.name, 'POLYGON' AS GeometryKind,
                   ST_AsGeoJSON(poly.geom) AS GeometryGeoJson, poly.status, NULL::float AS LengthMeters, ST_Area(poly.geom::geography)::float AS AreaSqM
            FROM polygons poly, buf
            WHERE ST_Intersects(poly.geom, buf.geom)
              AND (@networkId IS NULL OR poly.network_id = @networkId)
              AND (@typesCount = 0 OR poly.feature_type_code = ANY(@featureTypes))
            LIMIT 500;";

        var featureTypes = request.FeatureTypeCodes?.ToArray() ?? Array.Empty<string>();

        var rows = await conn.QueryAsync<IntersectingFeatureItem>(new CommandDefinition(intersectSql, new
        {
            geoJson,
            radius,
            networkId = request.NetworkId,
            featureTypes,
            typesCount = featureTypes.Length
        }, cancellationToken: ct));

        var items = rows.ToList();
        var totalPipeLength = items.Where(i => i.LengthMeters.HasValue).Sum(i => i.LengthMeters!.Value);
        var totalArea = items.Where(i => i.AreaSqM.HasValue).Sum(i => i.AreaSqM!.Value);

        return new BufferAnalysisResultDto(
            bufferPolygon,
            radius,
            items.Count,
            Math.Round(totalPipeLength, 2),
            Math.Round(totalArea, 2),
            items
        );
    }

    public async Task<ServiceAreaResultDto> RunServiceAreaAnalysisAsync(ServiceAreaRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rings = new List<ServiceAreaRingDto>();
        var radii = (request.RadiiMeters ?? new List<double> { 50, 100, 250 }).OrderBy(r => r).ToList();

        foreach (var radius in radii)
        {
            var clampedRadius = Math.Max(1.0, Math.Min(radius, 50000.0));

            const string ringSql = @"
                WITH center AS (
                    SELECT ST_SetSRID(ST_MakePoint(@centerLon, @centerLat), 4326)::geography AS pt
                ),
                buf AS (
                    SELECT ST_Buffer(center.pt, @clampedRadius)::geometry AS geom FROM center
                )
                SELECT 
                    ST_AsGeoJSON(buf.geom) AS PolygonGeoJson,
                    (SELECT COUNT(*) FROM points p, buf WHERE ST_Intersects(p.geom, buf.geom) AND (@networkId IS NULL OR p.network_id = @networkId)) +
                    (SELECT COUNT(*) FROM lines l, buf WHERE ST_Intersects(l.geom, buf.geom) AND (@networkId IS NULL OR l.network_id = @networkId)) AS FeaturesCount,
                    (SELECT COUNT(*) FROM points p, buf WHERE ST_Intersects(p.geom, buf.geom) AND p.feature_type_code LIKE '%VALVE%' AND (@networkId IS NULL OR p.network_id = @networkId)) AS ValvesCount,
                    COALESCE((SELECT SUM(ST_Length(l.geom::geography)) FROM lines l, buf WHERE ST_Intersects(l.geom, buf.geom) AND (@networkId IS NULL OR l.network_id = @networkId)), 0.0) AS PipeLengthMeters
                FROM buf;";

            var result = await conn.QuerySingleAsync<dynamic>(new CommandDefinition(ringSql, new
            {
                centerLon = request.CenterLon,
                centerLat = request.CenterLat,
                clampedRadius,
                networkId = request.NetworkId
            }, cancellationToken: ct));

            rings.Add(new ServiceAreaRingDto(
                clampedRadius,
                (string)result.polygongeojson,
                (int)result.featurescount,
                (int)result.valvescount,
                Math.Round((double)result.pipelengthmeters, 2)
            ));
        }

        return new ServiceAreaResultDto(
            request.CenterLon,
            request.CenterLat,
            rings
        );
    }

    public async Task<SpatialJoinResultDto> RunSpatialJoinAsync(SpatialJoinRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string joinSql = @"
            SELECT poly.id AS PolygonId, poly.name AS PolygonName,
                   ST_AsGeoJSON(poly.geom) AS GeometryGeoJson,
                   (
                       SELECT COUNT(*) FROM points p
                       WHERE p.feature_type_code = @targetTypeCode
                         AND ST_Intersects(p.geom, poly.geom)
                         AND (@networkId IS NULL OR p.network_id = @networkId)
                   ) + (
                       SELECT COUNT(*) FROM lines l
                       WHERE l.feature_type_code = @targetTypeCode
                         AND ST_Intersects(l.geom, poly.geom)
                         AND (@networkId IS NULL OR l.network_id = @networkId)
                   ) AS ContainedCount,
                   COALESCE((
                       SELECT SUM(ST_Length(l.geom::geography)) FROM lines l
                       WHERE l.feature_type_code = @targetTypeCode
                         AND ST_Intersects(l.geom, poly.geom)
                         AND (@networkId IS NULL OR l.network_id = @networkId)
                   ), 0.0) AS TotalContainedLengthMeters
            FROM polygons poly
            WHERE poly.feature_type_code = @polygonTypeCode
              AND (@networkId IS NULL OR poly.network_id = @networkId)
            ORDER BY poly.name ASC;";

        var rows = await conn.QueryAsync<dynamic>(new CommandDefinition(joinSql, new
        {
            polygonTypeCode = request.PolygonTypeCode,
            targetTypeCode = request.TargetTypeCode,
            networkId = request.NetworkId
        }, cancellationToken: ct));

        var items = new List<SpatialJoinItemDto>();
        int totalContained = 0;

        foreach (var r in rows)
        {
            var count = (int)r.containedcount;
            totalContained += count;
            items.Add(new SpatialJoinItemDto(
                (Guid)r.polygonid,
                (string)r.polygonname,
                count,
                Math.Round((double)r.totalcontainedlengthmeters, 2),
                (string)r.geometrygeojson
            ));
        }

        return new SpatialJoinResultDto(
            request.PolygonTypeCode,
            request.TargetTypeCode,
            items.Count,
            totalContained,
            items
        );
    }

    public async Task<NetworkStatisticsDto> GetNetworkStatisticsAsync(Guid networkId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. By Type (Accelerated via Materialized Summary View)
        const string byTypeSql = @"
            SELECT ft.code AS FeatureTypeCode, ft.display_name AS DisplayName, ft.geometry_kind AS GeometryKind,
                   COALESCE(SUM(mv.count), 0)::bigint AS Count,
                   COALESCE(SUM(mv.total_length_m), 0.0) AS TotalLengthMeters,
                   COALESCE(SUM(mv.total_area_sqm), 0.0) AS TotalAreaSqM
            FROM feature_types ft
            LEFT JOIN mv_network_feature_counts mv ON mv.feature_type_code = ft.code AND mv.network_id = @networkId
            WHERE ft.network_domain = (SELECT domain FROM networks WHERE id = @networkId)
            GROUP BY ft.code, ft.display_name, ft.geometry_kind
            ORDER BY Count DESC, ft.display_name ASC;";

        var typeRows = await conn.QueryAsync<dynamic>(new CommandDefinition(byTypeSql, new { networkId }, cancellationToken: ct));
        var byType = typeRows.Select(r => new TypeStatsDto(
            (string)r.featuretypecode,
            (string)r.displayname,
            (string)r.geometrykind,
            (int)r.count,
            Math.Round((double)r.totallengthmeters, 2),
            Math.Round((double)r.totalareasqm, 2)
        )).ToList();

        // 2. By Status
        const string byStatusSql = @"
            SELECT status, COUNT(*) AS Count
            FROM (
                SELECT status FROM points WHERE network_id = @networkId
                UNION ALL
                SELECT status FROM lines WHERE network_id = @networkId
                UNION ALL
                SELECT status FROM polygons WHERE network_id = @networkId
            ) all_f
            GROUP BY status
            ORDER BY Count DESC;";

        var statusRows = await conn.QueryAsync<dynamic>(new CommandDefinition(byStatusSql, new { networkId }, cancellationToken: ct));
        var byStatus = statusRows.Select(r => new StatusStatsDto(
            (string)r.status,
            (int)r.count
        )).ToList();

        // 3. Pipes by Diameter
        const string pipesByDiameterSql = @"
            SELECT COALESCE(NULLIF(attributes->>'diameter_mm', '')::int, NULLIF(attributes->>'diameter', '')::int, 0) AS DiameterMm,
                   COUNT(*) AS Count,
                   COALESCE(SUM(ST_Length(geom::geography)), 0.0) AS TotalLengthMeters
            FROM lines
            WHERE network_id = @networkId
            GROUP BY DiameterMm
            ORDER BY DiameterMm ASC;";

        var pipeRows = await conn.QueryAsync<dynamic>(new CommandDefinition(pipesByDiameterSql, new { networkId }, cancellationToken: ct));
        var pipesByDiameter = pipeRows.Select(r => new PipeDiameterStatsDto(
            (int)r.diametermm,
            (int)r.count,
            Math.Round((double)r.totallengthmeters, 2)
        )).ToList();

        // 4. Valves by State
        const string valvesByStateSql = @"
            SELECT CASE 
                     WHEN (attributes->>'is_open')::boolean IS TRUE THEN 'OPEN'
                     WHEN (attributes->>'is_open')::boolean IS FALSE THEN 'CLOSED'
                     ELSE 'UNKNOWN'
                   END AS State,
                   COUNT(*) AS Count
            FROM points
            WHERE network_id = @networkId AND feature_type_code LIKE '%VALVE%'
            GROUP BY State;";

        var valveRows = await conn.QueryAsync<dynamic>(new CommandDefinition(valvesByStateSql, new { networkId }, cancellationToken: ct));
        var valvesByState = valveRows.Select(r => new ValveStateStatsDto(
            (string)r.state,
            (int)r.count
        )).ToList();

        var totalFeatures = byType.Sum(t => t.Count);
        var totalPipeLength = byType.Sum(t => t.TotalLengthMeters);
        var totalArea = byType.Sum(t => t.TotalAreaSqM);

        return new NetworkStatisticsDto(
            networkId,
            totalFeatures,
            Math.Round(totalPipeLength, 2),
            Math.Round(totalArea, 2),
            byType,
            byStatus,
            pipesByDiameter,
            valvesByState
        );
    }
}
