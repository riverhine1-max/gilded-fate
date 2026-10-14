#!/usr/bin/env python3
"""Gilded Fate sound forge: renders the new sound-effect set from code (deterministic, no recordings, no licences).

  python3 Tools/SoundForge/forge.py                       # writes Assets/Resources/Audio/SFX_Forged/<Cue>_<1..3>.wav (+ Unity .meta files)
  python3 Tools/SoundForge/forge.py --only HitHeavy,UiConfirm
  python3 Tools/SoundForge/forge.py --report /tmp/forge-report --mp3     # also metrics, spectrogram sheets and mp3s for listening

The music and the Chronicle's narration are not touched. The original effects in Assets/Resources/Audio/SFX stay as they are.
"""
import argparse
import hashlib
import json
import os
import sys
import time
import zlib

import numpy as np
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dsp  # noqa: E402
import designs  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX_Forged")
TAKES = 3


def seed_for(cue, take):
    return zlib.crc32(f"gilded-fate:{cue}:{take}".encode())


def render(cue, take):
    ctx = dsp.Ctx(seed_for(cue, take))
    designs.CURRENT["cue"] = cue
    raw = designs.DESIGNS[cue](ctx, take)
    return dsp.finalize(raw, target_rms_db=designs.TARGET_RMS[cue] + (take - 1) * 0.0, peak=0.55)


def write_wav(path, x):
    pcm = np.clip(x, -1, 1)
    wavfile.write(path, dsp.SR, (pcm * 32767).astype(np.int16))


def meta_text(guid, template):
    lines = template.splitlines()
    out = []
    for l in lines:
        out.append("guid: " + guid if l.startswith("guid:") else l)
    return "\n".join(out) + "\n"


def guid_for(name):
    return hashlib.md5(("gilded-fate-forged:" + name).encode()).hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    ap.add_argument("--only", default="")
    ap.add_argument("--report", default="")
    ap.add_argument("--mp3", action="store_true")
    ap.add_argument("--no-meta", action="store_true")
    args = ap.parse_args()

    cues = [c for c in designs.DESIGNS if not args.only or c in args.only.split(",")]
    os.makedirs(args.out, exist_ok=True)
    wav_tpl = open(os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX", "UiHover_1.wav.meta")).read() if not args.no_meta else ""
    dir_meta = os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX.meta")
    if not args.no_meta and os.path.exists(dir_meta):
        open(args.out.rstrip("/\\") + ".meta", "w", newline="\n").write(meta_text(guid_for("folder"), open(dir_meta).read()))
    rows = []
    t0 = time.time()
    for cue in cues:
        for take in range(TAKES):
            x = render(cue, take)
            fname = f"{cue}_{take + 1}.wav"
            write_wav(os.path.join(args.out, fname), x)
            if not args.no_meta and wav_tpl:
                open(os.path.join(args.out, fname + ".meta"), "w", newline="\n").write(meta_text(guid_for(fname), wav_tpl))
            m = dsp.metrics(x)
            m.update(cue=cue, take=take + 1)
            rows.append(m)
            if args.report:
                os.makedirs(args.report, exist_ok=True)
                np.save(os.path.join(args.report, f"{cue}_{take + 1}.npy"), x.astype(np.float32))
    print(f"rendered {len(rows)} clips in {time.time() - t0:.1f}s -> {args.out}")
    bad = [r for r in rows if r["clipped"] or np.isnan(r["peak"]) or r["head"] > 0.08 or r["tail"] > 0.02]
    for r in bad:
        print("  CHECK", r["cue"], r["take"], {k: round(v, 3) if isinstance(v, float) else v for k, v in r.items() if k in ("peak", "head", "tail", "clipped")})
    if args.report:
        json.dump(rows, open(os.path.join(args.report, "metrics.json"), "w"), indent=1)
        if args.mp3:
            import subprocess
            mp3dir = os.path.join(args.report, "mp3")
            os.makedirs(mp3dir, exist_ok=True)
            for cue in cues:
                for take in range(TAKES):
                    src = os.path.join(args.out, f"{cue}_{take + 1}.wav")
                    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, "-codec:a", "libmp3lame", "-b:a", "128k", os.path.join(mp3dir, f"{cue}_{take + 1}.mp3")], check=True)
            print("mp3s ->", mp3dir)


if __name__ == "__main__":
    main()
