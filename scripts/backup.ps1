# backup.ps1 - ACUD GeoManager PostGIS & Attachments Backup Utility
param (
    [string]$ContainerName = "acud_postgis_db",
    [string]$DbName = "acud_geom_db",
    [string]$DbUser = "postgres",
    [string]$BackupDir = "backups"
)

$ErrorActionPreference = "Stop"

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}

$dumpFile = Join-Path $BackupDir "acud_dump_$timestamp.sql"
Write-Host "Creating PostGIS backup for '$DbName' from container '$ContainerName'..." -ForegroundColor Cyan

docker exec -i $ContainerName pg_dump -U $DbUser -d $DbName --format=plain --no-owner --no-privileges > $dumpFile

$dumpSize = (Get-Item $dumpFile).Length
Write-Host "Database backup created successfully: $dumpFile ($([math]::Round($dumpSize / 1MB, 2)) MB)" -ForegroundColor Green

# Optional: Archive attachments if uploads exist
$uploadsDir = "backend/src/UtilityNetwork.Api/bin/Debug/net8.0/uploads"
if (Test-Path $uploadsDir) {
    $attZip = Join-Path $BackupDir "acud_attachments_$timestamp.zip"
    Compress-Archive -Path "$uploadsDir/*" -DestinationPath $attZip -Force
    Write-Host "Attachments archive created: $attZip" -ForegroundColor Green
}

Write-Host "Backup process completed at $(Get-Date)" -ForegroundColor Cyan

