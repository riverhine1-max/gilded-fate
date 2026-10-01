#!/usr/bin/env bash
# Prepares the Linux "trailer capture" build. These edits happen only on the CI runner;
# nothing here is committed back to the game project.
set -euo pipefail
cd "${GITHUB_WORKSPACE:-.}"

# 1. Add the capture harness (inert unless the player is launched with -gfTrailer).
cp ci/trailer/GildedTrailerCapture.cs Assets/Scripts/UI/GildedTrailerCapture.cs

# 2. Locked-framerate capture: every presentation clock (unscaled time, realtime waits,
#    video time) is rewritten to follow Time.captureDeltaTime while a trailer records.
python3 ci/trailer/patch_scripts.py Assets/Scripts
cp ci/trailer/GildedTrailerTime.cs Assets/Scripts/UI/GildedTrailerTime.cs

# 3. Unity's Linux player can't decode H.264, so convert the character clips to VP8 WebM.
command -v ffmpeg >/dev/null || { sudo apt-get update -qq && sudo apt-get install -y -qq ffmpeg; }
count=0
for f in Assets/StreamingAssets/Animations/*/*.mp4; do
  ffmpeg -nostdin -y -loglevel error -i "$f" -an -c:v libvpx -b:v 5M -crf 4 -qmin 0 -qmax 24 -auto-alt-ref 0 "${f%.mp4}.webm"
  rm -f "$f" "$f.meta"
  count=$((count+1))
done
grep -h 'webm' Assets/Scripts/UI/*VideoCatalog.cs | head -3
echo "Trailer build prepared: $count clips converted."
