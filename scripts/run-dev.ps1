<#
.SYNOPSIS
  Start the development environment: the API and the Angular dev server, against the local PostgreSQL.

.DESCRIPTION
  - Database:  the locally installed PostgreSQL (default localhost:5432, database liwa, user liwa).
               Create it once with:  psql -h localhost -U postgres -f scripts/create-db.sql
  - API:       dotnet watch on http://localhost:5080  (Swagger: http://localhost:5080/api/swagger)
  - Web:       ng serve on http://localhost:4200, proxying /api to the API (same origin)
  Each server opens in its own PowerShell window. Close the windows (or Ctrl+C) to stop them.
  On start the API applies migrations and, in Development, loads the demo data.

.PARAMETER NoWeb
  Start only the API.
#>
param([switch]$NoWeb)

. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot

$cfg = Import-DotEnv
Initialize-Tools
$conn = Get-ConnectionString $cfg

$apiCommand = @"
`$env:ASPNETCORE_ENVIRONMENT='Development'
`$env:ConnectionStrings__Liwa='$conn'
`$env:NUGET_PACKAGES='$env:NUGET_PACKAGES'
`$env:DOTNET_CLI_HOME='$env:DOTNET_CLI_HOME'
`$env:Path='$env:Path'
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
    $webCommand = "`$env:Path='$env:Path'; Set-Location '$webDir'; npx ng serve"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $webCommand -WindowStyle Normal
    Write-Host "Web starting on http://localhost:4200" -ForegroundColor Green
}

Write-Host ""
Write-Host "Demo accounts (password Liwa#2027) are listed in README > Demo accounts." -ForegroundColor Gray
