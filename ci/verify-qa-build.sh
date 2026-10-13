#!/usr/bin/env bash
# Looks inside a finished Windows player build and checks whether the Chronicle QA tools are in it.
#   bash ci/verify-qa-build.sh release Builds/Windows     -> fails if ANY Chronicle QA code is present
#   bash ci/verify-qa-build.sh qa      Builds/WindowsQA   -> fails unless the QA tools are all present, and the build is safe to hand out
# Mono builds keep every C# type name in the managed assemblies, so a type that was compiled in can always be found by name.
set -uo pipefail
MODE="${1:-}"; DIR="${2:-}"
if [ "$MODE" != "release" ] && [ "$MODE" != "qa" ] || [ -z "$DIR" ] || [ ! -d "$DIR" ]; then
  echo "usage: verify-qa-build.sh <release|qa> <build directory>"; exit 2
fi
FAIL=0
fail() { echo "::error::QA build check ($MODE): $*"; FAIL=1; }
ok() { echo "ok   $*"; }

MANAGED=$(find "$DIR" -type d -name Managed | head -1)
[ -n "$MANAGED" ] || { fail "no Managed folder under $DIR, so the build cannot be inspected."; exit 1; }
echo "Inspecting $MANAGED"

# Type names that exist only when GILDED_CHRONICLE_QA was defined (or in the editor).
MARKERS=(ChronicleQaSession ChronicleQaMode ChronicleQaEntry DrawChronicleQa OpenChronicleQa)
DLLS=$(find "$MANAGED" -maxdepth 1 \( -name 'Assembly-CSharp.dll' -o -name 'GildedFate.*.dll' \))
[ -n "$DLLS" ] || { fail "found no game assemblies in $MANAGED."; exit 1; }

found=()
for marker in "${MARKERS[@]}"; do
  hit=0
  for dll in $DLLS; do grep -aq "$marker" "$dll" && hit=1; done
  [ "$hit" = 1 ] && found+=("$marker")
done

if [ "$MODE" = "release" ]; then
  if [ "${#found[@]}" -gt 0 ]; then fail "the release build contains Chronicle QA code: ${found[*]}"; else ok "no Chronicle QA code in the release build (checked ${#MARKERS[@]} markers)"; fi
else
  for marker in "${MARKERS[@]}"; do
    if printf '%s\n' "${found[@]:-}" | grep -qx "$marker"; then ok "QA marker present: $marker"; else fail "the QA build is missing $marker, so the QA panel is not in it."; fi
  done
  # A QA build must never be able to reach Steam or ship a Steam app id.
  if find "$DIR" -maxdepth 2 -name 'steam_appid.txt' | grep -q .; then fail "steam_appid.txt is present in the QA build."; else ok "no steam_appid.txt"; fi
  if find "$DIR" -name 'steam_api64.dll' | grep -q .; then fail "the Steam API library is shipped in the QA build."; else ok "no Steam API library shipped"; fi
  EXE=$(find "$DIR" -maxdepth 1 -name '*.exe' | head -1)
  case "$(basename "${EXE:-none}")" in GildedFateQA.exe) ok "executable is GildedFateQA.exe";; *) fail "executable should be GildedFateQA.exe (found: ${EXE:-none}).";; esac
fi

[ "$FAIL" = 0 ] && echo "Chronicle QA build check passed ($MODE)." || { echo "Chronicle QA build check FAILED ($MODE)."; exit 1; }
