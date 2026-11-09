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
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}

# Docker Engine runs inside WSL (no Docker Desktop on this machine; see README).
# Make sure the Ubuntu VM and dockerd are up, and the Windows CLI points at it.
function Initialize-Docker {
    if (-not $env:DOCKER_HOST) { $env:DOCKER_HOST = 'tcp://127.0.0.1:2375' }
    $dockerBin = Join-Path $env:USERPROFILE '.docker\bin'
    if ((Test-Path $dockerBin) -and ($env:Path -notlike "*$dockerBin*")) { $env:Path = "$dockerBin;$env:Path" }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "The docker command was not found. See README > Prerequisites."
    }

    docker version --format '{{.Server.Version}}' *> $null
    if ($LASTEXITCODE -ne 0) {
        if (Get-Command wsl -ErrorAction SilentlyContinue) {
            Write-Host "Starting Docker Engine in WSL..." -ForegroundColor Cyan
            wsl -d Ubuntu -u root -- systemctl start docker | Out-Null
            for ($i = 0; $i -lt 30; $i++) {
                docker version --format '{{.Server.Version}}' *> $null
                if ($LASTEXITCODE -eq 0) { break }
                Start-Sleep -Seconds 1
            }
        }
        docker version --format '{{.Server.Version}}' *> $null
        if ($LASTEXITCODE -ne 0) { throw "Docker is not reachable at $env:DOCKER_HOST. Start Docker (Docker Desktop, or 'wsl -d Ubuntu -u root -- systemctl start docker')." }
    }
}

function Wait-Healthy([string]$service, [int]$timeoutSeconds = 90) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $status = docker compose ps --format '{{.Health}}' $service 2>$null
        if ($status -eq 'healthy') { return }
        Start-Sleep -Seconds 2
    }
    throw "$service did not become healthy within $timeoutSeconds seconds. Check: docker compose logs $service"
}
