-- 0008_add_external_id_and_source.sql
-- Add external_id and source columns to points, lines, polygons for external migrations and seeding

ALTER TABLE points
    ADD COLUMN IF NOT EXISTS external_id VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS source VARCHAR(64) NOT NULL DEFAULT 'SYSTEM';

CREATE INDEX IF NOT EXISTS idx_points_source_external ON points (source, external_id);

ALTER TABLE lines
    ADD COLUMN IF NOT EXISTS external_id VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS source VARCHAR(64) NOT NULL DEFAULT 'SYSTEM';

CREATE INDEX IF NOT EXISTS idx_lines_source_external ON lines (source, external_id);

ALTER TABLE polygons
    ADD COLUMN IF NOT EXISTS external_id VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS source VARCHAR(64) NOT NULL DEFAULT 'SYSTEM';

CREATE INDEX IF NOT EXISTS idx_polygons_source_external ON polygons (source, external_id);
