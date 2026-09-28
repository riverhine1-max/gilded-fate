#!/usr/bin/env bash
# Prepares the Linux "trailer capture" build. These edits happen only on the CI runner;
# nothing here is committed back to the game project.
set -euo pipefail
cd "${GITHUB_WORKSPACE:-.}"

# 1. Add the capture harness (inert unless the player is launched with -gfTrailer).
cp ci/trailer/GildedTrailerCapture.cs Assets/Scripts/UI/GildedTrailerCapture.cs

# 2. Locked-framerate capture: WaitForSecondsRealtime runs on the wall clock and would
#    drift from the animations, so use a game-time wait that follows Time.captureDeltaTime.
grep -rl "new WaitForSecondsRealtime(" Assets/Scripts/UI | xargs -r sed -i 's/new WaitForSecondsRealtime(/new TrailerWait(/g'

# 3. Unity's Linux player can't decode H.264, so convert the character clips to VP8 WebM.
command -v ffmpeg >/dev/null || { sudo apt-get update -qq && sudo apt-get install -y -qq ffmpeg; }
count=0
for f in Assets/StreamingAssets/Animations/*/*.mp4; do
  ffmpeg -nostdin -y -loglevel error -i "$f" -an -c:v libvpx -b:v 5M -crf 4 -qmin 0 -qmax 24 -auto-alt-ref 0 "${f%.mp4}.webm"
  rm -f "$f" "$f.meta"
  count=$((count+1))
done
sed -i 's/+"\.mp4")/+".webm")/' Assets/Scripts/UI/HexerVideoCatalog.cs Assets/Scripts/UI/VanguardVideoCatalog.cs Assets/Scripts/UI/ReaperVideoCatalog.cs
grep -h 'webm' Assets/Scripts/UI/*VideoCatalog.cs | head -3
echo "Trailer build prepared: $count clips converted."
