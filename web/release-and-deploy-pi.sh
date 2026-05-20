#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_FULL_NAME="${REPO_FULL_NAME:-AubergineDeluxeEdition/Against-the-Odds}"
BRANCH="${BRANCH:-main}"
ASSET_NAME="${ASSET_NAME:-AgainstTheOdds-setup.exe}"

cd "$REPO_DIR"

if ! command -v gh >/dev/null 2>&1; then
  echo "gh is not installed on this machine." >&2
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "gh is not authenticated. Run: gh auth login" >&2
  exit 1
fi

echo "Fetching latest $BRANCH..."
git fetch origin "$BRANCH"

INSTALLER_PATH="$(
  git ls-tree -r "origin/$BRANCH" --name-only \
    | grep -E '(^|/)AgainstTheOdds-setup\.exe$' \
    | head -n 1 || true
)"

if [ -z "$INSTALLER_PATH" ]; then
  echo "Could not find AgainstTheOdds-setup.exe in origin/$BRANCH." >&2
  echo "Matching setup/exe files currently tracked:" >&2
  git ls-tree -r "origin/$BRANCH" --name-only | grep -Ei 'setup.*\.exe|\.exe$' >&2 || true
  exit 1
fi

echo "Adding installer to sparse checkout: $INSTALLER_PATH"
git sparse-checkout add web "$INSTALLER_PATH"
git pull --ff-only origin "$BRANCH"

if [ ! -f "$INSTALLER_PATH" ]; then
  echo "Installer still missing after pull: $INSTALLER_PATH" >&2
  exit 1
fi

COMMIT_SHA="$(git rev-parse HEAD)"
SHORT_SHA="$(git rev-parse --short HEAD)"
TIMESTAMP="$(date -u +%Y%m%d%H%M%S)"
TAG="build-${SHORT_SHA}-${TIMESTAMP}"

echo "Creating release $TAG from $COMMIT_SHA..."
gh release create "$TAG" "$INSTALLER_PATH#$ASSET_NAME" \
  --repo "$REPO_FULL_NAME" \
  --target "$COMMIT_SHA" \
  --title "Build $SHORT_SHA" \
  --notes "Windows installer from commit $COMMIT_SHA."

echo "Release created:"
echo "https://github.com/$REPO_FULL_NAME/releases/tag/$TAG"
echo "Download URL:"
echo "https://github.com/$REPO_FULL_NAME/releases/latest/download/$ASSET_NAME"

echo "Deploying site container..."
cd "$SCRIPT_DIR"
bash deploy-pi.sh
