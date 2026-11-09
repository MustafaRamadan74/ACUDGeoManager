-- 0012_approval_workflow.sql
-- Add APPROVER role, requires_approval column to feature_types, and create pending_changes queue

-- 1. Add APPROVER value to user_role enumeration if not already present
ALTER TYPE user_role ADD VALUE IF NOT EXISTS 'APPROVER';

-- 2. Add requires_approval column to feature_types catalog
ALTER TABLE feature_types
    ADD COLUMN IF NOT EXISTS requires_approval BOOLEAN NOT NULL DEFAULT false;

-- Flag key critical utility transmission layers to require approval
UPDATE feature_types
SET requires_approval = true
WHERE code IN ('WATER_MAIN', 'URBAN_DISTRICT')
  AND requires_approval = false;

-- 3. Create pending_changes table for staged modifications
CREATE TABLE IF NOT EXISTS pending_changes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    network_id UUID NOT NULL REFERENCES networks(id) ON DELETE CASCADE,
    feature_type_code VARCHAR(32) NOT NULL REFERENCES feature_types(code) ON DELETE RESTRICT,
    geometry_kind geometry_kind NOT NULL,
    feature_id UUID NULL,
    operation VARCHAR(16) NOT NULL,
    name VARCHAR(120) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'ACTIVE',
    geom geometry(Geometry, 4326) NULL,
    attributes JSONB NOT NULL DEFAULT '{}'::jsonb,
    is_open BOOLEAN NULL,
    flow_direction VARCHAR(32) NULL,
    diameter_mm INT NULL,
    material VARCHAR(64) NULL,
    target_version INT NULL,
    proposer_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    proposed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    review_status VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    reviewer_user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    reviewed_at TIMESTAMPTZ NULL,
    rejection_reason TEXT NULL,
    CONSTRAINT chk_pending_operation CHECK (operation IN ('INSERT', 'UPDATE', 'DELETE')),
    CONSTRAINT chk_pending_review_status CHECK (review_status IN ('PENDING', 'APPROVED', 'REJECTED'))
);

CREATE INDEX IF NOT EXISTS idx_pending_changes_review_status ON pending_changes (review_status, proposed_at DESC);
CREATE INDEX IF NOT EXISTS idx_pending_changes_network ON pending_changes (network_id);
CREATE INDEX IF NOT EXISTS idx_pending_changes_feature ON pending_changes (feature_id);
CREATE INDEX IF NOT EXISTS idx_pending_changes_geom ON pending_changes USING GIST (geom);
