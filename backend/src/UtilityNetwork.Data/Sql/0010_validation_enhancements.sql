-- 0010_validation_enhancements.sql
-- Add acknowledgment audit tracking and spatial/status indexes for validation issues

ALTER TABLE validation_issues
    ADD COLUMN IF NOT EXISTS acknowledged_by VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS acknowledged_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS idx_validation_issues_geom ON validation_issues USING GIST (geom);
CREATE INDEX IF NOT EXISTS idx_validation_issues_status ON validation_issues (status);
CREATE INDEX IF NOT EXISTS idx_validation_issues_rule ON validation_issues (rule_code);
