# Shared helpers for the scripts in this folder. Dot-source it: . "$PSScriptRoot\common.ps1"
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot

function Import-DotEnv {
    $envFile = Join-Path $RepoRoot '.env'
    if (-not (Test-Path $envFile)) {
        Copy-Item (Join-Path $RepoRoot '.env.example') $envFile
        Write-Host "Created .env from .env.example (edit secrets before any real use)." -ForegroundColor Yellow
    }
    $values = @{}
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}

# Puts the installed tools on PATH for this process (they are not always on the machine PATH)
# and keeps NuGet/npm/dotnet caches inside the repository (.local/, git-ignored).
# Nothing is downloaded or installed here.
function Initialize-Tools {
    $candidates = @(
        "$env:ProgramFiles\dotnet",
        "$env:ProgramFiles\nodejs",
        "$env:LOCALAPPDATA\Programs\Git\cmd",
        "$env:ProgramFiles\Git\cmd"
    )
    $pg = Get-ChildItem "$env:ProgramFiles\PostgreSQL" -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Name -replace '\D', '0') } -Descending | Select-Object -First 1
    if ($pg) { $candidates += (Join-Path $pg.FullName 'bin') }
    foreach ($dir in $candidates) {
        if ((Test-Path $dir) -and ($env:Path -notlike "*$dir*")) { $env:Path = "$dir;$env:Path" }
    }

    $local = Join-Path $RepoRoot '.local'
    $env:NUGET_PACKAGES = Join-Path $local 'nuget'
    $env:DOTNET_CLI_HOME = Join-Path $local 'dotnet-home'
    $env:npm_config_cache = Join-Path $local 'npm-cache'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'

    foreach ($tool in 'dotnet', 'node', 'npm') {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "'$tool' was not found. Install it (see README > Prerequisites) and run this script again."
        }
    }
}

# Npgsql connection string for the app database, from .env (POSTGRES_HOST/PORT/DB/USER/PASSWORD).
function Get-ConnectionString([hashtable]$cfg) {
    $dbHost = if ($cfg['POSTGRES_HOST']) { $cfg['POSTGRES_HOST'] } else { 'localhost' }
    $port = if ($cfg['POSTGRES_PORT']) { $cfg['POSTGRES_PORT'] } else { '5432' }
    return "Host=$dbHost;Port=$port;Database=$($cfg['POSTGRES_DB']);Username=$($cfg['POSTGRES_USER']);Password=$($cfg['POSTGRES_PASSWORD'])"
}
