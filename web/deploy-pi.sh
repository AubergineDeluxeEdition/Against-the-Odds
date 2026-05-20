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
GITHUB_REPOSITORY="${GITHUB_REPOSITORY:-AubergineDeluxeEdition/Against-the-Odds}"
GITHUB_RELEASE_TAG="${GITHUB_RELEASE_TAG:-latest}"
INSTALLER_ASSET="${INSTALLER_ASSET:-AgainstTheOdds-setup.exe}"

echo "Updating repository..."
cd "$REPO_DIR"
git pull --ff-only

cd "$SCRIPT_DIR"
mkdir -p downloads

echo "Downloading installer from GitHub release..."
if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI is required. Install it, then run: gh auth login" >&2
  exit 1
fi

if [ "$GITHUB_RELEASE_TAG" = "latest" ]; then
  gh release download \
    --repo "$GITHUB_REPOSITORY" \
    --pattern "$INSTALLER_ASSET" \
    --dir downloads \
    --clobber
else
  gh release download "$GITHUB_RELEASE_TAG" \
    --repo "$GITHUB_REPOSITORY" \
    --pattern "$INSTALLER_ASSET" \
    --dir downloads \
    --clobber
fi

if [ ! -s "downloads/$INSTALLER_ASSET" ]; then
  echo "Installer was not downloaded: downloads/$INSTALLER_ASSET" >&2
  exit 1
fi

echo "Starting site container..."
CLOUDFLARED_NETWORK="$CLOUDFLARED_NETWORK" docker compose -f compose.pi.yml up -d

echo "Done. Cloudflare Tunnel service should target: http://against-the-odds-site:8080"
