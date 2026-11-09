<#
.SYNOPSIS
  Run every automated test: .NET unit + integration tests (throwaway PostgreSQL containers) and Angular unit tests.
#>
. "$PSScriptRoot\common.ps1"
Set-Location $RepoRoot
Initialize-Docker

dotnet test Liwa.slnx --nologo
if ($LASTEXITCODE -ne 0) { throw ".NET tests failed." }

Push-Location (Join-Path $RepoRoot 'web')
try {
    npx ng test --watch=false
    if ($LASTEXITCODE -ne 0) { throw "Angular tests failed." }
} finally { Pop-Location }

Write-Host "All tests passed." -ForegroundColor Green
