#!/usr/bin/env bash
# backup.sh - ACUD GeoManager PostGIS & Attachments Backup Script
set -euo pipefail

CONTAINER_NAME="${1:-acud_postgis_db}"
DB_NAME="${2:-acud_geom_db}"
DB_USER="${3:-postgres}"
BACKUP_DIR="${4:-backups}"

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
mkdir -p "$BACKUP_DIR"

DUMP_FILE="$BACKUP_DIR/acud_dump_${TIMESTAMP}.sql"
echo "Creating PostGIS backup for '$DB_NAME' from container '$CONTAINER_NAME'..."

docker exec -i "$CONTAINER_NAME" pg_dump -U "$DB_USER" -d "$DB_NAME" --format=plain --no-owner --no-privileges > "$DUMP_FILE"

echo "Database backup created: $DUMP_FILE ($(du -h "$DUMP_FILE" | cut -f1))"
echo "Backup process completed."

