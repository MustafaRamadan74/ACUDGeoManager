-- 0015_scale_and_materialized_views.sql
-- High performance Vector Tiles (MVT) generation, materialized summaries, and city-scale indexing

-- 1. Materialized View for Network Feature Summaries
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_network_feature_counts AS
SELECT
    COALESCE(p.network_id, l.network_id, poly.network_id) AS network_id,
    COALESCE(p.feature_type_code, l.feature_type_code, poly.feature_type_code) AS feature_type_code,
    COALESCE(p.geometry_kind, l.geometry_kind, poly.geometry_kind) AS geometry_kind,
    COALESCE(p.status, l.status, poly.status) AS status,
    COALESCE(p.cnt, 0) + COALESCE(l.cnt, 0) + COALESCE(poly.cnt, 0) AS count,
    COALESCE(l.total_length_m, 0.0) AS total_length_m,
    COALESCE(poly.total_area_sqm, 0.0) AS total_area_sqm,
    clock_timestamp() AS last_refreshed_at
FROM (
    SELECT network_id, feature_type_code, 'POINT' AS geometry_kind, status, COUNT(*)::bigint AS cnt
    FROM points
    GROUP BY network_id, feature_type_code, status
) p
FULL OUTER JOIN (
    SELECT network_id, feature_type_code, 'LINE' AS geometry_kind, status, COUNT(*)::bigint AS cnt,
           SUM(ST_Length(geom::geography)) AS total_length_m
    FROM lines
    GROUP BY network_id, feature_type_code, status
) l ON p.network_id = l.network_id AND p.feature_type_code = l.feature_type_code AND p.status = l.status
FULL OUTER JOIN (
    SELECT network_id, feature_type_code, 'POLYGON' AS geometry_kind, status, COUNT(*)::bigint AS cnt,
           SUM(ST_Area(geom::geography)) AS total_area_sqm
    FROM polygons
    GROUP BY network_id, feature_type_code, status
) poly ON COALESCE(p.network_id, l.network_id) = poly.network_id
      AND COALESCE(p.feature_type_code, l.feature_type_code) = poly.feature_type_code
      AND COALESCE(p.status, l.status) = poly.status;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_network_feature_counts
ON mv_network_feature_counts(network_id, feature_type_code, geometry_kind, status);

-- Function to refresh the materialized summaries concurrently
CREATE OR REPLACE FUNCTION fn_refresh_materialized_summaries()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY mv_network_feature_counts;
END;
$$ LANGUAGE plpgsql;

-- 2. Stored Function for Fast Vector Tile (MVT) Generation
CREATE OR REPLACE FUNCTION fn_get_vector_tile(
    p_network_id UUID,
    p_z INT,
    p_x INT,
    p_y INT
)
RETURNS bytea AS $$
DECLARE
    v_env_3857 geometry;
    v_env_4326 geometry;
    v_points_mvt bytea;
    v_lines_mvt bytea;
    v_polygons_mvt bytea;
BEGIN
    -- 1. Calculate tile boundary in Web Mercator (EPSG:3857)
    v_env_3857 := ST_TileEnvelope(p_z, p_x, p_y);
    v_env_4326 := ST_Transform(v_env_3857, 4326);

    -- 2. Generate MVT for Points
    WITH mvtgeom AS (
        SELECT
            p.id::text AS id,
            p.feature_type_code,
            p.name,
            p.status,
            p.is_open,
            ST_AsMVTGeom(ST_Transform(p.geom, 3857), v_env_3857, 4096, 64, true) AS geom
        FROM points p
        WHERE (p_network_id IS NULL OR p.network_id = p_network_id)
          AND p.geom && v_env_4326
          AND ST_Intersects(p.geom, v_env_4326)
    )
    SELECT INTO v_points_mvt ST_AsMVT(mvtgeom.*, 'points', 4096, 'geom') FROM mvtgeom;

    -- 3. Generate MVT for Lines
    WITH mvtgeom AS (
        SELECT
            l.id::text AS id,
            l.feature_type_code,
            l.name,
            l.status,
            l.flow_direction,
            l.diameter_mm,
            l.material,
            ST_AsMVTGeom(ST_Transform(l.geom, 3857), v_env_3857, 4096, 64, true) AS geom
        FROM lines l
        WHERE (p_network_id IS NULL OR l.network_id = p_network_id)
          AND l.geom && v_env_4326
          AND ST_Intersects(l.geom, v_env_4326)
    )
    SELECT INTO v_lines_mvt ST_AsMVT(mvtgeom.*, 'lines', 4096, 'geom') FROM mvtgeom;

    -- 4. Generate MVT for Polygons
    WITH mvtgeom AS (
        SELECT
            poly.id::text AS id,
            poly.feature_type_code,
            poly.name,
            poly.status,
            ST_AsMVTGeom(ST_Transform(poly.geom, 3857), v_env_3857, 4096, 64, true) AS geom
        FROM polygons poly
        WHERE (p_network_id IS NULL OR poly.network_id = p_network_id)
          AND poly.geom && v_env_4326
          AND ST_Intersects(poly.geom, v_env_4326)
    )
    SELECT INTO v_polygons_mvt ST_AsMVT(mvtgeom.*, 'polygons', 4096, 'geom') FROM mvtgeom;

    -- Concatenate layers into one MVT buffer
    RETURN COALESCE(v_points_mvt, ''::bytea) ||
           COALESCE(v_lines_mvt, ''::bytea) ||
           COALESCE(v_polygons_mvt, ''::bytea);
END;
$$ LANGUAGE plpgsql STABLE PARALLEL SAFE;

