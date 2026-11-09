<#
.SYNOPSIS
  Start the development environment: PostgreSQL in Docker, the API and the Angular dev server as native Windows processes.

.DESCRIPTION
  - Database:  docker compose service "db" on 127.0.0.1:<DB_HOST_PORT> (default 5433)
  - API:       dotnet watch on http://localhost:5080  (Swagger: http://localhost:5080/api/swagger)
  - Web:       ng serve on http://localhost:4200, proxying /api to the API
  Each server opens in its own PowerShell window. Close the windows (or Ctrl+C) to stop them.
  The database keeps running; stop it with:  docker compose stop db

.PARAMETER NoWeb
  Start only the database and the API.
#>
param([switch]$NoWeb)

. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot

$cfg = Import-DotEnv
Initialize-Docker

Write-Host "Starting PostgreSQL..." -ForegroundColor Cyan
docker compose up -d db | Out-Null
Wait-Healthy 'db'

$dbPort = if ($cfg['DB_HOST_PORT']) { $cfg['DB_HOST_PORT'] } else { '5433' }
$conn = "Host=127.0.0.1;Port=$dbPort;Database=$($cfg['POSTGRES_DB']);Username=$($cfg['POSTGRES_USER']);Password=$($cfg['POSTGRES_PASSWORD'])"

$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:ConnectionStrings__Liwa='$conn'
Set-Location '$RepoRoot'
dotnet watch --project src/Liwa.Api --no-launch-profile --urls http://localhost:5080
"@
Start-Process powershell -ArgumentList '-NoExit', '-Command', $apiCommand -WindowStyle Normal
Write-Host "API starting on http://localhost:5080 (Swagger at /api/swagger)" -ForegroundColor Green

if (-not $NoWeb) {
    $webDir = Join-Path $RepoRoot 'web'
    if (-not (Test-Path (Join-Path $webDir 'node_modules'))) {
        Write-Host "Installing web dependencies (first run)..." -ForegroundColor Cyan
        Push-Location $webDir; npm ci; Pop-Location
    }
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "Set-Location '$webDir'; npx ng serve" -WindowStyle Normal
    Write-Host "Web starting on http://localhost:4200" -ForegroundColor Green
}

Write-Host ""
Write-Host "Demo accounts (password Liwa#2027): student.new, student.probation, student.honors, student.senior, student.repeat, advisor.it, advisor.business, advisor.media, admin" -ForegroundColor Gray
