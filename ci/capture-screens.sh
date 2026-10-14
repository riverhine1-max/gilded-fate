#!/usr/bin/env bash
# Screenshot tour of the real game. Runs the Linux player once per screen on a virtual display with software OpenGL (no GPU needed),
# using the game's own capture mode (-gfCapture <screen> -gfCapturePath <png>), and writes one PNG per screen per resolution.
#
#   bash ci/capture-screens.sh <linux player folder> <output folder> [WIDTHxHEIGHT ...]
#   CAPTURE_JOBS=3 bash ci/capture-screens.sh Builds/Linux shots 1920x1080 1280x720
#   CAPTURE_ONLY="map combat" bash ci/capture-screens.sh ...      (just those screens)
#   CAPTURE_EXTRA_ARGS="-gfClassicUi" bash ci/capture-screens.sh ...  (extra game flags, e.g. the classic look for before/after)
#
# Needs: xvfb (xvfb-run) and Mesa's software GL (libgl1-mesa-dri). Stills only: it says nothing about motion, sound or real-GPU performance.
# Output: <output>/<WxH>/<screen>.png, logs in <output>/<WxH>/logs, and <output>/summary.txt (one line per shot).
set -uo pipefail
PLAYER="${1:-}"; OUT="${2:-}"
if [ -z "$PLAYER" ] || [ -z "$OUT" ] || [ ! -x "$PLAYER/GildedFate.x86_64" ]; then echo "usage: capture-screens.sh <folder containing GildedFate.x86_64> <output folder> [WxH ...]"; exit 2; fi
shift 2
SIZES=("$@"); [ "${#SIZES[@]}" -eq 0 ] && SIZES=(1920x1080)
JOBS="${CAPTURE_JOBS:-3}"
PLAYER="$(cd "$PLAYER" && pwd)"; mkdir -p "$OUT"; OUT="$(cd "$OUT" && pwd)"

# Screens the game's capture mode can set up by itself (menu, run flow, combat for each hero, rewards, shops, events, collection ...).
ALL_SCREENS=(menu characters characters-hexer characters-reaper map map-summit combat combat-hexer combat-reaper combat-hand10 combat-hover combat-keyword combat-power boss
  reward collection inspection relics relic-inspection merchant merchant-hover upgrade-compare settings screens-pause sanctuary event fateweave binding shard-fractured credits card-style pile-inspection)
read -r -a SCREENS <<< "${CAPTURE_ONLY:-${ALL_SCREENS[*]}}"
export CAPTURE_EXTRA_ARGS="${CAPTURE_EXTRA_ARGS:-}"

shoot() {
  local size="$1" name="$2"; local w="${size%x*}" h="${size#*x}"; local dir="$OUT/$size"
  mkdir -p "$dir/logs"; rm -f "$dir/$name.png"
  LIBGL_ALWAYS_SOFTWARE=1 GALLIUM_DRIVER=llvmpipe timeout 240 xvfb-run -a -s "-screen 0 ${w}x${h}x24" "$PLAYER/GildedFate.x86_64" \
    -force-glcore -screen-width "$w" -screen-height "$h" -screen-fullscreen 0 -gfCapture "$name" -gfCapturePath "$dir/$name.png" -logFile "$dir/logs/$name.log" $CAPTURE_EXTRA_ARGS >/dev/null 2>&1
  local verdict; verdict="$(grep -o 'PASS\|FAIL' "$dir/logs/$name.log" 2>/dev/null | tail -1)"
  if [ -s "$dir/$name.png" ]; then echo "$size $name ok ${verdict:-no-verdict}"; else echo "$size $name MISSING ${verdict:-no-verdict}"; fi
}
export -f shoot; export PLAYER OUT

: > "$OUT/summary.txt"
for size in "${SIZES[@]}"; do for name in "${SCREENS[@]}"; do echo "$size $name"; done; done \
  | xargs -P "$JOBS" -L 1 bash -c 'shoot "$0" "$1"' | tee -a "$OUT/summary.txt"
rm -rf "$OUT"/*/save-fixtures 2>/dev/null

total=$(wc -l < "$OUT/summary.txt"); missing=$(grep -c ' MISSING ' "$OUT/summary.txt" || true); failed=$(grep -c ' FAIL$' "$OUT/summary.txt" || true)
echo "Captured $((total - missing)) of $total screenshots; $failed reported a self-check FAIL (see logs)."
[ "$missing" -eq 0 ]
