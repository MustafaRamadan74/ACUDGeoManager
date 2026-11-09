-- 0011_undo_redo_enhancements.sql
-- Add undone_at timestamp to change_sets, performance indexes, and allow version restoration during undo/redo

ALTER TABLE change_sets
    ADD COLUMN IF NOT EXISTS undone_at TIMESTAMPTZ NULL;

CREATE INDEX IF NOT EXISTS idx_change_sets_user_status_created 
    ON change_sets (user_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_change_sets_user_status_undone 
    ON change_sets (user_id, status, undone_at DESC);

-- Update trg_set_audit_fields() to preserve explicit version when app.reason is UNDO or REDO
CREATE OR REPLACE FUNCTION trg_set_audit_fields()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    BEGIN
        IF current_setting('app.reason', true) IN ('UNDO', 'REDO') AND NEW.version IS NOT NULL AND NEW.version <> OLD.version THEN
            -- Allow explicit version restoration during undo/redo
            NULL;
        ELSE
            NEW.version = OLD.version + 1;
        END IF;
    EXCEPTION WHEN OTHERS THEN
        NEW.version = OLD.version + 1;
    END;
    NEW.updated_by = coalesce(nullif(current_setting('app.username', true), ''), NEW.updated_by, 'system');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
