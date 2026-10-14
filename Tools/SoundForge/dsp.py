"""Sound-design primitives for the Gilded Fate sound forge.

Everything is numpy/scipy, deterministic (all randomness comes from a Ctx seeded per clip) and works on float arrays at SR.
Layers are mono until `stereo()` / `reverb()` make them stereo (shape (n, 2)). Nothing here touches the game's music or the Chronicle audio.
"""
import numpy as np
from scipy import signal

SR = 44100
TAU = 2 * np.pi


class Ctx:
    """Per-clip randomness: the same (cue, take) always makes the same sound."""

    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)

    def uniform(self, lo, hi):
        return float(self.rng.uniform(lo, hi))

    def jitter(self, value, amount):
        """value times a random factor within +-amount (0.05 = 5 percent)."""
        return value * (1.0 + self.uniform(-amount, amount))


# --------------------------------------------------------------------------- basics
def n_of(dur):
    return int(round(dur * SR))


def t_axis(dur):
    return np.arange(n_of(dur)) / SR


def silence(dur):
    return np.zeros(n_of(dur))


def db(x):
    return 10 ** (x / 20.0)


def to_db(x):
    return 20 * np.log10(max(x, 1e-9))


def fit(x, n):
    """Pad with zeros or trim to exactly n samples (mono or stereo)."""
    if len(x) >= n:
        return x[:n]
    pad = [(0, n - len(x))] + [(0, 0)] * (x.ndim - 1)
    return np.pad(x, pad)


def place(x, at, total=None):
    """Delay a layer by `at` seconds (optionally padding the result to `total` seconds)."""
    pad = n_of(at)
    pads = [(pad, 0)] + [(0, 0)] * (x.ndim - 1)
    y = np.pad(x, pads)
    return fit(y, n_of(total)) if total is not None else y


def mix(*layers, dur=None):
    """Sum layers of different lengths (all mono or all stereo)."""
    layers = [l for l in layers if l is not None and len(l)]
    n = max(len(l) for l in layers) if dur is None else n_of(dur)
    stereo_out = any(l.ndim == 2 for l in layers)
    out = np.zeros((n, 2)) if stereo_out else np.zeros(n)
    for l in layers:
        l = fit(l, n)
        if stereo_out and l.ndim == 1:
            l = np.stack([l, l], axis=1)
        out += l
    return out


def gain(x, g_db):
    return x * db(g_db)


class Env(np.ndarray):
    """An envelope. Multiplying a layer by an envelope of a different length fits the envelope to the layer instead of failing."""

    def __new__(cls, arr):
        return np.asarray(arr, dtype=float).view(cls)

    def _fitted(self, other):
        other = np.asarray(other)
        if other.ndim == 0:
            return None
        env = np.asarray(self)
        n = other.shape[0]
        if len(env) != n:
            env = np.pad(env, (0, n - len(env))) if len(env) < n else env[:n]
        return env if other.ndim == 1 else env[:, None]

    def __mul__(self, other):
        f = self._fitted(other)
        return np.asarray(other) * f if f is not None else np.asarray(self) * other

    def __rmul__(self, other):
        f = self._fitted(other)
        return np.asarray(other) * f if f is not None else np.asarray(self) * other


# --------------------------------------------------------------------------- envelopes
def env_exp(dur, tau, attack=0.002, hold=0.0):
    """Fast attack then exponential decay. tau is the time constant in seconds."""
    t = t_axis(dur)
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    a = a * a * (3 - 2 * a)
    d = np.exp(-np.maximum(t - attack - hold, 0) / tau)
    return Env(a * d)


def env_adsr(dur, a, d, s, r, peak=1.0, sustain_level=0.6):
    n = n_of(dur)
    t = np.arange(n) / SR
    env = np.zeros(n)
    na, nd, nr = n_of(a), n_of(d), n_of(r)
    env[:na] = np.linspace(0, peak, max(na, 1))
    env[na:na + nd] = np.linspace(peak, sustain_level * peak, max(nd, 1))[: max(0, n - na)]
    sus_end = max(na + nd, n - nr)
    env[na + nd:sus_end] = sustain_level * peak
    env[sus_end:] = np.linspace(sustain_level * peak, 0, n - sus_end)
    return Env(env)


def env_swell(dur, rise=0.5, fall=0.5, curve=2.0):
    """Rise to a peak at `rise` seconds then fall; curve > 1 makes the rise slow-then-fast (a riser)."""
    t = t_axis(dur)
    up = np.clip(t / max(rise, 1e-4), 0, 1) ** curve
    down = np.clip((dur - t) / max(fall, 1e-4), 0, 1)
    return Env(up * np.minimum(down, 1.0))


def fade(x, in_ms=2.0, out_ms=8.0):
    x = x.copy()
    ni, no = n_of(in_ms / 1000), n_of(out_ms / 1000)
    if ni:
        r = np.linspace(0, 1, ni)
        x[:ni] *= r if x.ndim == 1 else r[:, None]
    if no:
        r = np.linspace(1, 0, no)
        x[-no:] *= r if x.ndim == 1 else r[:, None]
    return x


# --------------------------------------------------------------------------- oscillators and noise
def sine(freq, dur, phase=0.0):
    """freq may be a scalar or an array (Hz, per sample) for pitch sweeps."""
    n = n_of(dur)
    f = np.full(n, float(freq)) if np.isscalar(freq) else fit(np.asarray(freq, float), n)
    return np.sin(phase + TAU * np.cumsum(f) / SR)


def freq_curve(dur, f0, f1, curve="exp"):
    """A frequency path from f0 to f1: 'exp' (musical, fast then slow), 'lin', or 'pow' (slow then fast)."""
    t = np.linspace(0, 1, n_of(dur))
    if curve == "lin":
        return f0 + (f1 - f0) * t
    if curve == "pow":
        return f0 + (f1 - f0) * t ** 3
    return f0 * (f1 / f0) ** t


def saw(freq, dur, harmonics=24, detune_cents=0.0):
    """Band-limited sawtooth by additive synthesis (scalar or per-sample frequency)."""
    n = n_of(dur)
    f = np.full(n, float(freq)) if np.isscalar(freq) else fit(np.asarray(freq, float), n)
    f = f * 2 ** (detune_cents / 1200)
    ph = np.cumsum(f) / SR
    out = np.zeros(n)
    for h in range(1, harmonics + 1):
        mask = (f * h) < SR * 0.45
        out += np.where(mask, np.sin(TAU * h * ph) / h, 0)
    return out * (2 / np.pi)


def square(freq, dur, harmonics=15):
    n = n_of(dur)
    f = np.full(n, float(freq)) if np.isscalar(freq) else fit(np.asarray(freq, float), n)
    ph = np.cumsum(f) / SR
    out = np.zeros(n)
    for h in range(1, harmonics * 2, 2):
        out += np.where((f * h) < SR * 0.45, np.sin(TAU * h * ph) / h, 0)
    return out * (4 / np.pi) * 0.5


def fm(carrier, ratio, index, dur):
    """Two-operator FM. index may be an array (an envelope on the modulation depth)."""
    n = n_of(dur)
    c = np.full(n, float(carrier)) if np.isscalar(carrier) else fit(np.asarray(carrier, float), n)
    idx = np.full(n, float(index)) if np.isscalar(index) else fit(np.asarray(index, float), n)
    ph_m = np.cumsum(c * ratio) / SR
    ph_c = np.cumsum(c) / SR
    return np.sin(TAU * ph_c + idx * np.sin(TAU * ph_m))


def noise(ctx, dur, color="white"):
    n = n_of(dur)
    w = ctx.rng.standard_normal(n)
    if color == "white":
        return w / 3.0
    spec = np.fft.rfft(w)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    freqs[0] = freqs[1] if len(freqs) > 1 else 1
    if color == "pink":
        spec = spec / np.sqrt(freqs)
    elif color == "brown":
        spec = spec / freqs
    elif color == "blue":
        spec = spec * np.sqrt(freqs)
    out = np.fft.irfft(spec, n)
    return out / (np.max(np.abs(out)) + 1e-9) * 0.7


# --------------------------------------------------------------------------- filters
def _sos(kind, fc, order):
    return signal.butter(order, fc, btype=kind, fs=SR, output="sos")


def lp(x, fc, order=4):
    return signal.sosfilt(_sos("lowpass", min(fc, SR * 0.45), order), x, axis=0)


def hp(x, fc, order=4):
    return signal.sosfilt(_sos("highpass", max(fc, 5), order), x, axis=0)


def bp(x, f0, f1, order=3):
    lo, hi = sorted((max(f0, 5.0), min(f1, SR * 0.45)))
    hi = max(hi, lo * 1.15)
    hi = min(hi, SR * 0.45)
    lo = min(lo, hi / 1.15)
    return signal.sosfilt(_sos("bandpass", [lo, hi], order), x, axis=0)


def resonator(x, f0, q=20.0):
    """A narrow resonant peak (rings after the input stops)."""
    w0 = f0 / (SR / 2)
    b, a = signal.iirpeak(min(w0, 0.99), q)
    return signal.lfilter(b, a, x, axis=0)


def peak_eq(x, f0, gain_db, q=1.0):
    """Peaking EQ (boost or cut around f0)."""
    A = 10 ** (gain_db / 40)
    w0 = TAU * f0 / SR
    alpha = np.sin(w0) / (2 * q)
    b = [1 + alpha * A, -2 * np.cos(w0), 1 - alpha * A]
    a = [1 + alpha / A, -2 * np.cos(w0), 1 - alpha / A]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x, axis=0)


def shelf(x, f0, gain_db, high=True):
    A = 10 ** (gain_db / 40)
    w0 = TAU * f0 / SR
    cosw, sinw = np.cos(w0), np.sin(w0)
    alpha = sinw / 2 * np.sqrt(2)
    if high:
        b0 = A * ((A + 1) + (A - 1) * cosw + 2 * np.sqrt(A) * alpha)
        b1 = -2 * A * ((A - 1) + (A + 1) * cosw)
        b2 = A * ((A + 1) + (A - 1) * cosw - 2 * np.sqrt(A) * alpha)
        a0 = (A + 1) - (A - 1) * cosw + 2 * np.sqrt(A) * alpha
        a1 = 2 * ((A - 1) - (A + 1) * cosw)
        a2 = (A + 1) - (A - 1) * cosw - 2 * np.sqrt(A) * alpha
    else:
        b0 = A * ((A + 1) - (A - 1) * cosw + 2 * np.sqrt(A) * alpha)
        b1 = 2 * A * ((A - 1) - (A + 1) * cosw)
        b2 = A * ((A + 1) - (A - 1) * cosw - 2 * np.sqrt(A) * alpha)
        a0 = (A + 1) + (A - 1) * cosw + 2 * np.sqrt(A) * alpha
        a1 = -2 * ((A - 1) + (A + 1) * cosw)
        a2 = (A + 1) + (A - 1) * cosw - 2 * np.sqrt(A) * alpha
    return signal.lfilter(np.array([b0, b1, b2]) / a0, np.array([1, a1 / a0, a2 / a0]), x, axis=0)


def sweep_filter(x, f_path, width=0.35, mode="bp", floor=0.0):
    """
    A filter whose centre moves over time, done in the frequency domain (STFT) so sweeps are smooth.
    f_path: per-sample centre frequency (use freq_curve). width: bandwidth in octaves for 'bp'; softness for 'lp'/'hp'.
    mode: 'bp' bright band moving through the sound, 'lp' opening/closing low-pass, 'hp' high-pass.
    floor: how much of the unfiltered signal leaks through (0..1).
    """
    n = len(x)
    f_path = fit(np.asarray(f_path, float), n)
    nper = 1024
    f, tt, Z = signal.stft(x, fs=SR, nperseg=nper, noverlap=nper * 3 // 4, boundary="zeros", padded=True)
    centres = np.interp(tt * SR, np.arange(n), f_path)
    logf = np.log2(np.maximum(f, 1.0))[:, None]
    logc = np.log2(np.maximum(centres, 20.0))[None, :]
    if mode == "bp":
        mask = np.exp(-0.5 * ((logf - logc) / max(width, 0.02)) ** 2)
    elif mode == "lp":
        mask = 1 / (1 + np.exp((logf - logc) / max(width * 0.3, 0.02)))
    else:
        mask = 1 / (1 + np.exp(-(logf - logc) / max(width * 0.3, 0.02)))
    mask = floor + (1 - floor) * mask
    _, y = signal.istft(Z * mask, fs=SR, nperseg=nper, noverlap=nper * 3 // 4, boundary=True)
    return fit(y, n)


# --------------------------------------------------------------------------- dynamics and tone shaping
def saturate(x, drive=2.0):
    return np.tanh(drive * x) / np.tanh(drive)


def soft_clip(x, ceiling=0.95):
    return ceiling * np.tanh(x / ceiling)


def transient_shape(x, attack_gain=1.8, sustain_gain=1.0, fast_ms=1.0, slow_ms=40.0):
    """Boost or soften the front of a sound. Punchier hits come from attack_gain > 1."""
    ax = np.abs(x if x.ndim == 1 else x.mean(1))
    nf, ns = max(1, n_of(fast_ms / 1000)), max(1, n_of(slow_ms / 1000))
    fast = signal.lfilter([1 / nf] * nf, [1], ax)
    slow = signal.lfilter([1 / ns] * ns, [1], ax) + 1e-6
    ratio = np.clip(fast / slow, 0, 6)
    g = sustain_gain + (attack_gain - sustain_gain) * np.clip((ratio - 1) / 2, 0, 1)
    return x * (g if x.ndim == 1 else g[:, None])


def compress(x, threshold_db=-18.0, ratio=4.0, attack_ms=5.0, release_ms=80.0, makeup_db=0.0):
    """A plain feed-forward compressor (block-wise, fine for short sound effects)."""
    mono = np.abs(x if x.ndim == 1 else x.max(1))
    block = 64
    nb = len(mono) // block + 1
    lvl = np.array([mono[i * block:(i + 1) * block].max(initial=0) for i in range(nb)])
    lvl_db = 20 * np.log10(lvl + 1e-9)
    over = np.maximum(lvl_db - threshold_db, 0)
    target_red = over * (1 - 1 / ratio)
    att = np.exp(-block / SR / (attack_ms / 1000))
    rel = np.exp(-block / SR / (release_ms / 1000))
    red = np.zeros(nb)
    cur = 0.0
    for i in range(nb):
        cur = att * cur + (1 - att) * target_red[i] if target_red[i] > cur else rel * cur + (1 - rel) * target_red[i]
        red[i] = cur
    g = 10 ** ((-red + makeup_db) / 20)
    g = np.repeat(g, block)[: len(mono)]
    return x * (g if x.ndim == 1 else g[:, None])


# --------------------------------------------------------------------------- physical-ish sources
BELL = [(1.0, 1.0, 1.0), (2.0, 0.6, 0.7), (2.76, 0.85, 0.55), (4.07, 0.45, 0.4), (5.4, 0.55, 0.3), (6.8, 0.25, 0.22), (8.93, 0.3, 0.16)]
GLASS = [(1.0, 1.0, 1.0), (2.32, 0.7, 0.5), (4.25, 0.55, 0.35), (6.63, 0.4, 0.25), (9.38, 0.28, 0.18)]
PLATE = [(1.0, 1.0, 1.0), (1.59, 0.8, 0.8), (2.14, 0.7, 0.7), (2.30, 0.65, 0.6), (2.65, 0.6, 0.5), (2.92, 0.5, 0.45), (3.16, 0.45, 0.4), (3.50, 0.4, 0.3)]
GONG = [(1.0, 1.0, 1.0), (1.47, 0.8, 0.9), (2.09, 0.7, 0.8), (2.56, 0.6, 0.7), (3.27, 0.55, 0.55), (3.91, 0.4, 0.45), (5.13, 0.3, 0.3)]
WOOD = [(1.0, 1.0, 1.0), (2.45, 0.5, 0.35), (4.1, 0.25, 0.2)]
COIN = [(1.0, 1.0, 1.0), (1.63, 0.8, 0.8), (2.41, 0.6, 0.6), (3.08, 0.5, 0.5), (4.79, 0.3, 0.3)]


def modal(ctx, f0, dur, partials=BELL, decay=1.2, brightness=1.0, detune=0.003, inharm=0.0, shimmer=0.0):
    """
    A struck object as a sum of exponentially decaying sine modes. partials = (ratio, amplitude, relative decay).
    detune makes each mode beat slightly (real metal is never perfectly in tune), shimmer adds a slow tremolo to the upper modes.
    """
    t = t_axis(dur)
    out = np.zeros_like(t)
    for i, (ratio, amp, dec) in enumerate(partials):
        f = f0 * ratio * (1 + inharm * i * i * 0.01) * (1 + ctx.uniform(-detune, detune))
        if f > SR * 0.45:
            continue
        a = amp * (brightness ** i)
        env = np.exp(-t / (decay * dec))
        ph = ctx.uniform(0, TAU)
        mod = 1 + shimmer * np.sin(TAU * ctx.uniform(2, 6) * t + ctx.uniform(0, TAU)) if shimmer and i > 0 else 1
        out += a * env * mod * np.sin(ph + TAU * f * t)
    return out / (len(partials) * 0.6)


def thump(ctx, dur=0.35, f_start=140.0, f_end=42.0, tau=0.11, click=0.2):
    """A kick-like sub drop: a sine that falls in pitch, plus a tiny click at the front."""
    f = freq_curve(dur, f_start, f_end, "exp")
    body = sine(f, dur) * env_exp(dur, tau, 0.001)
    if click:
        body = body + click * hp(noise(ctx, dur), 1500) * env_exp(dur, 0.004, 0.0005)
    return body


def click(ctx, dur=0.02, f_lo=2500.0, f_hi=9000.0, tau=0.004):
    return bp(noise(ctx, dur + 0.01), f_lo, f_hi) * env_exp(dur + 0.01, tau, 0.0004)[: n_of(dur + 0.01)]


def whoosh(ctx, dur, f0, f1, width=0.9, rise=0.5, curve=1.6, color="pink"):
    """Air movement: noise through a moving band, swelling in then out. rise is where (0..1) the peak sits."""
    n = noise(ctx, dur, color)
    path = freq_curve(dur, f0, f1, "exp")
    sweep = sweep_filter(n, path, width=width)
    t = np.linspace(0, 1, len(sweep))
    peak = np.clip(rise, 0.05, 0.95)
    env = np.where(t < peak, (t / peak) ** curve, ((1 - t) / (1 - peak)) ** 1.4)
    return sweep * env


def crackle(ctx, dur, density=60.0, f_lo=1500.0, f_hi=7000.0, decay=0.004, bias_to_start=0.0):
    """Sparse random pops (fire, debris, coins, paper). density = events per second."""
    n = n_of(dur)
    out = np.zeros(n)
    count = max(1, int(density * dur))
    pos = ctx.rng.random(count) ** (1 + bias_to_start)
    for p in pos:
        i = int(p * (n - 400))
        amp = ctx.rng.random() ** 2
        L = min(n - i, int(SR * decay * 8))
        seg = bp(noise(ctx, L / SR + 0.001), ctx.uniform(f_lo, f_hi * 0.6), ctx.uniform(f_lo * 1.4, f_hi), order=2)
        seg = seg[:L] * np.exp(-np.arange(L) / (SR * decay))
        out[i:i + L] += amp * seg
    return out


def pluck(ctx, freq, dur, damping=0.996, bright=0.5):
    """Karplus-Strong plucked string (harp, lute, gilded thread)."""
    n = n_of(dur)
    period = int(round(SR / freq))
    buf = ctx.rng.uniform(-1, 1, period) * np.linspace(1, bright, period)
    out = np.zeros(n)
    for i in range(n):
        j = i % period
        out[i] = buf[j]
        buf[j] = damping * 0.5 * (buf[j] + buf[(j + 1) % period])
    return out


def choir(ctx, freqs, dur, vowel="ah", voices=3, breath=0.15):
    """A synthetic 'ooh/ah' pad: detuned saws through vowel formants, with a little breath noise."""
    formants = {"ah": [(800, 90, 1.0), (1150, 110, 0.5), (2900, 150, 0.15)], "oh": [(450, 70, 1.0), (800, 80, 0.4), (2830, 130, 0.1)], "oo": [(325, 60, 1.0), (700, 70, 0.3), (2700, 120, 0.1)]}[vowel]
    out = np.zeros(n_of(dur))
    for f in np.atleast_1d(freqs):
        for v in range(voices):
            s = saw(f * (1 + ctx.uniform(-0.004, 0.004)), dur, harmonics=14)
            out += s / voices
    shaped = np.zeros_like(out)
    for fc, bw, g in formants:
        shaped += g * bp(out, max(fc - bw, 60), fc + bw, order=2)
    if breath:
        shaped += breath * bp(noise(ctx, dur), 1500, 5000) * 0.3
    return shaped


def brass(ctx, freq, dur, attack=0.06, open_to=4500.0, start=700.0, voices=3):
    """A synth-brass note: stacked saws through a low-pass that opens during the attack."""
    s = np.zeros(n_of(dur))
    for v in range(voices):
        s += saw(freq * (1 + ctx.uniform(-0.005, 0.005)), dur, harmonics=30) / voices
    path = np.concatenate([freq_curve(attack * 1.5, start, open_to), np.full(max(0, n_of(dur) - n_of(attack * 1.5)), open_to)])
    return sweep_filter(s, path, width=0.5, mode="lp") * env_adsr(dur, attack, 0.15, 0, 0.12, sustain_level=0.8)


# --------------------------------------------------------------------------- space and stereo
_IR_CACHE = {}


def impulse_response(space, ctx=None):
    key = space
    if key in _IR_CACHE:
        return _IR_CACHE[key]
    presets = {
        "plate": dict(rt=1.1, pre=0.004, damp=9000, er=0.3, density=1.0),
        "chamber": dict(rt=0.55, pre=0.006, damp=6500, er=0.5, density=0.9),
        "stone": dict(rt=1.5, pre=0.012, damp=5200, er=0.6, density=0.8),
        "hall": dict(rt=2.2, pre=0.02, damp=7000, er=0.4, density=1.0),
        "cathedral": dict(rt=4.2, pre=0.035, damp=4800, er=0.35, density=1.0),
        "tiny": dict(rt=0.22, pre=0.002, damp=8000, er=0.6, density=1.0),
        "cavern": dict(rt=6.0, pre=0.05, damp=3800, er=0.3, density=0.9),
    }
    p = presets[space]
    rng = np.random.default_rng(sum(map(ord, space)) * 7919)
    n = n_of(p["rt"] * 1.15 + p["pre"])
    out = []
    for ch in range(2):
        w = rng.standard_normal(n)
        t = np.arange(n) / SR
        w = w * np.exp(-6.9 * t / p["rt"])
        w[: n_of(p["pre"])] = 0
        # frequency-dependent decay: darker as it dies
        w = signal.sosfilt(signal.butter(2, p["damp"], btype="lowpass", fs=SR, output="sos"), w)
        w2 = signal.sosfilt(signal.butter(1, 700, btype="lowpass", fs=SR, output="sos"), w)
        w = w * 0.8 + w2 * 0.35 * np.exp(-t / (p["rt"] * 0.8))
        # a few early reflections
        for k in range(7):
            at = n_of(p["pre"] + rng.uniform(0.004, 0.06) * (1 + k * 0.5))
            if at < n:
                w[at] += rng.uniform(0.3, 1.0) * p["er"] * (1 if rng.random() > 0.5 else -1) * 6
        out.append(w)
    ir = np.stack(out, axis=1)
    ir /= np.sqrt((ir ** 2).sum(0, keepdims=True)) + 1e-9
    _IR_CACHE[key] = ir
    return ir


TAILS = {"tiny": 0.22, "chamber": 0.42, "plate": 0.7, "stone": 0.75, "hall": 1.25, "cathedral": 2.0, "cavern": 2.6}


def reverb(x, space="plate", wet=0.25, lowcut=180.0, tail=None):
    """
    Convolution reverb. Returns stereo, the dry sound centred, the wet spread left and right.
    tail: seconds of reverb allowed to ring on after the dry sound ends (default depends on the room); it fades out smoothly.
    Short tails keep frequent sounds clean; set a longer one for set pieces.
    """
    ir = impulse_response(space)
    dry = np.stack([x, x], axis=1) if x.ndim == 1 else x
    mono = x if x.ndim == 1 else x.mean(1)
    tail = TAILS[space] if tail is None else tail
    n = len(mono) + n_of(tail)
    wet_sig = np.stack([signal.fftconvolve(mono, ir[:, c])[:n] for c in range(2)], axis=1)
    wet_sig = hp(wet_sig, lowcut, 2)
    if len(wet_sig) < n:
        wet_sig = np.pad(wet_sig, ((0, n - len(wet_sig)), (0, 0)))
    fo = n_of(tail * 0.7)
    if fo > 0:
        ramp = np.ones(n)
        ramp[-fo:] = 0.5 * (1 + np.cos(np.linspace(0, np.pi, fo)))
        wet_sig = wet_sig * ramp[:, None]
    out = np.zeros((n, 2))
    out[: len(dry)] += dry * (1 - 0.35 * wet)
    out += wet_sig * wet * 1.6
    return out


def stereo(x, pan=0.0):
    """Constant-power pan of a mono layer (-1 left .. +1 right)."""
    if x.ndim == 2:
        return x
    a = (pan + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def widen(x, amount=0.5, delay_ms=11.0):
    """Stereo widening of a mono layer: a slightly delayed, high-passed copy on the opposite side."""
    mono = x if x.ndim == 1 else x.mean(1)
    d = n_of(delay_ms / 1000)
    side = np.concatenate([np.zeros(d), hp(mono, 400, 1)])[: len(mono)]
    return np.stack([mono + amount * side, mono - amount * side], axis=1) * (1 / (1 + amount * 0.5))


def echo(x, delay=0.18, feedback=0.35, taps=4, lowpass=4500.0):
    out = x.copy()
    d = n_of(delay)
    cur = x
    for k in range(1, taps + 1):
        cur = lp(cur, lowpass, 1) * feedback
        out = mix(out, place(cur, delay * k))
    return out


# --------------------------------------------------------------------------- mastering and metering
def k_weight(mono):
    """A rough K-weighting (ITU BS.1770 style): little weight below about 100 Hz, a lift above 1.5 kHz. Loudness by ear, not by raw RMS."""
    return shelf(hp(mono, 100, 2), 1700, 4.0, high=True)


def loud_rms(x, window=0.25):
    """Perceptual loudness: the RMS of the loudest `window` seconds after K-weighting (long quiet tails and deep sub-bass do not inflate it)."""
    mono = x if x.ndim == 1 else x.mean(1)
    mono = k_weight(mono)
    w = max(1, n_of(window))
    if len(mono) <= w:
        return np.sqrt((mono ** 2).mean()) + 1e-9
    sq = np.cumsum(np.concatenate([[0.0], mono ** 2]))
    means = (sq[w:] - sq[:-w]) / w
    return np.sqrt(means.max()) + 1e-9


def finalize(x, target_rms_db=-24.0, peak=0.55, fade_out_ms=20.0, lowcut=28.0, limit=True):
    """Make the clip game-ready: stereo, no DC or sub rumble, quiet ends, loudness near its target and under a safe peak."""
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    x = hp(x, lowcut, 2)
    x = x - x.mean(0, keepdims=True)
    env = np.abs(x).max(1)
    idx = np.where(env > 10 ** (-50 / 20) * max(env.max(), 1e-9))[0]
    if len(idx):
        x = x[: min(len(x), idx[-1] + n_of(0.03))]
    x = fade(x, 1.5, fade_out_ms)
    x = x * (db(target_rms_db) / loud_rms(x))
    if limit:
        pk = np.abs(x).max()
        if pk > peak:
            x = soft_clip(x, peak * 1.25)
            x = x * (peak / np.abs(x).max())
    return x


def metrics(x):
    mono = x if x.ndim == 1 else x.mean(1)
    pk = np.abs(x).max()
    active = np.abs(mono) > 10 ** (-40 / 20) * max(np.abs(mono).max(), 1e-9)
    rms = loud_rms(x)
    return dict(dur=len(x) / SR, peak=float(pk), rms_db=float(to_db(rms)), crest_db=float(to_db(pk) - to_db(rms)),
                clipped=bool(pk >= 0.999), head=float(np.abs(x[:8]).max()), tail=float(np.abs(x[-8:]).max()))
