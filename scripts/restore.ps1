# restore.ps1 - ACUD GeoManager PostGIS Restore Utility
param (
    [Parameter(Mandatory=$true)]
    [string]$BackupSqlFile,
    [string]$ContainerName = "acud_postgis_db",
    [string]$DbName = "acud_geom_db",
    [string]$DbUser = "postgres"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupSqlFile)) {
    Write-Error "Backup file not found: $BackupSqlFile"
    exit 1
}

Write-Host "Restoring database '$DbName' on container '$ContainerName' from '$BackupSqlFile'..." -ForegroundColor Yellow

Get-Content $BackupSqlFile -Raw | docker exec -i $ContainerName psql -U $DbUser -d $DbName

Write-Host "Database restore completed successfully!" -ForegroundColor Green

