#!/usr/bin/env python3
"""Builds listening demos: the opening and a sample combat turn, mixed on the game's real timeline, once with the ORIGINAL effects and once with the NEW ones,
so the two can be compared by ear. (Music is not included; the game ducks it under big hits.)

   python3 Tools/SoundForge/sequences.py <forged wav folder> <out folder>
"""
import os
import sys
import wave

import numpy as np
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dsp

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ORIG = os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX")

# The catalog's gain per cue (SoundCatalog.cs) times the intensity each call site passes; relative levels are what matter.
GAIN = {"EventWhisper": 0.25, "Resonance": 0.18, "Fateweave": 0.4, "BossPhase": 0.44, "HitHeavy": 0.55, "EventReveal": 0.3,
        "IntroAwaken": 0.4, "IntroDoor": 0.45, "IntroFlash": 0.5, "IntroStreaks": 0.4, "IntroClash": 0.55, "IntroCrown": 0.55, "IntroShimmer": 0.4,
        "TurnStart": 0.3, "CardDraw": 0.12, "CardPickup": 0.17, "AttackSteel": 0.29, "HitSteel": 0.49, "GainBlock": 0.52, "AttackArcane": 0.29, "HitArcane": 0.49,
        "EndTurn": 0.3, "EnemyAttack": 0.29, "BlockHit": 0.43, "BlockBreak": 0.55, "PlayerHurt": 0.55, "Heal": 0.37, "Debuff": 0.22, "EnemyDeath": 0.5, "Victory": 0.44,
        "CardPlay": 0.17, "CritHit": 0.55}

OPENING_ORIGINAL = [(1.2, "EventWhisper", 0.7), (3.4, "Resonance", 0.9), (4.0, "Fateweave", 0.75), (5.0, "BossPhase", 0.7), (7.1, "HitHeavy", 0.75), (7.25, "EventReveal", 0.6)]
OPENING_NEW = [(1.2, "IntroAwaken", 0.9), (3.4, "IntroDoor", 0.9), (4.0, "IntroFlash", 0.85), (4.2, "IntroStreaks", 0.85), (5.0, "IntroClash", 1.0), (7.1, "IntroCrown", 1.0), (7.25, "IntroShimmer", 0.8)]

COMBAT_ORIGINAL = [(0.0, "TurnStart", 0.7), (0.7, "CardDraw", 1), (0.8, "CardDraw", 1), (0.9, "CardDraw", 1), (1.0, "CardDraw", 1), (2.0, "CardPickup", 1), (2.45, "AttackSteel", 1),
                   (2.72, "HitSteel", 1), (3.4, "GainBlock", 1), (4.0, "CardPickup", 1), (4.4, "AttackArcane", 1), (4.75, "HitArcane", 1), (5.4, "HitHeavy", 1), (6.2, "EndTurn", 1),
                   (6.9, "EnemyAttack", 1), (7.35, "BlockHit", 1), (7.9, "BlockBreak", 1), (8.3, "PlayerHurt", 1), (9.0, "Heal", 1), (9.8, "Debuff", 1), (11.6, "Victory", 1)]
COMBAT_NEW = [(0.0, "TurnStart", 0.7), (0.7, "CardDraw", 1), (0.8, "CardDraw", 1), (0.9, "CardDraw", 1), (1.0, "CardDraw", 1), (2.0, "CardPickup", 1), (2.45, "AttackSteel", 1),
              (2.72, "HitSteel", 1), (3.4, "GainBlock", 1), (4.0, "CardPlay", 1), (4.4, "AttackArcane", 1), (4.75, "HitArcane", 1), (5.4, "HitHeavy", 1), (6.2, "EndTurn", 1),
              (6.9, "EnemyAttack", 1), (7.35, "BlockHit", 1), (7.9, "BlockBreak", 1), (8.3, "PlayerHurt", 1), (9.0, "Heal", 1), (9.8, "Debuff", 1), (10.6, "EnemyDeath", 1), (11.6, "Victory", 1)]


def load(folder, cue, take=1):
    w = wave.open(os.path.join(folder, f"{cue}_{take}.wav"))
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
    return a.reshape(-1, w.getnchannels())


def build(events, folder, total=None):
    clips = []
    end = 0
    for i, (at, cue, intensity) in enumerate(events):
        x = load(folder, cue, 1 + (i % 3))
        clips.append((at, x * GAIN.get(cue, 0.3) * intensity * 1.5))
        end = max(end, at + len(x) / dsp.SR)
    out = np.zeros((int((total or end + 0.3) * dsp.SR), 2))
    for at, x in clips:
        i = int(at * dsp.SR)
        n = min(len(x), len(out) - i)
        if n > 0:
            out[i:i + n] += x[:n]
    pk = np.abs(out).max()
    return out * (0.8 / pk) if pk > 0.8 else out


def save(path, x):
    wavfile.write(path, dsp.SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))


def main():
    forged, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    # the same overall scale for old and new, so the comparison is fair
    runs = {"opening_original": (OPENING_ORIGINAL, ORIG, 10.5), "opening_new": (OPENING_NEW, forged, 10.5),
            "combat_original": (COMBAT_ORIGINAL, ORIG, 13.0), "combat_new": (COMBAT_NEW, forged, 13.5)}
    for name, (events, folder, total) in runs.items():
        save(os.path.join(out, name + ".wav"), build(events, folder, total))
    print("wrote", ", ".join(runs), "->", out)


if __name__ == "__main__":
    main()
