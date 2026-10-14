#!/usr/bin/env python3
"""Measures the original effects (Assets/Resources/Audio/SFX, left untouched) with the forge's loudness metric and writes original_loudness.json,
so the new set can be matched to the old mix. Run it after changing the loudness metric. Needs the SFX wavs checked out (git lfs pull)."""
import glob, json, os, sys, wave
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dsp

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
res = {}
for f in sorted(glob.glob(os.path.join(root, "Assets", "Resources", "Audio", "SFX", "*.wav"))):
    w = wave.open(f)
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
    a = a.reshape(-1, w.getnchannels())
    res.setdefault(os.path.basename(f).rsplit("_", 1)[0], []).append(dsp.to_db(dsp.loud_rms(a)))
table = {c: round(float(np.mean(v)), 1) for c, v in res.items()}
json.dump(table, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "original_loudness.json"), "w"), indent=1, sort_keys=True)
print(len(table), "cues measured")
