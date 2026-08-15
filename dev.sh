#!/usr/bin/env bash
#
# Starts the local development environment: Postgres, Redis and the API in Docker, the SPA on the
# host with hot reload. The POSIX twin of dev.ps1 — same behaviour, for bash/zsh (Git Bash, WSL,
# macOS, Linux).
#
#   ./dev.sh          start
#   ./dev.sh --stop   stop the containers this script started
#
# This is the *development* counterpart to `docker compose up --build`, which serves the production
# bundle through nginx and has no hot reload.
set -euo pipefail

cd "$(dirname "$0")"

# Everything except `web` — the SPA runs on the host instead.
SERVICES=(postgres redis api)

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }
warn() { printf '\033[33m  ! %s\033[0m\n' "$1"; }

if [[ "${1:-}" == "--stop" ]]; then
  step 'Stopping containers'
  exec docker compose stop "${SERVICES[@]}"
fi

# ── Configuration ──────────────────────────────────────────────────────────────────────────────
if [[ ! -f .env ]]; then
  step 'Creating .env from .env.example'
  cp .env.example .env
fi

if grep -q 'replace-me-with-at-least-32-bytes' .env; then
  # Long enough to pass validation, so the API will start — but it is a published placeholder.
  warn 'JWT_SIGNING_KEY in .env is still the example placeholder. Fine locally, never elsewhere.'
fi

if [[ ! -f frontend/.env ]]; then
  step 'Creating frontend/.env from frontend/.env.example'
  cp frontend/.env.example frontend/.env
fi

# ── Backing services ───────────────────────────────────────────────────────────────────────────
# The web container publishes on host port 5173, which is the one origin the API allowlists, and
# Vite runs with strictPort. Leaving it up would make the dev server refuse to bind rather than
# silently move to 5174 — correct, but confusing. Free the port instead.
step 'Freeing port 5173 (stopping the web container if it is running)'
docker compose stop web >/dev/null 2>&1 || true

step "Starting ${SERVICES[*]}"

# Two attempts: on a memory-constrained host, Docker Desktop stops containers gracefully — they log
# a clean startup and then exit 0 — and the next attempt usually succeeds.
started=false
for attempt in 1 2; do
  if docker compose up -d --wait "${SERVICES[@]}"; then
    started=true
    break
  fi
  warn "Attempt ${attempt} did not reach healthy. Retrying..."
  sleep 5
done

if [[ "$started" != true ]]; then
  docker compose ps -a --format 'table {{.Service}}\t{{.Status}}'
  warn 'Containers did not become healthy.'
  warn 'A container that logs a clean startup and then exits 0 is almost always host memory'
  warn 'pressure, not configuration. Check `docker compose logs`, free some memory, re-run.'
  exit 1
fi

printf '\n  API      http://localhost:8080\n'
printf '  Swagger  http://localhost:8080/swagger\n'
printf '  SPA      http://localhost:5173  (starting below, with hot reload)\n\n'

# ── SPA ────────────────────────────────────────────────────────────────────────────────────────
cd frontend

if [[ ! -d node_modules ]]; then
  step 'Installing frontend dependencies'
  npm install
fi

step 'Starting the Vite dev server — Ctrl+C to stop. Containers keep running; ./dev.sh --stop ends them.'
exec npm run dev
