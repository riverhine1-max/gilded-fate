#!/usr/bin/env python3
"""Renders a contact sheet of spectrograms (time across, frequency up, log scale) for the forged clips, so their shape can be checked by eye.
   python3 Tools/SoundForge/sheet.py <folder with .npy files from forge.py --report> <out.png> Cue1 Cue2 ...
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import signal

SR = 44100


def spec_image(x, width=420, height=150, fmax=16000):
    mono = x.mean(1) if x.ndim == 2 else x
    f, t, Z = signal.stft(mono, fs=SR, nperseg=1024, noverlap=896)
    mag = 20 * np.log10(np.abs(Z) + 1e-7)
    keep = f <= fmax
    f, mag = f[keep], mag[keep]
    # resample to log-frequency rows
    rows = np.geomspace(40, fmax, height)
    idx = np.clip(np.searchsorted(f, rows), 0, len(f) - 1)
    img = mag[idx][::-1]
    img = np.clip((img + 100) / 70, 0, 1)
    cols = np.linspace(0, img.shape[1] - 1, width).astype(int)
    img = img[:, cols]
    r = np.clip(img * 3 - 1.2, 0, 1); g = np.clip(img * 3 - 0.4, 0, 1) ** 1.3; b = np.clip(1.4 - np.abs(img * 3 - 0.9), 0, 1)
    rgb = np.stack([r, g * 0.9 + 0.1 * img, b * 0.8], axis=2)
    return Image.fromarray((rgb * 255).astype(np.uint8))


def wave_strip(x, width=420, height=34):
    mono = x.mean(1) if x.ndim == 2 else x
    img = Image.new("RGB", (width, height), (10, 12, 18))
    d = ImageDraw.Draw(img)
    chunk = max(1, len(mono) // width)
    for i in range(width):
        seg = mono[i * chunk:(i + 1) * chunk]
        if len(seg) == 0:
            break
        a = np.abs(seg).max() * (height / 2 - 1) / 0.6
        d.line([(i, height / 2 - a), (i, height / 2 + a)], fill=(210, 170, 90))
    return img


def main():
    folder, out, cues = sys.argv[1], sys.argv[2], sys.argv[3:]
    tiles = []
    for c in cues:
        x = np.load(os.path.join(folder, f"{c}_1.npy"))
        tile = Image.new("RGB", (420, 150 + 34 + 18), (14, 16, 24))
        tile.paste(spec_image(x), (0, 18)); tile.paste(wave_strip(x), (0, 168))
        ImageDraw.Draw(tile).text((4, 3), f"{c}  {len(x) / SR:.2f}s", fill=(240, 220, 170))
        tiles.append(tile)
    cols = 3
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * 424, rows * 206), (0, 0, 0))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * 424, (i // cols) * 206))
    sheet.save(out)


if __name__ == "__main__":
    main()
