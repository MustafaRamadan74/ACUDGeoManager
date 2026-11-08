-- 0006_audit_trigger.sql
-- Create generic audit_trigger function and attach to points, lines, and polygons

CREATE OR REPLACE FUNCTION trg_audit_capture()
RETURNS TRIGGER AS $$
DECLARE
    v_change_set_id UUID;
    v_user_id UUID;
    v_username VARCHAR(64);
    v_role VARCHAR(32);
    v_request_id VARCHAR(128);
    v_reason audit_reason;
    v_action audit_action;
    v_record_id UUID;
    v_old_data JSONB;
    v_new_data JSONB;
    v_user_id_text TEXT;
    v_change_set_text TEXT;
BEGIN
    -- Read transaction-local configuration settings set by UnitOfWork
    v_change_set_text := nullif(current_setting('app.change_set_id', true), '');
    IF v_change_set_text IS NOT NULL THEN
        BEGIN
            v_change_set_id := v_change_set_text::UUID;
        EXCEPTION WHEN OTHERS THEN
            v_change_set_id := NULL;
        END;
    END IF;

    v_user_id_text := nullif(current_setting('app.user_id', true), '');
    IF v_user_id_text IS NOT NULL THEN
        BEGIN
            v_user_id := v_user_id_text::UUID;
        EXCEPTION WHEN OTHERS THEN
            v_user_id := NULL;
        END;
    END IF;

    v_username := coalesce(nullif(current_setting('app.username', true), ''), 'system');
    v_role := coalesce(nullif(current_setting('app.role', true), ''), 'SYSTEM');
    v_request_id := nullif(current_setting('app.request_id', true), '');

    BEGIN
        v_reason := coalesce(nullif(current_setting('app.reason', true), ''), 'NORMAL')::audit_reason;
    EXCEPTION WHEN OTHERS THEN
        v_reason := 'NORMAL'::audit_reason;
    END;

    IF (TG_OP = 'INSERT') THEN
        v_action := 'INSERT'::audit_action;
        v_record_id := NEW.id;
        v_old_data := NULL;
        v_new_data := to_jsonb(NEW);
    ELSIF (TG_OP = 'UPDATE') THEN
        v_action := 'UPDATE'::audit_action;
        v_record_id := NEW.id;
        v_old_data := to_jsonb(OLD);
        v_new_data := to_jsonb(NEW);
    ELSIF (TG_OP = 'DELETE') THEN
        v_action := 'DELETE'::audit_action;
        v_record_id := OLD.id;
        v_old_data := to_jsonb(OLD);
        v_new_data := NULL;
    END IF;

    INSERT INTO audit_log (
        change_set_id,
        table_name,
        record_id,
        action,
        old_data,
        new_data,
        user_id,
        username,
        role,
        changed_at,
        reason,
        request_id
    ) VALUES (
        v_change_set_id,
        TG_TABLE_NAME,
        v_record_id,
        v_action,
        v_old_data,
        v_new_data,
        v_user_id,
        v_username,
        v_role,
        now(),
        v_reason,
        v_request_id
    );

    IF (TG_OP = 'DELETE') THEN
        RETURN OLD;
    ELSE
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- Attach AFTER triggers to points, lines, and polygons
DROP TRIGGER IF EXISTS trg_audit_points ON points;
CREATE TRIGGER trg_audit_points
    AFTER INSERT OR UPDATE OR DELETE ON points
    FOR EACH ROW
    EXECUTE FUNCTION trg_audit_capture();

DROP TRIGGER IF EXISTS trg_audit_lines ON lines;
CREATE TRIGGER trg_audit_lines
    AFTER INSERT OR UPDATE OR DELETE ON lines
    FOR EACH ROW
    EXECUTE FUNCTION trg_audit_capture();

DROP TRIGGER IF EXISTS trg_audit_polygons ON polygons;
CREATE TRIGGER trg_audit_polygons
    AFTER INSERT OR UPDATE OR DELETE ON polygons
    FOR EACH ROW
    EXECUTE FUNCTION trg_audit_capture();
