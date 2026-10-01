"""Rewrite game scripts for the locked-framerate trailer capture build (never committed)."""
import re, sys, pathlib
root = pathlib.Path(sys.argv[1])
T = "GildedFate.UI.GildedTrailerTime"
rules = [
    (re.compile(r"(?:UnityEngine\.)?\bTime\.unscaledTime\b"), T + ".Now"),
    (re.compile(r"(?:UnityEngine\.)?\bTime\.unscaledDeltaTime\b"), T + ".Delta"),
    (re.compile(r"(?:UnityEngine\.)?\bTime\.realtimeSinceStartup\b"), T + ".Realtime"),
    (re.compile(r"new WaitForSecondsRealtime\("), "new GildedFate.UI.TrailerWait("),
    (re.compile(r"VideoTimeUpdateMode\.UnscaledGameTime"), "VideoTimeUpdateMode.GameTime"),
    # The OS cursor must never hover buttons in recorded footage; the harness drives a virtual pointer.
    (re.compile(r"guiPointerPosition=rootEventPointer/uiScale;"),
     "guiPointerPosition=GildedFate.UI.GildedTrailerDirector.Active?GildedFate.UI.GildedTrailerDirector.Pointer:rootEventPointer/uiScale;"),
    # Log every sound the game plays while recording so the edit can re-create the mix in sync.
    (re.compile(r"PlayedCount\+\+;cueCounts\[\(int\)cue\]\+\+;"),
     "PlayedCount++;cueCounts[(int)cue]++;GildedFate.UI.GildedTrailerDirector.Sfx(cue.ToString(),take,slot.gain,pan,slot.source.pitch);"),
]
# Only the character-clip catalogs point at .mp4 files that the Linux build converts to .webm.
video_rule = (re.compile(r'\+"\.mp4"\)'), '+".webm")')
count = 0
for f in root.rglob("*.cs"):
    if f.name == "GildedTrailerTime.cs":
        continue
    s = f.read_text(encoding="utf-8")
    o = s
    for rx, rep in rules + ([video_rule] if f.name.endswith("VideoCatalog.cs") else []):
        s = rx.sub(rep, s)
    if s != o:
        f.write_text(s, encoding="utf-8"); count += 1
print(f"patched {count} files")
