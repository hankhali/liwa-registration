<#
.SYNOPSIS
  Run every automated test: .NET unit + integration tests and Angular unit tests.

.DESCRIPTION
  Integration tests use the local PostgreSQL: each test fixture creates a uniquely named
  database (liwa_test_xxxxxxxx), migrates it, and drops it afterwards. They connect as the
  role in LIWA_TEST_PG (default Host=localhost;Port=5432;Username=liwa;Password=liwa), which
  needs CREATEDB (scripts/create-db.sql grants it).

.PARAMETER UnitOnly
  Skip the .NET integration tests.
#>
param([switch]$UnitOnly)

. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot
Initialize-Tools

if ($UnitOnly) {
    dotnet test tests/Liwa.UnitTests --nologo
} else {
    dotnet test Liwa.slnx --nologo
}
if ($LASTEXITCODE -ne 0) { throw ".NET tests failed." }

Push-Location (Join-Path $RepoRoot 'web')
try {
    npx ng test --watch=false
    if ($LASTEXITCODE -ne 0) { throw "Angular tests failed." }
} finally { Pop-Location }

Write-Host "All tests passed." -ForegroundColor Green
