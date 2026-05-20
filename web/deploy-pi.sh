#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$SCRIPT_DIR"

if [ -f ".env" ]; then
  set -a
  # shellcheck disable=SC1091
  . ".env"
  set +a
fi

CLOUDFLARED_NETWORK="${CLOUDFLARED_NETWORK:-cloudflare}"

echo "Updating repository..."
cd "$REPO_DIR"
git pull --ff-only

cd "$SCRIPT_DIR"

echo "Starting site container..."
CLOUDFLARED_NETWORK="$CLOUDFLARED_NETWORK" docker compose -f compose.pi.yml up -d --force-recreate

echo "Done. Cloudflare Tunnel service should target: http://against-the-odds-site:8080"
