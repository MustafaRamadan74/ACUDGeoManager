-- 0013_topology_engine.sql
-- Topology Engine tables, node-feature attachments, state management, rebuild procedure, and dirty tracking triggers

-- 1. Enhance topology_state with metric counts
ALTER TABLE topology_state
    ADD COLUMN IF NOT EXISTS node_count INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS edge_count INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS dirty_feature_count INT NOT NULL DEFAULT 0;

-- Ensure an entry in topology_state exists for every existing network
INSERT INTO topology_state (network_id, is_dirty, node_count, edge_count, dirty_feature_count)
SELECT id, true, 0, 0, 0
FROM networks
ON CONFLICT (network_id) DO NOTHING;

-- 2. Enhance topology_nodes with elevation if needed
ALTER TABLE topology_nodes
    ADD COLUMN IF NOT EXISTS elevation_m DOUBLE PRECISION NULL;

-- 3. Enhance topology_edges with geometry and length
ALTER TABLE topology_edges
    ADD COLUMN IF NOT EXISTS geom geometry(LineString, 4326) NULL,
    ADD COLUMN IF NOT EXISTS length_m DOUBLE PRECISION NOT NULL DEFAULT 0.0;

CREATE INDEX IF NOT EXISTS idx_topology_edges_geom ON topology_edges USING GIST (geom);

-- 4. Create topology_node_features attachment table
CREATE TABLE IF NOT EXISTS topology_node_features (
    node_id BIGINT NOT NULL REFERENCES topology_nodes(id) ON DELETE CASCADE,
    point_id UUID NOT NULL REFERENCES points(id) ON DELETE CASCADE,
    distance_m DOUBLE PRECISION NOT NULL DEFAULT 0.0,
    PRIMARY KEY (node_id, point_id)
);

CREATE INDEX IF NOT EXISTS idx_topology_node_features_point ON topology_node_features (point_id);
CREATE INDEX IF NOT EXISTS idx_topology_node_features_node ON topology_node_features (node_id);

-- 5. Topology Dirty Tracking Triggers
CREATE OR REPLACE FUNCTION trg_mark_topology_dirty()
RETURNS TRIGGER AS $$
DECLARE
    affected_net_id UUID;
BEGIN
    affected_net_id := COALESCE(NEW.network_id, OLD.network_id);
    IF affected_net_id IS NOT NULL THEN
        INSERT INTO topology_state (network_id, is_dirty, dirty_feature_count)
        VALUES (affected_net_id, true, 1)
        ON CONFLICT (network_id)
        DO UPDATE SET
            is_dirty = true,
            dirty_feature_count = topology_state.dirty_feature_count + 1;
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_lines_topology_dirty ON lines;
CREATE TRIGGER trg_lines_topology_dirty
AFTER INSERT OR UPDATE OR DELETE ON lines
FOR EACH ROW EXECUTE FUNCTION trg_mark_topology_dirty();

DROP TRIGGER IF EXISTS trg_points_topology_dirty ON points;
CREATE TRIGGER trg_points_topology_dirty
AFTER INSERT OR UPDATE OR DELETE ON points
FOR EACH ROW EXECUTE FUNCTION trg_mark_topology_dirty();

-- 6. Rebuild Topology Stored Procedure
CREATE OR REPLACE FUNCTION fn_rebuild_network_topology(p_network_id UUID)
RETURNS TABLE (
    network_id UUID,
    is_dirty BOOLEAN,
    node_count INT,
    edge_count INT,
    rebuilt_at TIMESTAMPTZ
) AS $$
DECLARE
    v_snap_tol DOUBLE PRECISION;
    v_nodes_total INT := 0;
    v_edges_total INT := 0;
    v_now TIMESTAMPTZ := clock_timestamp();
BEGIN
    -- Retrieve snap tolerance from network definition (default 0.5m)
    SELECT COALESCE(n.snap_tolerance_m, 0.5) INTO v_snap_tol
    FROM networks n
    WHERE n.id = p_network_id;

    IF v_snap_tol IS NULL OR v_snap_tol <= 0.0 THEN
        v_snap_tol := 0.5;
    END IF;

    -- Clean existing topology for this network
    DELETE FROM topology_edges WHERE topology_edges.network_id = p_network_id;
    DELETE FROM topology_node_features WHERE node_id IN (
        SELECT id FROM topology_nodes WHERE topology_nodes.network_id = p_network_id
    );
    DELETE FROM topology_nodes WHERE topology_nodes.network_id = p_network_id;

    -- Cluster line endpoints in metric projection (EPSG:32636 - UTM Zone 36N for Cairo/ACUD)
    -- and insert clustered centroids as topology_nodes
    INSERT INTO topology_nodes (network_id, geom, node_role, created_at)
    SELECT
        p_network_id,
        ST_SetSRID(ST_Transform(ST_Centroid(ST_Collect(ST_Transform(pts.geom, 32636))), 4326), 4326) AS geom,
        'JUNCTION'::topology_role,
        v_now
    FROM (
        SELECT ST_StartPoint(l.geom) AS geom
        FROM lines l
        WHERE l.network_id = p_network_id
          AND l.geom IS NOT NULL
        UNION ALL
        SELECT ST_EndPoint(l.geom) AS geom
        FROM lines l
        WHERE l.network_id = p_network_id
          AND l.geom IS NOT NULL
    ) pts
    GROUP BY ST_ClusterDBSCAN(ST_Transform(pts.geom, 32636), eps := v_snap_tol, minpoints := 1) OVER ();

    GET DIAGNOSTICS v_nodes_total = ROW_COUNT;

    -- Create edges by connecting each line to nearest start and end topology nodes
    INSERT INTO topology_edges (network_id, line_id, from_node, to_node, direction, geom, length_m, created_at)
    SELECT
        l.network_id,
        l.id,
        fn.id AS from_node,
        tn.id AS to_node,
        l.flow_direction,
        l.geom,
        COALESCE(l.length_m, ST_Length(l.geom::geography)),
        v_now
    FROM lines l
    CROSS JOIN LATERAL (
        SELECT n.id
        FROM topology_nodes n
        WHERE n.network_id = l.network_id
        ORDER BY n.geom <-> ST_StartPoint(l.geom)
        LIMIT 1
    ) fn
    CROSS JOIN LATERAL (
        SELECT n.id
        FROM topology_nodes n
        WHERE n.network_id = l.network_id
        ORDER BY n.geom <-> ST_EndPoint(l.geom)
        LIMIT 1
    ) tn
    WHERE l.network_id = p_network_id
      AND l.geom IS NOT NULL;

    GET DIAGNOSTICS v_edges_total = ROW_COUNT;

    -- Attach point features within tolerance to nearest topology node
    INSERT INTO topology_node_features (node_id, point_id, distance_m)
    SELECT DISTINCT ON (p.id)
        n.id AS node_id,
        p.id AS point_id,
        ST_Distance(p.geom::geography, n.geom::geography) AS distance_m
    FROM points p
    CROSS JOIN LATERAL (
        SELECT tn.id, tn.geom
        FROM topology_nodes tn
        WHERE tn.network_id = p.network_id
        ORDER BY tn.geom <-> p.geom
        LIMIT 1
    ) n
    WHERE p.network_id = p_network_id
      AND p.geom IS NOT NULL
      AND ST_DWithin(p.geom::geography, n.geom::geography, GREATEST(v_snap_tol * 2.0, 2.0))
    ON CONFLICT (node_id, point_id) DO NOTHING;

    -- Update node_role for nodes that have special equipment attached
    UPDATE topology_nodes tn
    SET node_role = ft.topology_role::topology_role
    FROM topology_node_features tnf
    JOIN points p ON tnf.point_id = p.id
    JOIN feature_types ft ON p.feature_type_code = ft.code
    WHERE tn.id = tnf.node_id
      AND tn.network_id = p_network_id
      AND ft.topology_role <> 'JUNCTION';

    -- Update topology state as clean
    INSERT INTO topology_state (network_id, is_dirty, rebuilt_at, node_count, edge_count, dirty_feature_count)
    VALUES (p_network_id, false, v_now, v_nodes_total, v_edges_total, 0)
    ON CONFLICT (network_id)
    DO UPDATE SET
        is_dirty = false,
        rebuilt_at = v_now,
        node_count = v_nodes_total,
        edge_count = v_edges_total,
        dirty_feature_count = 0;

    RETURN QUERY
    SELECT p_network_id, false, v_nodes_total, v_edges_total, v_now;
END;
$$ LANGUAGE plpgsql;

