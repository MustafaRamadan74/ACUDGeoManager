-- 0004_topology_and_validation.sql
-- Create change_sets, audit_log, topology tables, validation tables and bookmarks

CREATE TABLE IF NOT EXISTS change_sets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    description VARCHAR(255) NOT NULL DEFAULT '',
    status change_set_status NOT NULL DEFAULT 'APPLIED'
);

CREATE TABLE IF NOT EXISTS audit_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    change_set_id UUID NULL REFERENCES change_sets(id) ON DELETE SET NULL,
    table_name VARCHAR(32) NOT NULL,
    record_id UUID NOT NULL,
    action audit_action NOT NULL,
    old_data JSONB NULL,
    new_data JSONB NULL,
    user_id UUID NULL,
    username VARCHAR(64) NOT NULL DEFAULT 'system',
    role VARCHAR(32) NOT NULL DEFAULT 'VIEWER',
    changed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    reason audit_reason NOT NULL DEFAULT 'NORMAL',
    request_id VARCHAR(128) NULL
);

CREATE INDEX IF NOT EXISTS idx_audit_log_record ON audit_log (table_name, record_id);
CREATE INDEX IF NOT EXISTS idx_audit_log_user_date ON audit_log (user_id, changed_at);
CREATE INDEX IF NOT EXISTS idx_audit_log_changeset ON audit_log (change_set_id);

-- TOPOLOGY
CREATE TABLE IF NOT EXISTS topology_nodes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE CASCADE,
    geom geometry(Point, 4326) NOT NULL,
    node_role topology_role NOT NULL DEFAULT 'JUNCTION',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_topology_nodes_geom ON topology_nodes USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_topology_nodes_network ON topology_nodes (network_id);

CREATE TABLE IF NOT EXISTS topology_edges (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE CASCADE,
    line_id UUID NOT NULL UNIQUE REFERENCES lines(id) ON DELETE CASCADE,
    from_node BIGINT NOT NULL REFERENCES topology_nodes(id) ON DELETE CASCADE,
    to_node BIGINT NOT NULL REFERENCES topology_nodes(id) ON DELETE CASCADE,
    direction flow_direction NOT NULL DEFAULT 'WITH_DIGITIZED',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_topology_edges_network_from ON topology_edges (network_id, from_node);
CREATE INDEX IF NOT EXISTS idx_topology_edges_network_to ON topology_edges (network_id, to_node);

CREATE TABLE IF NOT EXISTS topology_state (
    network_id UUID PRIMARY KEY REFERENCES networks(id) ON DELETE CASCADE,
    is_dirty BOOLEAN NOT NULL DEFAULT true,
    rebuilt_at TIMESTAMPTZ NULL
);

-- VALIDATION
CREATE TABLE IF NOT EXISTS validation_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE CASCADE,
    started_by VARCHAR(64) NOT NULL DEFAULT 'system',
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    finished_at TIMESTAMPTZ NULL,
    summary JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS idx_validation_runs_network ON validation_runs (network_id, started_at);

CREATE TABLE IF NOT EXISTS validation_issues (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id UUID NOT NULL REFERENCES validation_runs(id) ON DELETE CASCADE,
    rule_code VARCHAR(64) NOT NULL,
    severity validation_severity NOT NULL DEFAULT 'ERROR',
    feature_table VARCHAR(32) NOT NULL,
    feature_id UUID NOT NULL,
    message TEXT NOT NULL,
    geom geometry(Geometry, 4326) NULL,
    status issue_status NOT NULL DEFAULT 'OPEN',
    resolved_by VARCHAR(64) NULL,
    resolved_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS idx_validation_issues_run ON validation_issues (run_id, status);
CREATE INDEX IF NOT EXISTS idx_validation_issues_feature ON validation_issues (feature_id);

-- BOOKMARKS
CREATE TABLE IF NOT EXISTS bookmarks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(120) NOT NULL,
    center_lon DOUBLE PRECISION NOT NULL,
    center_lat DOUBLE PRECISION NOT NULL,
    zoom INT NOT NULL DEFAULT 15,
    bbox JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_bookmarks_user ON bookmarks (user_id);
