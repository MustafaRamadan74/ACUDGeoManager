using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Exceptions;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class FeatureRepository : IFeatureRepository
{
    private readonly string _connectionString;

    public FeatureRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    // =========================================================================
    // POINTS
    // =========================================================================

    public async Task<KeysetPagedResult<PointFeatureDto>> GetPointsAsync(FeatureQueryFilter filter, CancellationToken ct = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.NetworkId.HasValue)
        {
            conditions.Add("network_id = @NetworkId");
            parameters.Add("NetworkId", filter.NetworkId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.FeatureTypeCode))
        {
            conditions.Add("upper(feature_type_code) = upper(@TypeCode)");
            parameters.Add("TypeCode", filter.FeatureTypeCode.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("status = @Status::feature_status");
            parameters.Add("Status", filter.Status.Trim().ToUpperInvariant());
        }

        if (filter.MinX.HasValue && filter.MinY.HasValue && filter.MaxX.HasValue && filter.MaxY.HasValue)
        {
            conditions.Add("geom && ST_MakeEnvelope(@MinX, @MinY, @MaxX, @MaxY, 4326)");
            parameters.Add("MinX", filter.MinX.Value);
            parameters.Add("MinY", filter.MinY.Value);
            parameters.Add("MaxX", filter.MaxX.Value);
            parameters.Add("MaxY", filter.MaxY.Value);
        }

        if (filter.AfterId.HasValue)
        {
            conditions.Add("id > @AfterId");
            parameters.Add("AfterId", filter.AfterId.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var limit = Math.Clamp(filter.Limit, 1, 500);
        parameters.Add("Limit", limit + 1);

        var countSql = $"SELECT count(*) FROM points {whereClause};";
        var querySql = $@"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_X(geom) AS X, ST_Y(geom) AS Y, is_open AS IsOpen, node_id AS NodeId,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM points
            {whereClause}
            ORDER BY id ASC
            LIMIT @Limit;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var rawRows = (await conn.QueryAsync<PointDbRow>(new CommandDefinition(querySql, parameters, cancellationToken: ct))).ToList();

        var hasMore = rawRows.Count > limit;
        if (hasMore) rawRows.RemoveAt(rawRows.Count - 1);

        var nextCursor = hasMore && rawRows.Count > 0 ? rawRows[^1].Id : (Guid?)null;
        var dtos = rawRows.Select(r => r.ToDto()).ToList();

        return new KeysetPagedResult<PointFeatureDto>(dtos, nextCursor, hasMore, totalCount);
    }

    public async Task<PointFeatureDto?> GetPointByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_X(geom) AS X, ST_Y(geom) AS Y, is_open AS IsOpen, node_id AS NodeId,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM points WHERE id = @id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<PointDbRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return row?.ToDto();
    }

    public async Task<Guid> CreatePointAsync(CreatePointRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO points (id, network_id, feature_type_code, name, status, attributes, geom, is_open)
            VALUES (@id, @NetworkId, upper(@FeatureTypeCode), @Name, @Status::feature_status, @attributes::jsonb, ST_SetSRID(ST_MakePoint(@X, @Y), 4326), @IsOpen)
            RETURNING id;";

        var id = Guid.NewGuid();
        return await uow.Connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            id,
            req.NetworkId,
            req.FeatureTypeCode,
            req.Name,
            Status = req.Status.ToUpperInvariant(),
            attributes = req.Attributes.GetRawText(),
            req.X,
            req.Y,
            req.IsOpen
        }, transaction: uow.Transaction, cancellationToken: ct));
    }

    public async Task<PointFeatureDto?> UpdatePointAsync(Guid id, UpdatePointRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("id", id);
        parameters.Add("expectedVersion", req.Version);

        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            setClauses.Add("name = @Name");
            parameters.Add("Name", req.Name);
        }

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            setClauses.Add("status = @Status::feature_status");
            parameters.Add("Status", req.Status.ToUpperInvariant());
        }

        if (req.Attributes.HasValue)
        {
            setClauses.Add("attributes = @Attributes::jsonb");
            parameters.Add("Attributes", req.Attributes.Value.GetRawText());
        }

        if (req.X.HasValue && req.Y.HasValue)
        {
            setClauses.Add("geom = ST_SetSRID(ST_MakePoint(@X, @Y), 4326)");
            parameters.Add("X", req.X.Value);
            parameters.Add("Y", req.Y.Value);
        }

        if (req.IsOpen.HasValue)
        {
            setClauses.Add("is_open = @IsOpen");
            parameters.Add("IsOpen", req.IsOpen.Value);
        }

        if (setClauses.Count == 0)
        {
            return await GetPointByIdAsync(id, ct);
        }

        var updateSql = $@"
            UPDATE points 
            SET {string.Join(", ", setClauses)}
            WHERE id = @id AND version = @expectedVersion
            RETURNING id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                      name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                      ST_X(geom) AS X, ST_Y(geom) AS Y, is_open AS IsOpen, node_id AS NodeId,
                      version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                      created_at AS CreatedAt, updated_at AS UpdatedAt;";

        var row = await uow.Connection.QuerySingleOrDefaultAsync<PointDbRow>(
            new CommandDefinition(updateSql, parameters, transaction: uow.Transaction, cancellationToken: ct));

        if (row is null)
        {
            // Check if concurrency conflict or non-existent
            var currentVersion = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM points WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));

            if (currentVersion.HasValue)
            {
                throw new OptimisticConcurrencyException(id, req.Version);
            }
            return null;
        }

        return row.ToDto();
    }

    public async Task<bool> DeletePointAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM points WHERE id = @id AND version = @version;";
        var rows = await uow.Connection.ExecuteAsync(
            new CommandDefinition(sql, new { id, version }, transaction: uow.Transaction, cancellationToken: ct));

        if (rows == 0)
        {
            var exists = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM points WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));
            if (exists.HasValue)
            {
                throw new OptimisticConcurrencyException(id, version);
            }
            return false;
        }

        return true;
    }

    // =========================================================================
    // LINES
    // =========================================================================

    public async Task<KeysetPagedResult<LineFeatureDto>> GetLinesAsync(FeatureQueryFilter filter, CancellationToken ct = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.NetworkId.HasValue)
        {
            conditions.Add("network_id = @NetworkId");
            parameters.Add("NetworkId", filter.NetworkId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.FeatureTypeCode))
        {
            conditions.Add("upper(feature_type_code) = upper(@TypeCode)");
            parameters.Add("TypeCode", filter.FeatureTypeCode.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("status = @Status::feature_status");
            parameters.Add("Status", filter.Status.Trim().ToUpperInvariant());
        }

        if (filter.MinX.HasValue && filter.MinY.HasValue && filter.MaxX.HasValue && filter.MaxY.HasValue)
        {
            conditions.Add("geom && ST_MakeEnvelope(@MinX, @MinY, @MaxX, @MaxY, 4326)");
            parameters.Add("MinX", filter.MinX.Value);
            parameters.Add("MinY", filter.MinY.Value);
            parameters.Add("MaxX", filter.MaxX.Value);
            parameters.Add("MaxY", filter.MaxY.Value);
        }

        if (filter.AfterId.HasValue)
        {
            conditions.Add("id > @AfterId");
            parameters.Add("AfterId", filter.AfterId.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var limit = Math.Clamp(filter.Limit, 1, 500);
        parameters.Add("Limit", limit + 1);

        var countSql = $"SELECT count(*) FROM lines {whereClause};";
        var querySql = $@"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_AsGeoJSON(geom) AS GeoJson, flow_direction::text AS FlowDirection,
                   diameter_mm AS DiameterMm, material AS Material, ST_Length(geom::geography) AS LengthM,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM lines
            {whereClause}
            ORDER BY id ASC
            LIMIT @Limit;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var rawRows = (await conn.QueryAsync<LineDbRow>(new CommandDefinition(querySql, parameters, cancellationToken: ct))).ToList();

        var hasMore = rawRows.Count > limit;
        if (hasMore) rawRows.RemoveAt(rawRows.Count - 1);

        var nextCursor = hasMore && rawRows.Count > 0 ? rawRows[^1].Id : (Guid?)null;
        var dtos = rawRows.Select(r => r.ToDto()).ToList();

        return new KeysetPagedResult<LineFeatureDto>(dtos, nextCursor, hasMore, totalCount);
    }

    public async Task<LineFeatureDto?> GetLineByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_AsGeoJSON(geom) AS GeoJson, flow_direction::text AS FlowDirection,
                   diameter_mm AS DiameterMm, material AS Material, ST_Length(geom::geography) AS LengthM,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM lines WHERE id = @id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<LineDbRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return row?.ToDto();
    }

    public async Task<Guid> CreateLineAsync(CreateLineRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        var lineStringGeoJson = JsonSerializer.Serialize(new
        {
            type = "LineString",
            coordinates = req.Coordinates
        });

        const string sql = @"
            INSERT INTO lines (id, network_id, feature_type_code, name, status, attributes, geom, flow_direction, diameter_mm, material)
            VALUES (@id, @NetworkId, upper(@FeatureTypeCode), @Name, @Status::feature_status, @attributes::jsonb, 
                    ST_SetSRID(ST_GeomFromGeoJSON(@lineStringGeoJson), 4326), 
                    @FlowDirection::flow_direction, @DiameterMm, @Material)
            RETURNING id;";

        var id = Guid.NewGuid();
        return await uow.Connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            id,
            req.NetworkId,
            req.FeatureTypeCode,
            req.Name,
            Status = req.Status.ToUpperInvariant(),
            attributes = req.Attributes.GetRawText(),
            lineStringGeoJson,
            FlowDirection = req.FlowDirection.ToUpperInvariant(),
            req.DiameterMm,
            req.Material
        }, transaction: uow.Transaction, cancellationToken: ct));
    }

    public async Task<LineFeatureDto?> UpdateLineAsync(Guid id, UpdateLineRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("id", id);
        parameters.Add("expectedVersion", req.Version);

        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            setClauses.Add("name = @Name");
            parameters.Add("Name", req.Name);
        }

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            setClauses.Add("status = @Status::feature_status");
            parameters.Add("Status", req.Status.ToUpperInvariant());
        }

        if (req.Attributes.HasValue)
        {
            setClauses.Add("attributes = @Attributes::jsonb");
            parameters.Add("Attributes", req.Attributes.Value.GetRawText());
        }

        if (req.Coordinates != null && req.Coordinates.Count >= 2)
        {
            var lineGeoJson = JsonSerializer.Serialize(new { type = "LineString", coordinates = req.Coordinates });
            setClauses.Add("geom = ST_SetSRID(ST_GeomFromGeoJSON(@LineGeoJson), 4326)");
            parameters.Add("LineGeoJson", lineGeoJson);
        }

        if (!string.IsNullOrWhiteSpace(req.FlowDirection))
        {
            setClauses.Add("flow_direction = @FlowDirection::flow_direction");
            parameters.Add("FlowDirection", req.FlowDirection.ToUpperInvariant());
        }

        if (req.DiameterMm.HasValue)
        {
            setClauses.Add("diameter_mm = @DiameterMm");
            parameters.Add("DiameterMm", req.DiameterMm.Value);
        }

        if (req.Material != null)
        {
            setClauses.Add("material = @Material");
            parameters.Add("Material", req.Material);
        }

        if (setClauses.Count == 0)
        {
            return await GetLineByIdAsync(id, ct);
        }

        var updateSql = $@"
            UPDATE lines 
            SET {string.Join(", ", setClauses)}
            WHERE id = @id AND version = @expectedVersion
            RETURNING id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                      name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                      ST_AsGeoJSON(geom) AS GeoJson, flow_direction::text AS FlowDirection,
                      diameter_mm AS DiameterMm, material AS Material, ST_Length(geom::geography) AS LengthM,
                      version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                      created_at AS CreatedAt, updated_at AS UpdatedAt;";

        var row = await uow.Connection.QuerySingleOrDefaultAsync<LineDbRow>(
            new CommandDefinition(updateSql, parameters, transaction: uow.Transaction, cancellationToken: ct));

        if (row is null)
        {
            var exists = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM lines WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));
            if (exists.HasValue)
            {
                throw new OptimisticConcurrencyException(id, req.Version);
            }
            return null;
        }

        return row.ToDto();
    }

    public async Task<bool> DeleteLineAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM lines WHERE id = @id AND version = @version;";
        var rows = await uow.Connection.ExecuteAsync(
            new CommandDefinition(sql, new { id, version }, transaction: uow.Transaction, cancellationToken: ct));

        if (rows == 0)
        {
            var exists = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM lines WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));
            if (exists.HasValue)
            {
                throw new OptimisticConcurrencyException(id, version);
            }
            return false;
        }

        return true;
    }

    // =========================================================================
    // POLYGONS
    // =========================================================================

    public async Task<KeysetPagedResult<PolygonFeatureDto>> GetPolygonsAsync(FeatureQueryFilter filter, CancellationToken ct = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.NetworkId.HasValue)
        {
            conditions.Add("network_id = @NetworkId");
            parameters.Add("NetworkId", filter.NetworkId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.FeatureTypeCode))
        {
            conditions.Add("upper(feature_type_code) = upper(@TypeCode)");
            parameters.Add("TypeCode", filter.FeatureTypeCode.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("status = @Status::feature_status");
            parameters.Add("Status", filter.Status.Trim().ToUpperInvariant());
        }

        if (filter.MinX.HasValue && filter.MinY.HasValue && filter.MaxX.HasValue && filter.MaxY.HasValue)
        {
            conditions.Add("geom && ST_MakeEnvelope(@MinX, @MinY, @MaxX, @MaxY, 4326)");
            parameters.Add("MinX", filter.MinX.Value);
            parameters.Add("MinY", filter.MinY.Value);
            parameters.Add("MaxX", filter.MaxX.Value);
            parameters.Add("MaxY", filter.MaxY.Value);
        }

        if (filter.AfterId.HasValue)
        {
            conditions.Add("id > @AfterId");
            parameters.Add("AfterId", filter.AfterId.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var limit = Math.Clamp(filter.Limit, 1, 500);
        parameters.Add("Limit", limit + 1);

        var countSql = $"SELECT count(*) FROM polygons {whereClause};";
        var querySql = $@"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_AsGeoJSON(geom) AS GeoJson, ST_Area(geom::geography) AS AreaSqm,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM polygons
            {whereClause}
            ORDER BY id ASC
            LIMIT @Limit;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var rawRows = (await conn.QueryAsync<PolygonDbRow>(new CommandDefinition(querySql, parameters, cancellationToken: ct))).ToList();

        var hasMore = rawRows.Count > limit;
        if (hasMore) rawRows.RemoveAt(rawRows.Count - 1);

        var nextCursor = hasMore && rawRows.Count > 0 ? rawRows[^1].Id : (Guid?)null;
        var dtos = rawRows.Select(r => r.ToDto()).ToList();

        return new KeysetPagedResult<PolygonFeatureDto>(dtos, nextCursor, hasMore, totalCount);
    }

    public async Task<PolygonFeatureDto?> GetPolygonByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                   name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                   ST_AsGeoJSON(geom) AS GeoJson, ST_Area(geom::geography) AS AreaSqm,
                   version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM polygons WHERE id = @id;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<PolygonDbRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return row?.ToDto();
    }

    public async Task<Guid> CreatePolygonAsync(CreatePolygonRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        var polyGeoJson = JsonSerializer.Serialize(new
        {
            type = "Polygon",
            coordinates = req.RingCoordinates
        });

        const string sql = @"
            INSERT INTO polygons (id, network_id, feature_type_code, name, status, attributes, geom)
            VALUES (@id, @NetworkId, upper(@FeatureTypeCode), @Name, @Status::feature_status, @attributes::jsonb, 
                    ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@polyGeoJson), 4326)))
            RETURNING id;";

        var id = Guid.NewGuid();
        return await uow.Connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            id,
            req.NetworkId,
            req.FeatureTypeCode,
            req.Name,
            Status = req.Status.ToUpperInvariant(),
            attributes = req.Attributes.GetRawText(),
            polyGeoJson
        }, transaction: uow.Transaction, cancellationToken: ct));
    }

    public async Task<PolygonFeatureDto?> UpdatePolygonAsync(Guid id, UpdatePolygonRequest req, IUnitOfWork uow, CancellationToken ct = default)
    {
        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("id", id);
        parameters.Add("expectedVersion", req.Version);

        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            setClauses.Add("name = @Name");
            parameters.Add("Name", req.Name);
        }

        if (!string.IsNullOrWhiteSpace(req.Status))
        {
            setClauses.Add("status = @Status::feature_status");
            parameters.Add("Status", req.Status.ToUpperInvariant());
        }

        if (req.Attributes.HasValue)
        {
            setClauses.Add("attributes = @Attributes::jsonb");
            parameters.Add("Attributes", req.Attributes.Value.GetRawText());
        }

        if (req.RingCoordinates != null && req.RingCoordinates.Count > 0)
        {
            var polyGeoJson = JsonSerializer.Serialize(new { type = "Polygon", coordinates = req.RingCoordinates });
            setClauses.Add("geom = ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(@PolyGeoJson), 4326))");
            parameters.Add("PolyGeoJson", polyGeoJson);
        }

        if (setClauses.Count == 0)
        {
            return await GetPolygonByIdAsync(id, ct);
        }

        var updateSql = $@"
            UPDATE polygons 
            SET {string.Join(", ", setClauses)}
            WHERE id = @id AND version = @expectedVersion
            RETURNING id AS Id, network_id AS NetworkId, feature_type_code AS FeatureTypeCode,
                      name AS Name, status::text AS Status, attributes::text AS AttributesJson,
                      ST_AsGeoJSON(geom) AS GeoJson, ST_Area(geom::geography) AS AreaSqm,
                      version AS Version, created_by AS CreatedBy, updated_by AS UpdatedBy,
                      created_at AS CreatedAt, updated_at AS UpdatedAt;";

        var row = await uow.Connection.QuerySingleOrDefaultAsync<PolygonDbRow>(
            new CommandDefinition(updateSql, parameters, transaction: uow.Transaction, cancellationToken: ct));

        if (row is null)
        {
            var exists = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM polygons WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));
            if (exists.HasValue)
            {
                throw new OptimisticConcurrencyException(id, req.Version);
            }
            return null;
        }

        return row.ToDto();
    }

    public async Task<bool> DeletePolygonAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM polygons WHERE id = @id AND version = @version;";
        var rows = await uow.Connection.ExecuteAsync(
            new CommandDefinition(sql, new { id, version }, transaction: uow.Transaction, cancellationToken: ct));

        if (rows == 0)
        {
            var exists = await uow.Connection.ExecuteScalarAsync<int?>(
                new CommandDefinition("SELECT version FROM polygons WHERE id = @id;", new { id }, transaction: uow.Transaction, cancellationToken: ct));
            if (exists.HasValue)
            {
                throw new OptimisticConcurrencyException(id, version);
            }
            return false;
        }

        return true;
    }

    // =========================================================================
    // UNIFIED MAP FEATURES
    // =========================================================================

    public async Task<GeoJsonFeatureCollection> GetMapFeaturesAsync(
        double minX,
        double minY,
        double maxX,
        double maxY,
        IReadOnlyList<Guid>? networkIds,
        IReadOnlyList<string>? typeCodes,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("minX", minX);
        parameters.Add("minY", minY);
        parameters.Add("maxX", maxX);
        parameters.Add("maxY", maxY);
        parameters.Add("limit", Math.Clamp(limit, 1, 5000));

        var netFilter = networkIds != null && networkIds.Count > 0 ? "AND network_id = ANY(@NetIds)" : "";
        if (networkIds != null && networkIds.Count > 0) parameters.Add("NetIds", networkIds.ToArray());

        var typeFilter = typeCodes != null && typeCodes.Count > 0 ? "AND upper(feature_type_code) = ANY(@TypeCodes)" : "";
        if (typeCodes != null && typeCodes.Count > 0) parameters.Add("TypeCodes", typeCodes.Select(t => t.ToUpperInvariant()).ToArray());

        var unionSql = $@"
            (
                SELECT id, network_id, feature_type_code, name, status::text, attributes::text,
                       ST_AsGeoJSON(geom) AS geojson, 'POINT' AS kind
                FROM points
                WHERE geom && ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326)
                {netFilter} {typeFilter}
                LIMIT @limit
            )
            UNION ALL
            (
                SELECT id, network_id, feature_type_code, name, status::text, attributes::text,
                       ST_AsGeoJSON(geom) AS geojson, 'LINE' AS kind
                FROM lines
                WHERE geom && ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326)
                {netFilter} {typeFilter}
                LIMIT @limit
            )
            UNION ALL
            (
                SELECT id, network_id, feature_type_code, name, status::text, attributes::text,
                       ST_AsGeoJSON(geom) AS geojson, 'POLYGON' AS kind
                FROM polygons
                WHERE geom && ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, 4326)
                {netFilter} {typeFilter}
                LIMIT @limit
            );";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<MapFeatureDbRow>(new CommandDefinition(unionSql, parameters, cancellationToken: ct));

        var features = new List<GeoJsonFeature>();
        foreach (var r in rows)
        {
            var parsedGeom = JsonSerializer.Deserialize<GeoJsonGeometryRaw>(r.GeoJson);
            if (parsedGeom != null)
            {
                var geometry = new GeoJsonGeometry(parsedGeom.Type, parsedGeom.Coordinates);
                var properties = new Dictionary<string, object?>
                {
                    ["networkId"] = r.Network_Id,
                    ["featureTypeCode"] = r.Feature_Type_Code,
                    ["name"] = r.Name,
                    ["status"] = r.Status,
                    ["geometryKind"] = r.Kind,
                    ["attributes"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Attributes ?? "{}")
                };

                features.Add(new GeoJsonFeature(r.Id, geometry, properties));
            }
        }

        return new GeoJsonFeatureCollection(features);
    }

    private sealed record PointDbRow(
        Guid Id,
        Guid NetworkId,
        string FeatureTypeCode,
        string Name,
        string Status,
        string AttributesJson,
        double X,
        double Y,
        bool? IsOpen,
        long? NodeId,
        int Version,
        string CreatedBy,
        string UpdatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public PointFeatureDto ToDto() => new(
            Id,
            NetworkId,
            FeatureTypeCode,
            Name,
            Status,
            JsonDocument.Parse(string.IsNullOrWhiteSpace(AttributesJson) ? "{}" : AttributesJson).RootElement.Clone(),
            X,
            Y,
            IsOpen,
            NodeId,
            Version,
            CreatedBy,
            UpdatedBy,
            CreatedAt,
            UpdatedAt);
    }

    private sealed record LineDbRow(
        Guid Id,
        Guid NetworkId,
        string FeatureTypeCode,
        string Name,
        string Status,
        string AttributesJson,
        string GeoJson,
        string FlowDirection,
        int? DiameterMm,
        string? Material,
        double LengthM,
        int Version,
        string CreatedBy,
        string UpdatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public LineFeatureDto ToDto()
        {
            var coordinates = new List<double[]>();
            try
            {
                var parsed = JsonSerializer.Deserialize<GeoJsonCoordsRaw<double[]>>(GeoJson);
                if (parsed?.Coordinates != null) coordinates.AddRange(parsed.Coordinates);
            }
            catch { }

            return new(
                Id,
                NetworkId,
                FeatureTypeCode,
                Name,
                Status,
                JsonDocument.Parse(string.IsNullOrWhiteSpace(AttributesJson) ? "{}" : AttributesJson).RootElement.Clone(),
                coordinates,
                FlowDirection,
                DiameterMm,
                Material,
                LengthM,
                Version,
                CreatedBy,
                UpdatedBy,
                CreatedAt,
                UpdatedAt);
        }
    }

    private sealed record PolygonDbRow(
        Guid Id,
        Guid NetworkId,
        string FeatureTypeCode,
        string Name,
        string Status,
        string AttributesJson,
        string GeoJson,
        double AreaSqm,
        int Version,
        string CreatedBy,
        string UpdatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public PolygonFeatureDto ToDto()
        {
            var coordinates = new List<IReadOnlyList<IReadOnlyList<double[]>>>();
            try
            {
                var parsed = JsonSerializer.Deserialize<GeoJsonCoordsRaw<IReadOnlyList<IReadOnlyList<double[]>>>>(GeoJson);
                if (parsed?.Coordinates != null) coordinates.AddRange(parsed.Coordinates);
            }
            catch { }

            return new(
                Id,
                NetworkId,
                FeatureTypeCode,
                Name,
                Status,
                JsonDocument.Parse(string.IsNullOrWhiteSpace(AttributesJson) ? "{}" : AttributesJson).RootElement.Clone(),
                coordinates,
                AreaSqm,
                Version,
                CreatedBy,
                UpdatedBy,
                CreatedAt,
                UpdatedAt);
        }
    }

    private sealed record MapFeatureDbRow(
        Guid Id,
        Guid Network_Id,
        string Feature_Type_Code,
        string Name,
        string Status,
        string Attributes,
        string GeoJson,
        string Kind);

    private sealed record GeoJsonGeometryRaw(string Type, JsonElement Coordinates);
    private sealed record GeoJsonCoordsRaw<T>(string Type, T[] Coordinates);
}
