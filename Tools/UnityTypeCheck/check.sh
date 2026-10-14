#!/usr/bin/env bash
# Type-checks all game scripts without Unity. Prints only real errors (known Unity 6 / Input System stand-in gaps are filtered).
#   bash Tools/UnityTypeCheck/check.sh            -> prints errors, ends with "typecheck done"; exit code 1 if any error was printed
#   GILDED_CHRONICLE_QA=1 bash ...                -> also compiles the Chronicle QA code
set -uo pipefail
cd "$(dirname "$0")" || exit 2
DEFS="UNITY_2021_3_OR_NEWER%3BUNITY_5_3_OR_NEWER"; [ -n "${GILDED_CHRONICLE_QA:-}" ] && DEFS="$DEFS%3BGILDED_CHRONICLE_QA"
OUT=$(dotnet build UnityTypeCheck.csproj -o "${TMPDIR:-/tmp}/gf-typecheck-out" -p:DefineConstants="$DEFS" 2>&1)
ERRS=$(echo "$OUT" | grep -E " error " | sed -E 's/ \[.*\]//' | sort -u | sed -E "s#$(cd ../.. && pwd)/##" \
  | grep -v "globalTextureMipmapLimit\|'Gamepad' does not contain a definition for 'allControls'\|WithButton\|timeUpdateMode\|VideoTimeUpdateMode" | cut -c1-260)
[ -n "$ERRS" ] && echo "$ERRS"
echo "typecheck done"
[ -z "$ERRS" ]
