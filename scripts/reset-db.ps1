<#
.SYNOPSIS
  Delete the local development database and create a fresh, empty one.

.DESCRIPTION
  Drops and recreates the database named in .env (POSTGRES_DB, default liwa) on the local
  PostgreSQL, connecting as POSTGRES_USER (the database owner). Migrations and demo data are
  applied the next time the API starts (scripts/run-dev.ps1).
  Refuses to run against any host other than localhost / 127.0.0.1.

.PARAMETER Force
  Skip the confirmation prompt.
#>
param([switch]$Force)

. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot

$cfg = Import-DotEnv
Initialize-Tools
$dbHost = if ($cfg['POSTGRES_HOST']) { $cfg['POSTGRES_HOST'] } else { 'localhost' }
$port = if ($cfg['POSTGRES_PORT']) { $cfg['POSTGRES_PORT'] } else { '5432' }
$db = $cfg['POSTGRES_DB']; $user = $cfg['POSTGRES_USER']

if ($dbHost -notin 'localhost', '127.0.0.1', '::1') { throw "reset-db only works on a local database (POSTGRES_HOST is '$dbHost')." }
if ($db -notmatch '^[a-z_][a-z0-9_]*$') { throw "Unexpected database name '$db'." }
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) { throw "psql was not found (PostgreSQL bin folder)." }

if (-not $Force) {
    $answer = Read-Host "This deletes ALL data in database '$db' on $dbHost`:$port. Type 'reset' to continue"
    if ($answer -ne 'reset') { Write-Host "Cancelled."; return }
}

$env:PGPASSWORD = $cfg['POSTGRES_PASSWORD']
try {
    psql -h $dbHost -p $port -U $user -d postgres -w -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS `"$db`" WITH (FORCE);" -c "CREATE DATABASE `"$db`" OWNER `"$user`" ENCODING 'UTF8' TEMPLATE template0;"
    if ($LASTEXITCODE -ne 0) { throw "psql failed." }
} finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }

Write-Host "Fresh database '$db' is ready. Start the API (scripts/run-dev.ps1) to apply migrations and demo data." -ForegroundColor Green
