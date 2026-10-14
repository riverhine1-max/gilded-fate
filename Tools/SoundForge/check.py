#!/usr/bin/env python3
"""Checks the sound forge against the game: every SoundCue in SoundCatalog.cs has a design, every design is a real cue, and the rendered clips are sound
(three takes each, stereo, 44.1 kHz, no clipping, quiet start and end, sane length and loudness). Exits non-zero on any problem.
   python3 Tools/SoundForge/check.py
"""
import os
import re
import sys
import tempfile
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import designs  # noqa: E402
import dsp  # noqa: E402
import forge  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))


def cues_in_catalog():
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Audio", "SoundCatalog.cs")).read()
    body = re.search(r"public enum SoundCue\s*\{(.*?)\}", src, re.S).group(1)
    body = re.sub(r"//[^\n]*", "", body)
    return [c.strip() for c in body.replace("\n", " ").split(",") if c.strip()]


def main():
    problems = []
    cues = cues_in_catalog()
    missing = [c for c in cues if c not in designs.DESIGNS]
    extra = [c for c in designs.DESIGNS if c not in cues]
    if missing:
        problems.append(f"cues with no design: {missing}")
    if extra:
        problems.append(f"designs for cues that do not exist: {extra}")
    out = tempfile.mkdtemp(prefix="forge-check-")
    sys.argv = ["forge.py", "--out", out, "--no-meta"]
    forge.main()
    for cue in designs.DESIGNS:
        for take in range(1, 4):
            path = os.path.join(out, f"{cue}_{take}.wav")
            if not os.path.exists(path):
                problems.append(f"{cue} take {take} was not rendered")
                continue
            w = wave.open(path)
            if (w.getnchannels(), w.getframerate(), w.getsampwidth()) != (2, 44100, 2):
                problems.append(f"{cue}_{take}: not 16-bit stereo 44.1 kHz")
            a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
            dur = len(a) / 2 / 44100
            if np.abs(a).max() >= 0.999:
                problems.append(f"{cue}_{take}: clips")
            if np.abs(a[:16]).max() > 0.1 or np.abs(a[-16:]).max() > 0.01:
                problems.append(f"{cue}_{take}: does not start/end quietly (click)")
            if not 0.15 <= dur <= 8.0:
                problems.append(f"{cue}_{take}: length {dur:.2f}s out of range")
            loud = dsp.to_db(dsp.loud_rms(a.reshape(-1, 2)))
            if not -42 <= loud <= -12:
                problems.append(f"{cue}_{take}: loudness {loud:.1f} dB out of range")
    if problems:
        print("SOUND FORGE PROBLEMS:")
        for p in problems:
            print("  -", p)
        sys.exit(1)
    print(f"sound forge ok: {len(cues)} cues x 3 takes")


if __name__ == "__main__":
    main()
