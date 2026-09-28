#!/usr/bin/env bash
# Runs inside the unityci/editor container. Activates a Unity Personal seat with the
# account in UNITY_EMAIL / UNITY_PASSWORD, builds, and ALWAYS returns the seat.
# Usage: ci/unity-build.sh <BuildTarget> <ExecuteMethod> <OutputPath>
set -uo pipefail
TARGET="$1"; METHOD="$2"; OUTPUT="$3"
PROJECT=/project
LICENSE_DIR="$HOME/.local/share/unity3d/Unity"
CLIENT="$(find /opt/unity -type f -name 'Unity.Licensing.Client' 2>/dev/null | head -n 1)"

return_seat() {
  if [ -n "${ACTIVATED:-}" ] && [ -n "$CLIENT" ]; then
    echo "::group::Returning Unity license seat"
    "$CLIENT" --return-ulf || echo "::warning::Seat return reported an error. If later runs say no seats are available, sign in at id.unity.com and remove old activations."
    echo "::endgroup::"
  fi
}
trap return_seat EXIT

echo "::group::Unity license"
if [ -n "${UNITY_LICENSE:-}" ]; then
  mkdir -p "$LICENSE_DIR"
  printf '%s' "$UNITY_LICENSE" > "$LICENSE_DIR/Unity_lic.ulf"
  echo "Using UNITY_LICENSE file secret."
elif [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ] && [ -n "$CLIENT" ]; then
  if "$CLIENT" --activate-all --include-personal --username "$UNITY_EMAIL" --password "$UNITY_PASSWORD"; then
    ACTIVATED=1
    echo "Personal seat activated."
  else
    echo "::error::Unity sign-in failed. Check the UNITY_EMAIL and UNITY_PASSWORD secrets, and make sure two-step verification is OFF for that Unity account."
    exit 1
  fi
else
  echo "::error::No Unity credentials. Add UNITY_EMAIL and UNITY_PASSWORD as repository secrets (Settings > Secrets and variables > Actions)."
  exit 1
fi
echo "::endgroup::"

cp "$PROJECT/ci/GildedFateCiBuild.cs" "$PROJECT/Assets/Editor/GildedFateCiBuild.cs"

echo "::group::Unity build ($TARGET)"
unity-editor -batchmode -nographics -quit -accept-apiupdate \
  -projectPath "$PROJECT" -buildTarget "$TARGET" \
  -executeMethod "$METHOD" -gfOutput "$PROJECT/$OUTPUT" \
  -logFile -
CODE=$?
echo "::endgroup::"
if [ $CODE -ne 0 ]; then echo "::error::Unity build failed with exit code $CODE (scroll up in this step's log for the first 'error' line)."; fi
exit $CODE
