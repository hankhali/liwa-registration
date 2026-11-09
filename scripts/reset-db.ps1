<#
.SYNOPSIS
  Delete the local development database and start a fresh, empty one.

.DESCRIPTION
  Removes the "db" container and its data volume, then starts PostgreSQL again.
  Migrations and demo data are applied the next time the API starts (scripts/dev.ps1).
  This only touches the local Docker volume "liwa_db-data"; it never connects to a shared server.

.PARAMETER Force
  Skip the confirmation prompt.
#>
param([switch]$Force)

. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot

Import-DotEnv | Out-Null
Initialize-Docker

if (-not $Force) {
    $answer = Read-Host "This deletes ALL local database data (volume liwa_db-data). Type 'reset' to continue"
    if ($answer -ne 'reset') { Write-Host "Cancelled."; return }
}

docker compose stop db | Out-Null
docker compose rm -f db | Out-Null
docker volume rm liwa_db-data 2>$null | Out-Null
docker compose up -d db | Out-Null
Wait-Healthy 'db'
Write-Host "Fresh database is ready. Start the API (scripts/dev.ps1) to apply migrations and demo data." -ForegroundColor Green
