#!/usr/bin/env bash
# restore.sh - ACUD GeoManager PostGIS Restore Script
set -euo pipefail

BACKUP_FILE="${1:?Usage: ./restore.sh <backup_file.sql> [container_name] [db_name] [db_user]}"
CONTAINER_NAME="${2:-acud_postgis_db}"
DB_NAME="${3:-acud_geom_db}"
DB_USER="${4:-postgres}"

if [[ ! -f "$BACKUP_FILE" ]]; then
  echo "Error: Backup file '$BACKUP_FILE' does not exist." >&2
  exit 1
fi

echo "Restoring database '$DB_NAME' from '$BACKUP_FILE' into container '$CONTAINER_NAME'..."
docker exec -i "$CONTAINER_NAME" psql -U "$DB_USER" -d "$DB_NAME" < "$BACKUP_FILE"

echo "Database restore completed successfully."

