-- 0002_catalog_and_users.sql
-- Create networks, feature_types catalog, users and security tables

CREATE TABLE IF NOT EXISTS networks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(32) NOT NULL UNIQUE,
    name VARCHAR(120) NOT NULL,
    domain VARCHAR(64) NOT NULL,
    is_directed BOOLEAN NOT NULL DEFAULT true,
    snap_tolerance_m DOUBLE PRECISION NOT NULL DEFAULT 0.5,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_networks_code_format CHECK (code ~ '^[A-Z][A-Z0-9_]{1,31}$'),
    CONSTRAINT chk_networks_name_not_empty CHECK (length(trim(name)) > 0),
    CONSTRAINT chk_networks_snap_tolerance_positive CHECK (snap_tolerance_m > 0)
);

CREATE TABLE IF NOT EXISTS feature_types (
    code VARCHAR(32) PRIMARY KEY,
    network_domain VARCHAR(64) NOT NULL,
    geometry_kind geometry_kind NOT NULL,
    display_name VARCHAR(120) NOT NULL,
    topology_role topology_role NOT NULL DEFAULT 'NONE',
    attribute_schema JSONB NOT NULL DEFAULT '{}'::jsonb,
    style JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_active BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT chk_feature_types_code_format CHECK (code ~ '^[A-Z][A-Z0-9_]{1,31}$'),
    CONSTRAINT chk_feature_types_name_not_empty CHECK (length(trim(display_name)) > 0)
);

CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username VARCHAR(32) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role user_role NOT NULL DEFAULT 'VIEWER',
    is_active BOOLEAN NOT NULL DEFAULT true,
    failed_login_count INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ NULL,
    last_login_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_users_username_format CHECK (username ~ '^[A-Za-z0-9_.-]{3,32}$'),
    CONSTRAINT chk_users_email_format CHECK (email ~ '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$')
);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL,
    family_id UUID NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ NULL,
    replaced_by UUID NULL,
    created_ip VARCHAR(45) NULL,
    user_agent VARCHAR(512) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_family ON refresh_tokens (user_id, family_id);

CREATE TABLE IF NOT EXISTS user_network_access (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, network_id)
);
