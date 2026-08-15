<#
.SYNOPSIS
    Starts the local development environment: Postgres, Redis and the API in Docker, the SPA on
    the host with hot reload.

.DESCRIPTION
    This is the *development* counterpart to `docker compose up --build`, which serves the
    production bundle through nginx and has no hot reload.

    The `web` container is stopped first, deliberately: it publishes on host port 5173, which is
    the one origin the API allowlists, and Vite is configured with strictPort. Leaving it running
    would make the dev server fail to bind rather than silently move to 5174 — correct, but
    confusing. Freeing the port is the fix.

.PARAMETER Stop
    Stops the containers this script started and exits. The dev server is a foreground process;
    Ctrl+C ends it.

.EXAMPLE
    ./dev.ps1
.EXAMPLE
    ./dev.ps1 -Stop
#>
[CmdletBinding()]
param([switch]$Stop)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# Everything except `web` — the SPA runs on the host instead.
$Services = @('postgres', 'redis', 'api')

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Warn($message) { Write-Host "  ! $message" -ForegroundColor Yellow }

if ($Stop) {
    Write-Step 'Stopping containers'
    docker compose stop @Services
    exit $LASTEXITCODE
}

# ── Configuration ──────────────────────────────────────────────────────────────────────────────
if (-not (Test-Path '.env')) {
    Write-Step 'Creating .env from .env.example'
    Copy-Item '.env.example' '.env'
}

if ((Get-Content '.env' -Raw) -match 'replace-me-with-at-least-32-bytes') {
    # Long enough to pass validation, so the API will start — but it is a published placeholder.
    Write-Warn 'JWT_SIGNING_KEY in .env is still the example placeholder. Fine locally, never elsewhere.'
}

if (-not (Test-Path 'frontend/.env')) {
    Write-Step 'Creating frontend/.env from frontend/.env.example'
    Copy-Item 'frontend/.env.example' 'frontend/.env'
}

# ── Backing services ───────────────────────────────────────────────────────────────────────────
Write-Step 'Freeing port 5173 (stopping the web container if it is running)'
docker compose stop web 2>&1 | Out-Null

Write-Step "Starting $($Services -join ', ')"

# Two attempts: on a memory-constrained host, Docker Desktop stops containers gracefully — they
# log a clean startup and then exit 0 — and the next attempt usually succeeds.
$started = $false
foreach ($attempt in 1..2) {
    docker compose up -d --wait @Services
    if ($LASTEXITCODE -eq 0) { $started = $true; break }
    Write-Warn "Attempt $attempt did not reach healthy. Retrying..."
    Start-Sleep -Seconds 5
}

if (-not $started) {
    docker compose ps -a --format 'table {{.Service}}\t{{.Status}}'
    Write-Warn 'Containers did not become healthy.'
    Write-Warn 'A container that logs a clean startup and then exits 0 is almost always host memory'
    Write-Warn 'pressure, not configuration. Check `docker compose logs`, free some memory, re-run.'
    exit 1
}

Write-Host ''
Write-Host '  API      http://localhost:8080'
Write-Host '  Swagger  http://localhost:8080/swagger'
Write-Host '  SPA      http://localhost:5173  (starting below, with hot reload)'
Write-Host ''

# ── SPA ────────────────────────────────────────────────────────────────────────────────────────
Set-Location 'frontend'

if (-not (Test-Path 'node_modules')) {
    Write-Step 'Installing frontend dependencies'
    npm install
}

Write-Step 'Starting the Vite dev server — Ctrl+C to stop. Containers keep running; ./dev.ps1 -Stop ends them.'
npm run dev
