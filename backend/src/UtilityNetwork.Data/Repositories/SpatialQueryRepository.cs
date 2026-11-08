using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class SpatialQueryRepository : ISpatialQueryRepository
{
    private readonly string _connectionString;

    public SpatialQueryRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<GeoJsonFeatureCollection> QueryWithinAsync(QueryWithinRequest request, CancellationToken ct = default)
    {
        string polygonJson;
        if (request.Polygon.ValueKind == JsonValueKind.Object && request.Polygon.TryGetProperty("type", out _))
        {
            polygonJson = request.Polygon.GetRawText();
        }
        else if (request.Polygon.ValueKind == JsonValueKind.Array)
        {
            polygonJson = JsonSerializer.Serialize(new
            {
                type = "Polygon",
                coordinates = request.Polygon
            });
        }
        else
        {
            polygonJson = request.Polygon.GetRawText();
        }

        var parameters = new DynamicParameters();
        parameters.Add("PolygonJson", polygonJson);
        parameters.Add("Limit", request.Limit <= 0 ? 500 : request.Limit);

        var netFilter = "";
        if (request.NetworkId.HasValue)
        {
            netFilter = "AND network_id = @NetworkId";
            parameters.Add("NetworkId", request.NetworkId.Value);
        }

        var statusFilter = "";
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            statusFilter = "AND status = @Status";
            parameters.Add("Status", request.Status);
        }

        var typeFilter = "";
        if (request.FeatureTypes != null && request.FeatureTypes.Count > 0)
        {
            typeFilter = "AND upper(feature_type_code) = ANY(@TypeCodes)";
            parameters.Add("TypeCodes", request.FeatureTypes.Select(t => t.ToUpperInvariant()).ToArray());
        }

        var sql = $@"
            WITH query_poly AS (
                SELECT ST_MakeValid(ST_SetSRID(ST_GeomFromGeoJSON(@PolygonJson), 4326)) AS poly
            )
            (
                SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                       name AS Name, status AS Status, 'POINT' AS GeometryKind, version AS Version,
                       is_open AS IsOpen, NULL::text AS FlowDirection, NULL::integer AS DiameterMm, NULL::text AS Material,
                       attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                       0.0 AS DistanceMeters
                FROM points, query_poly
                WHERE ST_Intersects(geom, query_poly.poly)
                  {netFilter} {statusFilter} {typeFilter}
            )
            UNION ALL
            (
                SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                       name AS Name, status AS Status, 'LINE' AS GeometryKind, version AS Version,
                       NULL::boolean AS IsOpen, flow_direction::text AS FlowDirection, diameter_mm AS DiameterMm, material::text AS Material,
                       attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                       0.0 AS DistanceMeters
                FROM lines, query_poly
                WHERE ST_Intersects(geom, query_poly.poly)
                  {netFilter} {statusFilter} {typeFilter}
            )
            UNION ALL
            (
                SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                       name AS Name, status AS Status, 'POLYGON' AS GeometryKind, version AS Version,
                       NULL::boolean AS IsOpen, NULL::text AS FlowDirection, NULL::integer AS DiameterMm, NULL::text AS Material,
                       attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                       0.0 AS DistanceMeters
                FROM polygons, query_poly
                WHERE ST_Intersects(geom, query_poly.poly)
                  {netFilter} {statusFilter} {typeFilter}
            )
            LIMIT @Limit;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<SpatialQueryResultDb>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var features = new List<GeoJsonFeature>();
        foreach (var r in rows)
        {
            var f = r.ToFeature();
            if (f != null) features.Add(f);
        }

        return new GeoJsonFeatureCollection(features);
    }

    public async Task<GeoJsonFeatureCollection> QueryNearestAsync(NearestQueryFilter filter, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("X", filter.X);
        parameters.Add("Y", filter.Y);
        parameters.Add("Limit", filter.Limit <= 0 ? 10 : filter.Limit);

        var netFilter = "";
        if (filter.NetworkId.HasValue)
        {
            netFilter = "AND network_id = @NetworkId";
            parameters.Add("NetworkId", filter.NetworkId.Value);
        }

        var statusFilter = "";
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            statusFilter = "AND status = @Status";
            parameters.Add("Status", filter.Status);
        }

        var typeFilter = "";
        if (!string.IsNullOrWhiteSpace(filter.FeatureType))
        {
            typeFilter = "AND upper(feature_type_code) = upper(@FeatureType)";
            parameters.Add("FeatureType", filter.FeatureType);
        }

        var dwithinFilter = "";
        if (filter.MaxDistanceM.HasValue && filter.MaxDistanceM.Value > 0)
        {
            dwithinFilter = "AND ST_DWithin(geom::geography, query_pt.pt, @MaxDistanceM)";
            parameters.Add("MaxDistanceM", filter.MaxDistanceM.Value);
        }

        var sql = $@"
            WITH query_pt AS (
                SELECT ST_SetSRID(ST_Point(@X, @Y), 4326)::geography AS pt
            ),
            candidates AS (
                (
                    SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                           name AS Name, status AS Status, 'POINT' AS GeometryKind, version AS Version,
                           is_open AS IsOpen, NULL::text AS FlowDirection, NULL::integer AS DiameterMm, NULL::text AS Material,
                           attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                           ROUND(ST_Distance(geom::geography, query_pt.pt)::numeric, 2)::double precision AS DistanceMeters
                    FROM points, query_pt
                    WHERE 1=1 {netFilter} {statusFilter} {typeFilter} {dwithinFilter}
                    ORDER BY geom::geography <-> query_pt.pt
                    LIMIT @Limit
                )
                UNION ALL
                (
                    SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                           name AS Name, status AS Status, 'LINE' AS GeometryKind, version AS Version,
                           NULL::boolean AS IsOpen, flow_direction::text AS FlowDirection, diameter_mm AS DiameterMm, material::text AS Material,
                           attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                           ROUND(ST_Distance(geom::geography, query_pt.pt)::numeric, 2)::double precision AS DistanceMeters
                    FROM lines, query_pt
                    WHERE 1=1 {netFilter} {statusFilter} {typeFilter} {dwithinFilter}
                    ORDER BY geom::geography <-> query_pt.pt
                    LIMIT @Limit
                )
                UNION ALL
                (
                    SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                           name AS Name, status AS Status, 'POLYGON' AS GeometryKind, version AS Version,
                           NULL::boolean AS IsOpen, NULL::text AS FlowDirection, NULL::integer AS DiameterMm, NULL::text AS Material,
                           attributes::text AS AttributesJson, ST_AsGeoJSON(geom) AS GeometryJson,
                           ROUND(ST_Distance(geom::geography, query_pt.pt)::numeric, 2)::double precision AS DistanceMeters
                    FROM polygons, query_pt
                    WHERE 1=1 {netFilter} {statusFilter} {typeFilter} {dwithinFilter}
                    ORDER BY geom::geography <-> query_pt.pt
                    LIMIT @Limit
                )
            )
            SELECT * FROM candidates
            ORDER BY DistanceMeters ASC
            LIMIT @Limit;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<SpatialQueryResultDb>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        var features = new List<GeoJsonFeature>();
        foreach (var r in rows)
        {
            var f = r.ToFeature();
            if (f != null) features.Add(f);
        }

        return new GeoJsonFeatureCollection(features);
    }

    private sealed class SpatialQueryResultDb
    {
        public Guid Id { get; set; }
        public Guid NetworkId { get; set; }
        public string FeatureTypeCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string GeometryKind { get; set; } = "";
        public int Version { get; set; }
        public bool? IsOpen { get; set; }
        public string? FlowDirection { get; set; }
        public int? DiameterMm { get; set; }
        public string? Material { get; set; }
        public string? AttributesJson { get; set; }
        public string? GeometryJson { get; set; }
        public double DistanceMeters { get; set; }

        public GeoJsonFeature? ToFeature()
        {
            if (string.IsNullOrWhiteSpace(GeometryJson)) return null;
            var geomNode = JsonNode.Parse(GeometryJson);
            if (geomNode == null) return null;

            var geomType = geomNode["type"]?.GetValue<string>() ?? "";
            var coordinates = geomNode["coordinates"];
            var geometry = new GeoJsonGeometry(geomType, coordinates!);

            var props = new Dictionary<string, object?>
            {
                ["networkId"] = NetworkId,
                ["featureTypeCode"] = FeatureTypeCode,
                ["name"] = Name,
                ["status"] = Status,
                ["geometryKind"] = GeometryKind,
                ["version"] = Version,
                ["attributes"] = JsonNode.Parse(string.IsNullOrWhiteSpace(AttributesJson) ? "{}" : AttributesJson)
            };

            if (IsOpen.HasValue) props["isOpen"] = IsOpen.Value;
            if (FlowDirection != null) props["flowDirection"] = FlowDirection;
            if (DiameterMm.HasValue) props["diameterMm"] = DiameterMm.Value;
            if (Material != null) props["material"] = Material;
            if (DistanceMeters > 0)
            {
                props["distanceMeters"] = DistanceMeters;
                props["distance_meters"] = DistanceMeters;
            }

            return new GeoJsonFeature(Id, geometry, props);
        }
    }
}
