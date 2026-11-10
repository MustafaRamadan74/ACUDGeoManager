-- 0014_network_trace.sql
-- Network Trace Engine: Upstream, Downstream, and Emergency Isolation Tracing for directed utility networks

-- 1. Create topology_edge_features to attach inline valves and equipment directly to edges
CREATE TABLE IF NOT EXISTS topology_edge_features (
    edge_id BIGINT NOT NULL REFERENCES topology_edges(id) ON DELETE CASCADE,
    point_id UUID NOT NULL REFERENCES points(id) ON DELETE CASCADE,
    distance_m DOUBLE PRECISION NOT NULL DEFAULT 0.0,
    PRIMARY KEY (edge_id, point_id)
);

CREATE INDEX IF NOT EXISTS idx_topology_edge_features_point ON topology_edge_features(point_id);
CREATE INDEX IF NOT EXISTS idx_topology_edge_features_edge ON topology_edge_features(edge_id);

-- Populate topology_edge_features for existing water features
INSERT INTO topology_edge_features (edge_id, point_id, distance_m)
SELECT DISTINCT ON (p.id)
    te.id AS edge_id,
    p.id AS point_id,
    ST_Distance(p.geom::geography, te.geom::geography) AS distance_m
FROM points p
JOIN topology_edges te ON te.network_id = p.network_id AND ST_DWithin(p.geom::geography, te.geom::geography, 2.5)
WHERE p.feature_type_code = 'VALVE'
ON CONFLICT (edge_id, point_id) DO NOTHING;

-- 2. Network Trace Stored Function
CREATE OR REPLACE FUNCTION fn_network_trace(
    p_network_id UUID,
    p_mode VARCHAR, -- 'UPSTREAM', 'DOWNSTREAM', 'ISOLATION'
    p_start_feature_id UUID,
    p_start_kind VARCHAR, -- 'LINE', 'POINT'
    p_max_depth INT DEFAULT 150,
    p_max_edges INT DEFAULT 5000
)
RETURNS jsonb AS $$
DECLARE
    v_start_time TIMESTAMPTZ := clock_timestamp();
    v_is_dirty BOOLEAN := false;
    v_start_edge_id BIGINT := NULL;
    v_start_node_id BIGINT := NULL;
    v_result jsonb;
BEGIN
    -- Check if topology is dirty; auto-rebuild if needed for accurate trace
    SELECT is_dirty INTO v_is_dirty
    FROM topology_state
    WHERE network_id = p_network_id;

    IF v_is_dirty IS TRUE THEN
        PERFORM fn_rebuild_network_topology(p_network_id);
    END IF;

    -- Refresh edge features for valves if needed
    INSERT INTO topology_edge_features (edge_id, point_id, distance_m)
    SELECT DISTINCT ON (p.id)
        te.id AS edge_id,
        p.id AS point_id,
        ST_Distance(p.geom::geography, te.geom::geography) AS distance_m
    FROM points p
    JOIN topology_edges te ON te.network_id = p.network_id AND ST_DWithin(p.geom::geography, te.geom::geography, 2.5)
    WHERE p.network_id = p_network_id
      AND p.feature_type_code = 'VALVE'
    ON CONFLICT (edge_id, point_id) DO NOTHING;

    -- Resolve starting element
    IF UPPER(p_start_kind) = 'LINE' THEN
        SELECT id INTO v_start_edge_id
        FROM topology_edges
        WHERE network_id = p_network_id AND line_id = p_start_feature_id;

        IF v_start_edge_id IS NULL THEN
            -- Fallback to nearest edge
            SELECT te.id INTO v_start_edge_id
            FROM topology_edges te
            CROSS JOIN lines l
            WHERE l.id = p_start_feature_id AND te.network_id = p_network_id
            ORDER BY te.geom <-> l.geom
            LIMIT 1;
        END IF;
    ELSE
        -- Starting kind is POINT
        SELECT node_id INTO v_start_node_id
        FROM topology_node_features
        WHERE point_id = p_start_feature_id
        LIMIT 1;

        IF v_start_node_id IS NULL THEN
            -- Nearest topology node to point
            SELECT tn.id INTO v_start_node_id
            FROM topology_nodes tn
            CROSS JOIN points p
            WHERE p.id = p_start_feature_id AND tn.network_id = p_network_id
            ORDER BY tn.geom <-> p.geom
            LIMIT 1;
        END IF;

        IF v_start_node_id IS NULL THEN
            -- Try resolving through edge
            SELECT te.from_node INTO v_start_node_id
            FROM topology_edges te
            CROSS JOIN points p
            WHERE p.id = p_start_feature_id AND te.network_id = p_network_id
            ORDER BY te.geom <-> p.geom
            LIMIT 1;
        END IF;
    END IF;

    IF v_start_edge_id IS NULL AND v_start_node_id IS NULL THEN
        RETURN jsonb_build_object(
            'networkId', p_network_id,
            'mode', UPPER(p_mode),
            'startFeatureId', p_start_feature_id,
            'startKind', UPPER(p_start_kind),
            'error', 'Start feature not found in topology network',
            'summary', jsonb_build_object(
                'totalLengthM', 0,
                'edgeCount', 0,
                'nodeCount', 0,
                'valvesToCloseCount', 0,
                'affectedZonesCount', 0,
                'executionMs', ROUND(EXTRACT(MILLISECONDS FROM clock_timestamp() - v_start_time)::numeric, 2)
            ),
            'edges', '[]'::jsonb,
            'nodes', '[]'::jsonb,
            'valvesToClose', '[]'::jsonb,
            'affectedPolygons', '[]'::jsonb
        );
    END IF;

    -- =========================================================================
    -- MODE 1: DOWNSTREAM TRACE
    -- =========================================================================
    IF UPPER(p_mode) = 'DOWNSTREAM' THEN
        WITH RECURSIVE trace_cte AS (
            -- Seed from Line
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.direction = 'AGAINST_DIGITIZED' THEN te.from_node
                    ELSE te.to_node
                END AS next_node,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[te.from_node, te.to_node] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_edge_id IS NOT NULL AND te.id = v_start_edge_id

            UNION ALL

            -- Seed from Node
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.from_node = v_start_node_id THEN te.to_node
                    ELSE te.from_node
                END AS next_node,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[v_start_node_id, CASE WHEN te.from_node = v_start_node_id THEN te.to_node ELSE te.from_node END] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_node_id IS NOT NULL
              AND te.network_id = p_network_id
              AND (
                  (te.from_node = v_start_node_id AND te.direction IN ('WITH_DIGITIZED', 'BIDIRECTIONAL'))
                  OR (te.to_node = v_start_node_id AND te.direction IN ('AGAINST_DIGITIZED', 'BIDIRECTIONAL'))
              )
              -- Ensure start node is not blocked by a closed valve
              AND NOT EXISTS (
                  SELECT 1 FROM topology_node_features tnf
                  JOIN points pv ON tnf.point_id = pv.id
                  WHERE tnf.node_id = v_start_node_id AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )

            UNION ALL

            -- Recursive Step
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.from_node = tc.next_node THEN te.to_node
                    ELSE te.from_node
                END AS next_node,
                tc.depth + 1 AS depth,
                tc.visited_edges || te.id AS visited_edges,
                tc.visited_nodes || (CASE WHEN te.from_node = tc.next_node THEN te.to_node ELSE te.from_node END) AS visited_nodes
            FROM trace_cte tc
            JOIN topology_edges te ON (
                (te.from_node = tc.next_node AND te.direction IN ('WITH_DIGITIZED', 'BIDIRECTIONAL'))
                OR (te.to_node = tc.next_node AND te.direction IN ('AGAINST_DIGITIZED', 'BIDIRECTIONAL'))
            )
            WHERE te.network_id = p_network_id
              AND tc.depth < p_max_depth
              AND NOT (te.id = ANY(tc.visited_edges))
              -- Ensure curr_node is not blocked by closed valve
              AND NOT EXISTS (
                  SELECT 1 FROM topology_node_features tnf
                  JOIN points pv ON tnf.point_id = pv.id
                  WHERE tnf.node_id = tc.next_node AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )
              -- Ensure edge does not contain an inline closed valve
              AND NOT EXISTS (
                  SELECT 1 FROM topology_edge_features tef
                  JOIN points pv ON tef.point_id = pv.id
                  WHERE tef.edge_id = te.id AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )
        ),
        distinct_edges AS (
            SELECT DISTINCT edge_id FROM trace_cte LIMIT p_max_edges
        ),
        distinct_nodes AS (
            SELECT DISTINCT unnest(visited_nodes) AS node_id FROM trace_cte
        ),
        edge_data AS (
            SELECT
                te.id,
                te.line_id,
                l.name,
                l.diameter_mm,
                l.material,
                te.direction,
                ROUND(te.length_m::numeric, 2) AS length_m,
                ST_AsGeoJSON(te.geom)::jsonb AS geojson
            FROM distinct_edges de
            JOIN topology_edges te ON de.edge_id = te.id
            LEFT JOIN lines l ON te.line_id = l.id
        ),
        node_data AS (
            SELECT
                tn.id,
                tn.node_role,
                tn.elevation_m,
                ST_AsGeoJSON(tn.geom)::jsonb AS geojson
            FROM distinct_nodes dn
            JOIN topology_nodes tn ON dn.node_id = tn.id
        ),
        affected_zones AS (
            SELECT DISTINCT
                poly.id,
                poly.name,
                poly.feature_type_code,
                ST_AsGeoJSON(poly.geom)::jsonb AS geojson
            FROM polygons poly
            JOIN distinct_edges de ON true
            JOIN topology_edges te ON de.edge_id = te.id
            WHERE poly.network_id = p_network_id
              AND poly.geom && te.geom
              AND ST_Intersects(poly.geom, te.geom)
        )
        SELECT jsonb_build_object(
            'networkId', p_network_id,
            'mode', 'DOWNSTREAM',
            'startFeatureId', p_start_feature_id,
            'startKind', UPPER(p_start_kind),
            'summary', jsonb_build_object(
                'totalLengthM', COALESCE((SELECT ROUND(SUM(length_m)::numeric, 2) FROM edge_data), 0),
                'edgeCount', (SELECT COUNT(*) FROM edge_data),
                'nodeCount', (SELECT COUNT(*) FROM node_data),
                'valvesToCloseCount', 0,
                'affectedZonesCount', (SELECT COUNT(*) FROM affected_zones),
                'executionMs', ROUND(EXTRACT(MILLISECONDS FROM clock_timestamp() - v_start_time)::numeric, 2)
            ),
            'edges', COALESCE((SELECT jsonb_agg(to_jsonb(e)) FROM edge_data e), '[]'::jsonb),
            'nodes', COALESCE((SELECT jsonb_agg(to_jsonb(n)) FROM node_data n), '[]'::jsonb),
            'valvesToClose', '[]'::jsonb,
            'affectedPolygons', COALESCE((SELECT jsonb_agg(to_jsonb(az)) FROM affected_zones az), '[]'::jsonb)
        ) INTO v_result;

        RETURN v_result;

    -- =========================================================================
    -- MODE 2: UPSTREAM TRACE
    -- =========================================================================
    ELSIF UPPER(p_mode) = 'UPSTREAM' THEN
        WITH RECURSIVE trace_cte AS (
            -- Seed from Line
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.direction = 'AGAINST_DIGITIZED' THEN te.to_node
                    ELSE te.from_node
                END AS next_node,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[te.from_node, te.to_node] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_edge_id IS NOT NULL AND te.id = v_start_edge_id

            UNION ALL

            -- Seed from Node
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.to_node = v_start_node_id THEN te.from_node
                    ELSE te.to_node
                END AS next_node,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[v_start_node_id, CASE WHEN te.to_node = v_start_node_id THEN te.from_node ELSE te.to_node END] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_node_id IS NOT NULL
              AND te.network_id = p_network_id
              AND (
                  (te.to_node = v_start_node_id AND te.direction IN ('WITH_DIGITIZED', 'BIDIRECTIONAL'))
                  OR (te.from_node = v_start_node_id AND te.direction IN ('AGAINST_DIGITIZED', 'BIDIRECTIONAL'))
              )
              AND NOT EXISTS (
                  SELECT 1 FROM topology_node_features tnf
                  JOIN points pv ON tnf.point_id = pv.id
                  WHERE tnf.node_id = v_start_node_id AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )

            UNION ALL

            -- Recursive Step
            SELECT
                te.id AS edge_id,
                CASE
                    WHEN te.to_node = tc.next_node THEN te.from_node
                    ELSE te.to_node
                END AS next_node,
                tc.depth + 1 AS depth,
                tc.visited_edges || te.id AS visited_edges,
                tc.visited_nodes || (CASE WHEN te.to_node = tc.next_node THEN te.from_node ELSE te.to_node END) AS visited_nodes
            FROM trace_cte tc
            JOIN topology_edges te ON (
                (te.to_node = tc.next_node AND te.direction IN ('WITH_DIGITIZED', 'BIDIRECTIONAL'))
                OR (te.from_node = tc.next_node AND te.direction IN ('AGAINST_DIGITIZED', 'BIDIRECTIONAL'))
            )
            WHERE te.network_id = p_network_id
              AND tc.depth < p_max_depth
              AND NOT (te.id = ANY(tc.visited_edges))
              AND NOT EXISTS (
                  SELECT 1 FROM topology_node_features tnf
                  JOIN points pv ON tnf.point_id = pv.id
                  WHERE tnf.node_id = tc.next_node AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )
              AND NOT EXISTS (
                  SELECT 1 FROM topology_edge_features tef
                  JOIN points pv ON tef.point_id = pv.id
                  WHERE tef.edge_id = te.id AND pv.feature_type_code = 'VALVE' AND pv.is_open = false
              )
        ),
        distinct_edges AS (
            SELECT DISTINCT edge_id FROM trace_cte LIMIT p_max_edges
        ),
        distinct_nodes AS (
            SELECT DISTINCT unnest(visited_nodes) AS node_id FROM trace_cte
        ),
        edge_data AS (
            SELECT
                te.id,
                te.line_id,
                l.name,
                l.diameter_mm,
                l.material,
                te.direction,
                ROUND(te.length_m::numeric, 2) AS length_m,
                ST_AsGeoJSON(te.geom)::jsonb AS geojson
            FROM distinct_edges de
            JOIN topology_edges te ON de.edge_id = te.id
            LEFT JOIN lines l ON te.line_id = l.id
        ),
        node_data AS (
            SELECT
                tn.id,
                tn.node_role,
                tn.elevation_m,
                ST_AsGeoJSON(tn.geom)::jsonb AS geojson
            FROM distinct_nodes dn
            JOIN topology_nodes tn ON dn.node_id = tn.id
        ),
        affected_zones AS (
            SELECT DISTINCT
                poly.id,
                poly.name,
                poly.feature_type_code,
                ST_AsGeoJSON(poly.geom)::jsonb AS geojson
            FROM polygons poly
            JOIN distinct_edges de ON true
            JOIN topology_edges te ON de.edge_id = te.id
            WHERE poly.network_id = p_network_id
              AND poly.geom && te.geom
              AND ST_Intersects(poly.geom, te.geom)
        )
        SELECT jsonb_build_object(
            'networkId', p_network_id,
            'mode', 'UPSTREAM',
            'startFeatureId', p_start_feature_id,
            'startKind', UPPER(p_start_kind),
            'summary', jsonb_build_object(
                'totalLengthM', COALESCE((SELECT ROUND(SUM(length_m)::numeric, 2) FROM edge_data), 0),
                'edgeCount', (SELECT COUNT(*) FROM edge_data),
                'nodeCount', (SELECT COUNT(*) FROM node_data),
                'valvesToCloseCount', 0,
                'affectedZonesCount', (SELECT COUNT(*) FROM affected_zones),
                'executionMs', ROUND(EXTRACT(MILLISECONDS FROM clock_timestamp() - v_start_time)::numeric, 2)
            ),
            'edges', COALESCE((SELECT jsonb_agg(to_jsonb(e)) FROM edge_data e), '[]'::jsonb),
            'nodes', COALESCE((SELECT jsonb_agg(to_jsonb(n)) FROM node_data n), '[]'::jsonb),
            'valvesToClose', '[]'::jsonb,
            'affectedPolygons', COALESCE((SELECT jsonb_agg(to_jsonb(az)) FROM affected_zones az), '[]'::jsonb)
        ) INTO v_result;

        RETURN v_result;

    -- =========================================================================
    -- MODE 3: EMERGENCY ISOLATION TRACE
    -- =========================================================================
    ELSE
        -- ISOLATION Mode:
        -- Outward bidirectional expansion until hitting boundary operable valves or closed valves
        WITH RECURSIVE iso_cte AS (
            -- Seed from Line
            SELECT
                te.id AS edge_id,
                te.from_node AS node_a,
                te.to_node AS node_b,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[te.from_node, te.to_node] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_edge_id IS NOT NULL AND te.id = v_start_edge_id

            UNION ALL

            -- Seed from Node
            SELECT
                te.id AS edge_id,
                te.from_node AS node_a,
                te.to_node AS node_b,
                1 AS depth,
                ARRAY[te.id] AS visited_edges,
                ARRAY[v_start_node_id, te.from_node, te.to_node] AS visited_nodes
            FROM topology_edges te
            WHERE v_start_node_id IS NOT NULL
              AND te.network_id = p_network_id
              AND (te.from_node = v_start_node_id OR te.to_node = v_start_node_id)

            UNION ALL

            -- Recursive Step: Expand across either endpoint node_a or node_b
            SELECT
                te.id AS edge_id,
                te.from_node AS node_a,
                te.to_node AS node_b,
                ic.depth + 1 AS depth,
                ic.visited_edges || te.id AS visited_edges,
                ic.visited_nodes || te.from_node || te.to_node AS visited_nodes
            FROM iso_cte ic
            JOIN LATERAL (
                SELECT candidate_node
                FROM (VALUES (ic.node_a), (ic.node_b)) AS v(candidate_node)
                WHERE candidate_node IS NOT NULL
            ) cand ON true
            JOIN topology_edges te ON (
                te.network_id = p_network_id
                AND (te.from_node = cand.candidate_node OR te.to_node = cand.candidate_node)
                AND NOT (te.id = ANY(ic.visited_edges))
            )
            WHERE ic.depth < p_max_depth
              -- Do not expand across a node that has a valve (either closed or open-to-close)
              -- UNLESS it is the incident origin node itself!
              AND (
                  cand.candidate_node = v_start_node_id
                  OR NOT EXISTS (
                      SELECT 1 FROM topology_node_features tnf
                      JOIN points pv ON tnf.point_id = pv.id
                      WHERE tnf.node_id = cand.candidate_node AND pv.feature_type_code = 'VALVE'
                  )
              )
              -- Do not expand across an edge if the current edge had an inline valve
              AND (
                  ic.edge_id = v_start_edge_id
                  OR NOT EXISTS (
                      SELECT 1 FROM topology_edge_features tef
                      JOIN points pv ON tef.point_id = pv.id
                      WHERE tef.edge_id = ic.edge_id AND pv.feature_type_code = 'VALVE'
                  )
              )
        ),
        distinct_edges AS (
            SELECT DISTINCT edge_id FROM iso_cte LIMIT p_max_edges
        ),
        distinct_nodes AS (
            SELECT DISTINCT unnest(visited_nodes) AS node_id FROM iso_cte
        ),
        edge_data AS (
            SELECT
                te.id,
                te.line_id,
                l.name,
                l.diameter_mm,
                l.material,
                te.direction,
                ROUND(te.length_m::numeric, 2) AS length_m,
                ST_AsGeoJSON(te.geom)::jsonb AS geojson
            FROM distinct_edges de
            JOIN topology_edges te ON de.edge_id = te.id
            LEFT JOIN lines l ON te.line_id = l.id
        ),
        node_data AS (
            SELECT
                tn.id,
                tn.node_role,
                tn.elevation_m,
                ST_AsGeoJSON(tn.geom)::jsonb AS geojson
            FROM distinct_nodes dn
            JOIN topology_nodes tn ON dn.node_id = tn.id
        ),
        -- Valves to close: Find all operable open valves bounding the isolated nodes or edges
        valves_to_close AS (
            SELECT DISTINCT
                p.id,
                p.name,
                COALESCE(p.attributes->>'valve_type', 'ISOLATION') AS valve_type,
                COALESCE(p.is_open, true) AS is_open,
                p.status,
                jsonb_build_array(ST_X(p.geom), ST_Y(p.geom)) AS coordinates,
                ST_AsGeoJSON(p.geom)::jsonb AS geojson
            FROM points p
            WHERE p.network_id = p_network_id
              AND p.feature_type_code = 'VALVE'
              AND COALESCE(p.is_open, true) = true
              AND p.id <> COALESCE(p_start_feature_id, '00000000-0000-0000-0000-000000000000'::uuid)
              AND (
                  -- Attached to a boundary node of the isolated set
                  EXISTS (
                      SELECT 1 FROM topology_node_features tnf
                      JOIN distinct_nodes dn ON tnf.node_id = dn.node_id
                      WHERE tnf.point_id = p.id
                  )
                  -- Or attached to an edge of the isolated set
                  OR EXISTS (
                      SELECT 1 FROM topology_edge_features tef
                      JOIN distinct_edges de ON tef.edge_id = de.edge_id
                      WHERE tef.point_id = p.id
                  )
                  -- Or spatially within 3 meters of any edge in the isolated set
                  OR EXISTS (
                      SELECT 1 FROM distinct_edges de
                      JOIN topology_edges te ON de.edge_id = te.id
                      WHERE ST_DWithin(p.geom::geography, te.geom::geography, 3.0)
                  )
              )
        ),
        affected_zones AS (
            SELECT DISTINCT
                poly.id,
                poly.name,
                poly.feature_type_code,
                ST_AsGeoJSON(poly.geom)::jsonb AS geojson
            FROM polygons poly
            JOIN distinct_edges de ON true
            JOIN topology_edges te ON de.edge_id = te.id
            WHERE poly.network_id = p_network_id
              AND poly.geom && te.geom
              AND ST_Intersects(poly.geom, te.geom)
        )
        SELECT jsonb_build_object(
            'networkId', p_network_id,
            'mode', 'ISOLATION',
            'startFeatureId', p_start_feature_id,
            'startKind', UPPER(p_start_kind),
            'summary', jsonb_build_object(
                'totalLengthM', COALESCE((SELECT ROUND(SUM(length_m)::numeric, 2) FROM edge_data), 0),
                'edgeCount', (SELECT COUNT(*) FROM edge_data),
                'nodeCount', (SELECT COUNT(*) FROM node_data),
                'valvesToCloseCount', (SELECT COUNT(*) FROM valves_to_close),
                'affectedZonesCount', (SELECT COUNT(*) FROM affected_zones),
                'executionMs', ROUND(EXTRACT(MILLISECONDS FROM clock_timestamp() - v_start_time)::numeric, 2)
            ),
            'edges', COALESCE((SELECT jsonb_agg(to_jsonb(e)) FROM edge_data e), '[]'::jsonb),
            'nodes', COALESCE((SELECT jsonb_agg(to_jsonb(n)) FROM node_data n), '[]'::jsonb),
            'valvesToClose', COALESCE((SELECT jsonb_agg(to_jsonb(v)) FROM valves_to_close v), '[]'::jsonb),
            'affectedPolygons', COALESCE((SELECT jsonb_agg(to_jsonb(az)) FROM affected_zones az), '[]'::jsonb)
        ) INTO v_result;

        RETURN v_result;
    END IF;
END;
$$ LANGUAGE plpgsql;

