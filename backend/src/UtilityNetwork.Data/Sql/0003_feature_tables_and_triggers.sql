-- 0003_feature_tables_and_triggers.sql
-- Create points, lines, polygons feature tables, constraints, dual GiST indexes and update trigger

CREATE OR REPLACE FUNCTION trg_set_audit_fields()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    NEW.version = OLD.version + 1;
    NEW.updated_by = coalesce(nullif(current_setting('app.username', true), ''), NEW.updated_by, 'system');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 1. POINTS TABLE
CREATE TABLE IF NOT EXISTS points (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE RESTRICT,
    feature_type_code VARCHAR(32) NOT NULL REFERENCES feature_types(code) ON DELETE RESTRICT,
    name VARCHAR(120) NOT NULL,
    status feature_status NOT NULL DEFAULT 'ACTIVE',
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    geom geometry(Point, 4326) NOT NULL,
    is_open BOOLEAN NULL,
    node_id BIGINT NULL,
    version INT NOT NULL DEFAULT 1,
    created_by VARCHAR(64) NOT NULL DEFAULT 'system',
    updated_by VARCHAR(64) NOT NULL DEFAULT 'system',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_points_geom_valid CHECK (ST_IsValid(geom) AND NOT ST_IsEmpty(geom) AND geometrytype(geom) = 'POINT'),
    CONSTRAINT chk_points_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_points_geom ON points USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_points_geog ON points USING GIST (((geom::geography)));
CREATE INDEX IF NOT EXISTS idx_points_network_type ON points (network_id, feature_type_code);
CREATE INDEX IF NOT EXISTS idx_points_network_status ON points (network_id, status);

DROP TRIGGER IF EXISTS trg_points_audit ON points;
CREATE TRIGGER trg_points_audit
    BEFORE UPDATE ON points
    FOR EACH ROW
    EXECUTE FUNCTION trg_set_audit_fields();

-- 2. LINES TABLE
CREATE TABLE IF NOT EXISTS lines (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE RESTRICT,
    feature_type_code VARCHAR(32) NOT NULL REFERENCES feature_types(code) ON DELETE RESTRICT,
    name VARCHAR(120) NOT NULL,
    status feature_status NOT NULL DEFAULT 'ACTIVE',
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    geom geometry(LineString, 4326) NOT NULL,
    flow_direction flow_direction NOT NULL DEFAULT 'WITH_DIGITIZED',
    diameter_mm INT NULL,
    material VARCHAR(64) NULL,
    version INT NOT NULL DEFAULT 1,
    created_by VARCHAR(64) NOT NULL DEFAULT 'system',
    updated_by VARCHAR(64) NOT NULL DEFAULT 'system',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_lines_geom_valid CHECK (ST_IsValid(geom) AND NOT ST_IsEmpty(geom) AND geometrytype(geom) = 'LINESTRING'),
    CONSTRAINT chk_lines_nonzero_length CHECK (ST_Length(geom::geography) > 0),
    CONSTRAINT chk_lines_name_not_empty CHECK (length(trim(name)) > 0),
    CONSTRAINT chk_lines_pipe_diameter CHECK (feature_type_code <> 'PIPE' OR (diameter_mm IS NOT NULL AND diameter_mm > 0))
);

CREATE INDEX IF NOT EXISTS idx_lines_geom ON lines USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_lines_geog ON lines USING GIST (((geom::geography)));
CREATE INDEX IF NOT EXISTS idx_lines_network_type ON lines (network_id, feature_type_code);
CREATE INDEX IF NOT EXISTS idx_lines_network_status ON lines (network_id, status);

DROP TRIGGER IF EXISTS trg_lines_audit ON lines;
CREATE TRIGGER trg_lines_audit
    BEFORE UPDATE ON lines
    FOR EACH ROW
    EXECUTE FUNCTION trg_set_audit_fields();

-- 3. POLYGONS TABLE
CREATE TABLE IF NOT EXISTS polygons (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE RESTRICT,
    feature_type_code VARCHAR(32) NOT NULL REFERENCES feature_types(code) ON DELETE RESTRICT,
    name VARCHAR(120) NOT NULL,
    status feature_status NOT NULL DEFAULT 'ACTIVE',
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    geom geometry(MultiPolygon, 4326) NOT NULL,
    version INT NOT NULL DEFAULT 1,
    created_by VARCHAR(64) NOT NULL DEFAULT 'system',
    updated_by VARCHAR(64) NOT NULL DEFAULT 'system',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_polygons_geom_valid CHECK (ST_IsValid(geom) AND NOT ST_IsEmpty(geom) AND geometrytype(geom) = 'MULTIPOLYGON'),
    CONSTRAINT chk_polygons_name_not_empty CHECK (length(trim(name)) > 0)
);

CREATE INDEX IF NOT EXISTS idx_polygons_geom ON polygons USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_polygons_geog ON polygons USING GIST (((geom::geography)));
CREATE INDEX IF NOT EXISTS idx_polygons_network_type ON polygons (network_id, feature_type_code);
CREATE INDEX IF NOT EXISTS idx_polygons_network_status ON polygons (network_id, status);

DROP TRIGGER IF EXISTS trg_polygons_audit ON polygons;
CREATE TRIGGER trg_polygons_audit
    BEFORE UPDATE ON polygons
    FOR EACH ROW
    EXECUTE FUNCTION trg_set_audit_fields();
