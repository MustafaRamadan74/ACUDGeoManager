-- 0009_user_layer_permissions.sql
-- Add department column to users table and create user_layer_permissions table

ALTER TABLE users 
    ADD COLUMN IF NOT EXISTS department VARCHAR(120) NULL;

CREATE TABLE IF NOT EXISTS user_layer_permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    feature_type_code VARCHAR(32) NOT NULL REFERENCES feature_types(code) ON DELETE CASCADE,
    can_read BOOLEAN NOT NULL DEFAULT true,
    can_create BOOLEAN NOT NULL DEFAULT false,
    can_update BOOLEAN NOT NULL DEFAULT false,
    can_delete BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_user_layer_permissions UNIQUE (user_id, feature_type_code)
);

CREATE INDEX IF NOT EXISTS idx_user_layer_perm_user ON user_layer_permissions(user_id);
CREATE INDEX IF NOT EXISTS idx_user_layer_perm_type ON user_layer_permissions(feature_type_code);
