-- 0001_extensions_and_enums.sql
-- Enable PostGIS and UUID extensions and create domain enumerations

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS postgis;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'geometry_kind') THEN
        CREATE TYPE geometry_kind AS ENUM ('POINT', 'LINE', 'POLYGON');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'topology_role') THEN
        CREATE TYPE topology_role AS ENUM ('NONE', 'SOURCE', 'JUNCTION', 'CONTROL', 'TERMINAL', 'CONSUMER');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'flow_direction') THEN
        CREATE TYPE flow_direction AS ENUM ('WITH_DIGITIZED', 'AGAINST_DIGITIZED', 'BIDIRECTIONAL', 'UNKNOWN');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'feature_status') THEN
        CREATE TYPE feature_status AS ENUM ('ACTIVE', 'INACTIVE', 'ABANDONED', 'PLANNED', 'DAMAGED');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'user_role') THEN
        CREATE TYPE user_role AS ENUM ('VIEWER', 'EDITOR', 'ADMIN');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'change_set_status') THEN
        CREATE TYPE change_set_status AS ENUM ('APPLIED', 'UNDONE');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'audit_action') THEN
        CREATE TYPE audit_action AS ENUM ('INSERT', 'UPDATE', 'DELETE');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'audit_reason') THEN
        CREATE TYPE audit_reason AS ENUM ('NORMAL', 'UNDO', 'REDO');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'validation_severity') THEN
        CREATE TYPE validation_severity AS ENUM ('ERROR', 'WARNING', 'INFO');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'issue_status') THEN
        CREATE TYPE issue_status AS ENUM ('OPEN', 'ACKNOWLEDGED', 'RESOLVED');
    END IF;
END $$;
