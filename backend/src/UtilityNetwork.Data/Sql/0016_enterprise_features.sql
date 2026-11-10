-- 0016_enterprise_features.sql
-- Enterprise Capabilities: Feature Attachments, Spatial Relationships, Edit Locking, and Layer Data Quality

-- 1. Attachments: Photos, Drawings, and Engineering Documents linked to features
CREATE TABLE IF NOT EXISTS feature_attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    feature_id UUID NOT NULL,
    feature_table VARCHAR(32) NOT NULL, -- 'points', 'lines', 'polygons'
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(128) NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    file_path VARCHAR(512) NOT NULL,
    description TEXT NULL,
    uploaded_by VARCHAR(64) NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_feature_attachments_feature ON feature_attachments(feature_id);

-- 2. Relationships: Inter-feature linking (Building to Parcel, Valve to Pipe, Pump to Zone)
CREATE TABLE IF NOT EXISTS feature_relationships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_feature_id UUID NOT NULL,
    source_table VARCHAR(32) NOT NULL,
    target_feature_id UUID NOT NULL,
    target_table VARCHAR(32) NOT NULL,
    relationship_type VARCHAR(64) NOT NULL, -- 'CONTAINS', 'SERVES', 'ATTACHED_TO', 'FEEDS', 'ASSOCIATED_WITH'
    description TEXT NULL,
    created_by VARCHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_feature_relationship UNIQUE(source_feature_id, target_feature_id, relationship_type)
);

CREATE INDEX IF NOT EXISTS idx_feature_rel_source ON feature_relationships(source_feature_id);
CREATE INDEX IF NOT EXISTS idx_feature_rel_target ON feature_relationships(target_feature_id);

-- 3. Edit Locking: Optimistic and pessimistic edit locking for multi-user coordination
CREATE TABLE IF NOT EXISTS feature_edit_locks (
    feature_id UUID PRIMARY KEY,
    feature_table VARCHAR(32) NOT NULL,
    locked_by_user_id UUID NOT NULL,
    locked_by_username VARCHAR(64) NOT NULL,
    locked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    client_session_id VARCHAR(64) NULL
);

CREATE INDEX IF NOT EXISTS idx_feature_edit_locks_expires ON feature_edit_locks(expires_at);

-- 4. Stored Procedure for Data Quality and Completeness Dashboard
CREATE OR REPLACE FUNCTION fn_get_data_quality_dashboard(p_network_id UUID)
RETURNS TABLE (
    feature_type_code VARCHAR,
    display_name VARCHAR,
    geometry_kind VARCHAR,
    total_count BIGINT,
    valid_geom_count BIGINT,
    open_issues_count BIGINT,
    completeness_pct NUMERIC,
    attachments_count BIGINT,
    relationships_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    WITH layer_stats AS (
        -- Points
        SELECT
            p.feature_type_code,
            COUNT(*)::bigint AS cnt,
            COUNT(CASE WHEN ST_IsValid(p.geom) THEN 1 END)::bigint AS valid_cnt,
            COUNT(CASE WHEN p.name IS NOT NULL AND p.attributes IS NOT NULL AND p.attributes <> '{}'::jsonb THEN 1 END)::bigint AS attr_complete_cnt
        FROM points p
        WHERE (p_network_id IS NULL OR p.network_id = p_network_id)
        GROUP BY p.feature_type_code

        UNION ALL

        -- Lines
        SELECT
            l.feature_type_code,
            COUNT(*)::bigint AS cnt,
            COUNT(CASE WHEN ST_IsValid(l.geom) THEN 1 END)::bigint AS valid_cnt,
            COUNT(CASE WHEN l.name IS NOT NULL AND l.attributes IS NOT NULL AND l.attributes <> '{}'::jsonb THEN 1 END)::bigint AS attr_complete_cnt
        FROM lines l
        WHERE (p_network_id IS NULL OR l.network_id = p_network_id)
        GROUP BY l.feature_type_code

        UNION ALL

        -- Polygons
        SELECT
            poly.feature_type_code,
            COUNT(*)::bigint AS cnt,
            COUNT(CASE WHEN ST_IsValid(poly.geom) THEN 1 END)::bigint AS valid_cnt,
            COUNT(CASE WHEN poly.name IS NOT NULL AND poly.attributes IS NOT NULL AND poly.attributes <> '{}'::jsonb THEN 1 END)::bigint AS attr_complete_cnt
        FROM polygons poly
        WHERE (p_network_id IS NULL OR poly.network_id = p_network_id)
        GROUP BY poly.feature_type_code
    ),
    layer_issues AS (
        SELECT p.feature_type_code, COUNT(*)::bigint AS open_cnt
        FROM validation_issues vi
        JOIN points p ON vi.feature_id = p.id
        WHERE vi.status = 'OPEN' AND vi.feature_table = 'points'
          AND (p_network_id IS NULL OR p.network_id = p_network_id)
        GROUP BY p.feature_type_code

        UNION ALL

        SELECT l.feature_type_code, COUNT(*)::bigint AS open_cnt
        FROM validation_issues vi
        JOIN lines l ON vi.feature_id = l.id
        WHERE vi.status = 'OPEN' AND vi.feature_table = 'lines'
          AND (p_network_id IS NULL OR l.network_id = p_network_id)
        GROUP BY l.feature_type_code

        UNION ALL

        SELECT poly.feature_type_code, COUNT(*)::bigint AS open_cnt
        FROM validation_issues vi
        JOIN polygons poly ON vi.feature_id = poly.id
        WHERE vi.status = 'OPEN' AND vi.feature_table = 'polygons'
          AND (p_network_id IS NULL OR poly.network_id = p_network_id)
        GROUP BY poly.feature_type_code
    ),
    layer_attachments AS (
        SELECT p.feature_type_code, COUNT(*)::bigint AS att_cnt
        FROM feature_attachments fa
        JOIN points p ON fa.feature_id = p.id
        WHERE fa.feature_table = 'points' AND (p_network_id IS NULL OR p.network_id = p_network_id)
        GROUP BY p.feature_type_code

        UNION ALL

        SELECT l.feature_type_code, COUNT(*)::bigint AS att_cnt
        FROM feature_attachments fa
        JOIN lines l ON fa.feature_id = l.id
        WHERE fa.feature_table = 'lines' AND (p_network_id IS NULL OR l.network_id = p_network_id)
        GROUP BY l.feature_type_code

        UNION ALL

        SELECT poly.feature_type_code, COUNT(*)::bigint AS att_cnt
        FROM feature_attachments fa
        JOIN polygons poly ON fa.feature_id = poly.id
        WHERE fa.feature_table = 'polygons' AND (p_network_id IS NULL OR poly.network_id = p_network_id)
        GROUP BY poly.feature_type_code
    ),
    layer_relationships AS (
        SELECT p.feature_type_code, COUNT(*)::bigint AS rel_cnt
        FROM feature_relationships fr
        JOIN points p ON fr.source_feature_id = p.id
        WHERE fr.source_table = 'points' AND (p_network_id IS NULL OR p.network_id = p_network_id)
        GROUP BY p.feature_type_code

        UNION ALL

        SELECT l.feature_type_code, COUNT(*)::bigint AS rel_cnt
        FROM feature_relationships fr
        JOIN lines l ON fr.source_feature_id = l.id
        WHERE fr.source_table = 'lines' AND (p_network_id IS NULL OR l.network_id = p_network_id)
        GROUP BY l.feature_type_code

        UNION ALL

        SELECT poly.feature_type_code, COUNT(*)::bigint AS rel_cnt
        FROM feature_relationships fr
        JOIN polygons poly ON fr.source_feature_id = poly.id
        WHERE fr.source_table = 'polygons' AND (p_network_id IS NULL OR poly.network_id = p_network_id)
        GROUP BY poly.feature_type_code
    )
    SELECT
        ft.code::varchar AS feature_type_code,
        ft.display_name::varchar AS display_name,
        ft.geometry_kind::varchar AS geometry_kind,
        COALESCE(ls.cnt, 0)::bigint AS total_count,
        COALESCE(ls.valid_cnt, 0)::bigint AS valid_geom_count,
        COALESCE(li.open_cnt, 0)::bigint AS open_issues_count,
        CASE
            WHEN COALESCE(ls.cnt, 0) = 0 THEN 100.0
            ELSE ROUND((COALESCE(ls.attr_complete_cnt, 0)::numeric / ls.cnt::numeric) * 100.0, 1)
        END AS completeness_pct,
        COALESCE(la.att_cnt, 0)::bigint AS attachments_count,
        COALESCE(lr.rel_cnt, 0)::bigint AS relationships_count
    FROM feature_types ft
    LEFT JOIN layer_stats ls ON ft.code = ls.feature_type_code
    LEFT JOIN layer_issues li ON ft.code = li.feature_type_code
    LEFT JOIN layer_attachments la ON ft.code = la.feature_type_code
    LEFT JOIN layer_relationships lr ON ft.code = lr.feature_type_code
    WHERE (p_network_id IS NULL OR ft.network_domain = (SELECT domain FROM networks WHERE id = p_network_id))
    ORDER BY total_count DESC, ft.display_name ASC;
END;
$$ LANGUAGE plpgsql STABLE;
