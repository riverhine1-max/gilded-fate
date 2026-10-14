"""The sound designs: one recipe per cue. Each takes (ctx, take) and returns a layered clip; forge.py masters and writes it.

Palette: dark gilded fantasy. Bells and chimes are tuned to D minor pentatonic (D F G A C) so everything the game makes sounds like one instrument.
Layering per hit: sub (weight), body (mid thump), crack (attack), ring (the object sounding), debris/air (texture), room (reverb).
"""
import json
import os

import numpy as np
from dsp import *
from dsp import reverb as _reverb

CURRENT = {"cue": None}

def reverb(x, space, wet, **kw):
    """dsp.reverb with the per-cue tail length applied (see TAIL_OVERRIDE at the bottom of this file)."""
    tail = TAIL_OVERRIDE.get(CURRENT["cue"])
    if tail is not None and "tail" not in kw:
        kw["tail"] = tail
    return _reverb(x, space, wet, **kw)


PV = [1.0, 1.047, 0.953]      # pitch factor per take, so the three takes are the same sound in a different mood


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)


D3, F3, G3, A3, C4, D4, F4, G4, A4, C5, D5, F5, G5, A5, C6, D6 = [hz(m) for m in (50, 53, 55, 57, 60, 62, 65, 67, 69, 72, 74, 77, 79, 81, 84, 86)]
D2, A2 = hz(38), hz(45)


def lvl(x, g):
    return x * g


def S(*layers, dur=None):
    return mix(*layers, dur=dur)


# --------------------------------------------------------------------------- reusable hit pieces
def impact(ctx, p, size=1.0, dur=0.5, crack=0.5, body_hz=(160, 900), sub_to=42.0):
    """A solid thud: sub drop, mid body, and a bright crack at the very front."""
    sub = thump(ctx, dur, 130 * p, sub_to * p * (0.8 + 0.4 * size), tau=0.07 + 0.09 * size, click=0.0) * (0.9 + 0.3 * size)
    body = bp(noise(ctx, dur), body_hz[0] * p, body_hz[1] * p, 2) * env_exp(dur, 0.045 + 0.04 * size, 0.0008) * 1.4
    cr = hp(noise(ctx, dur), 2800) * env_exp(dur, 0.006 + 0.004 * size, 0.0004) * crack
    cr2 = bp(noise(ctx, dur), 6000, 14000, 2) * env_exp(dur, 0.003, 0.0003) * crack * 0.7
    return S(sub, body, cr, cr2)


def steel_ring(ctx, p, f0=1250.0, dur=0.6, decay=0.35, amp=0.4, partials=PLATE):
    return modal(ctx, f0 * p, dur, partials, decay=decay, brightness=0.88, detune=0.004, inharm=0.4) * amp


def debris(ctx, dur, density=70.0, f_lo=500.0, f_hi=4500.0, decay=0.01, amp=0.35, start=0.0):
    return place(crackle(ctx, dur, density, f_lo, f_hi, decay, bias_to_start=1.2) * amp, start)


def shimmer_cluster(ctx, base, dur, count=7, spread=2.0, decay=0.7, amp=0.3, partials=GLASS):
    """A handful of glassy partials ringing together (the sparkle on rewards, magic and light)."""
    out = np.zeros(n_of(dur))
    for i in range(count):
        f = base * (1 + ctx.uniform(0, spread)) * (2 ** (i / 12.0 * ctx.uniform(0.5, 4)))
        f = min(f, 11000)
        out = out + place(modal(ctx, f, dur, partials, decay=decay * ctx.uniform(0.5, 1.0), shimmer=0.4) * amp / count * 2, ctx.uniform(0, dur * 0.3))[: len(out)]
    return out


def riser(ctx, dur, f0=200, f1=9000, amp=1.0, curve=2.4):
    n = noise(ctx, dur, "white")
    s = sweep_filter(n, freq_curve(dur, f0, f1), width=0.8)
    return s * env_swell(dur, rise=dur * 0.97, fall=dur * 0.03, curve=curve) * amp


# =========================================================================== UI
def ui_hover(ctx, take):
    p = PV[take]
    tick = modal(ctx, 2900 * p, 0.12, GLASS, decay=0.05, brightness=0.8) * 0.5
    air = bp(noise(ctx, 0.05), 5500, 10000, 2) * env_exp(0.05, 0.006, 0.0005) * 0.5
    return reverb(S(tick, air), "tiny", 0.2)


def ui_confirm(ctx, take):
    p = PV[take]
    ting = modal(ctx, A5 * p, 0.5, BELL, decay=0.22, brightness=0.8) * 0.9
    fifth = place(modal(ctx, A5 * 1.5 * p, 0.4, BELL, decay=0.16, brightness=0.75) * 0.55, 0.045)
    weight = thump(ctx, 0.12, 190 * p, 95 * p, 0.04, click=0.15) * 0.7
    return reverb(S(ting, fifth, weight), "plate", 0.22)


def ui_back(ctx, take):
    p = PV[take]
    tone = modal(ctx, D5 * p, 0.35, BELL, decay=0.13, brightness=0.7) * 0.8
    drop = sine(freq_curve(0.14, 640 * p, 330 * p), 0.14) * env_exp(0.14, 0.05, 0.002) * 0.4
    weight = thump(ctx, 0.14, 160 * p, 80 * p, 0.045, click=0.1) * 0.6
    return reverb(S(tone, drop, weight), "tiny", 0.18)


def ui_denied(ctx, take):
    p = PV[take]
    thunk = thump(ctx, 0.2, 120 * p, 62 * p, 0.05, click=0.25) * 0.9
    dis = S(sine(233 * p, 0.28), sine(247.5 * p, 0.28)) * env_exp(0.28, 0.1, 0.004) * 0.35
    buzz = lp(saw(110 * p, 0.2, 10), 1100) * env_exp(0.2, 0.07, 0.003) * 0.25
    return reverb(S(thunk, dis, buzz), "tiny", 0.15)


# =========================================================================== cards
def card_draw(ctx, take):
    p = PV[take]
    slide = whoosh(ctx, 0.17, 1700 * p, 5800 * p, width=0.7, rise=0.35, curve=1.3, color="white") * 0.9
    snap = place(click(ctx, 0.012, 2800, 9000, 0.004) * 0.7, 0.135)
    pat = place(thump(ctx, 0.07, 230 * p, 130 * p, 0.03, click=0.1) * 0.45, 0.14)
    return reverb(S(slide, snap, pat), "tiny", 0.15)


def card_pickup(ctx, take):
    p = PV[take]
    cloth = bp(noise(ctx, 0.26, "pink"), 700 * p, 3400 * p, 2) * env_swell(0.26, 0.09, 0.17, 1.3) * 1.2
    tick = click(ctx, 0.01, 3500, 9000, 0.003) * 0.6
    lift = sine(freq_curve(0.2, 260 * p, 420 * p), 0.2) * env_swell(0.2, 0.08, 0.12) * 0.12
    return reverb(S(cloth, tick, lift), "tiny", 0.15)


def card_return(ctx, take):
    p = PV[take]
    back = whoosh(ctx, 0.14, 4200 * p, 1400 * p, width=0.7, rise=0.6, curve=1.4, color="white") * 0.8
    tap = place(thump(ctx, 0.08, 210 * p, 110 * p, 0.03, click=0.2) * 0.5, 0.11)
    tick = place(click(ctx, 0.01, 3000, 8000) * 0.5, 0.115)
    return reverb(S(back, tap, tick), "tiny", 0.12)


def card_discard(ctx, take):
    p = PV[take]
    flick = whoosh(ctx, 0.24, 5200 * p, 1500 * p, width=0.8, rise=0.3, curve=1.2, color="white") * 0.85
    tap = place(thump(ctx, 0.1, 190 * p, 100 * p, 0.04, click=0.12) * 0.55, 0.2)
    spark = place(crackle(ctx, 0.2, 40, 4000, 11000, 0.002) * 0.25, 0.04)
    return reverb(S(flick, tap, spark), "chamber", 0.16)


def card_exhaust(ctx, take):
    p = PV[take]
    fire = crackle(ctx, 0.85, 130, 1800, 8000, 0.004, bias_to_start=0.8) * env_exp(0.85, 0.32, 0.01) * 0.9
    air = sweep_filter(noise(ctx, 0.8, "white"), freq_curve(0.8, 1400 * p, 9500 * p), width=0.9) * env_swell(0.8, 0.12, 0.68, 1.1) * 0.55
    breath = lp(noise(ctx, 0.7, "pink"), 900) * env_swell(0.7, 0.1, 0.6, 1.2) * 0.4
    sparkle = shimmer_cluster(ctx, 1800 * p, 0.85, count=6, decay=0.35, amp=0.28)
    return reverb(S(fire, air, breath, sparkle), "plate", 0.3)


def card_shuffle(ctx, take):
    p = PV[take]
    out = np.zeros(n_of(1.35))
    t = 0.02
    gap = 0.085
    i = 0
    while t < 1.05:
        flap = bp(noise(ctx, 0.06), 1200 * p, 6500 * p, 2) * env_exp(0.06, 0.012, 0.0006) * ctx.uniform(0.5, 1.0)
        snap = click(ctx, 0.008, 2500, 9500, 0.003) * ctx.uniform(0.3, 0.7)
        out += place(S(flap, snap), t, 1.35)[: len(out)]
        t += gap * (0.55 + 0.45 * abs(np.cos(i * 0.4)))
        i += 1
    tap = place(thump(ctx, 0.12, 200 * p, 110 * p, 0.05, click=0.15) * 0.55, 1.1, 1.35)
    return reverb(S(out, tap), "chamber", 0.16)


def card_play(ctx, take):
    p = PV[take]
    swish = whoosh(ctx, 0.28, 900 * p, 4200 * p, width=0.9, rise=0.4, curve=1.4, color="pink") * 0.9
    ting = place(modal(ctx, D6 * p, 0.3, GLASS, decay=0.14) * 0.3, 0.1)
    pad = sine(freq_curve(0.3, 160 * p, 230 * p), 0.3) * env_swell(0.3, 0.12, 0.18) * 0.18
    return reverb(S(swish, ting, pad), "chamber", 0.2)


# =========================================================================== attacks
def attack_steel(ctx, take):
    p = PV[take]
    air = whoosh(ctx, 0.4, 500 * p, 7500 * p, width=0.65, rise=0.5, curve=1.5, color="white") * 1.1
    body = whoosh(ctx, 0.36, 110 * p, 520 * p, width=1.0, rise=0.5, curve=1.4, color="pink") * 0.8
    shing = place(S(steel_ring(ctx, p, 2300, 0.4, 0.16, 0.34, PLATE), hp(noise(ctx, 0.3), 5000) * env_exp(0.3, 0.05, 0.002) * 0.12), 0.16)
    glide = sine(freq_curve(0.3, 1100 * p, 3600 * p), 0.3) * env_swell(0.3, 0.2, 0.1) * 0.07
    return reverb(S(air, body, shing, glide), "stone", 0.15)


def attack_arcane(ctx, take):
    p = PV[take]
    dur = 0.62
    whine = fm(freq_curve(dur, 520 * p, 3000 * p, "exp"), 2.01, np.linspace(7, 0.4, n_of(dur)), dur) * env_swell(dur, 0.45, 0.17, 1.6) * 0.55
    air = whoosh(ctx, dur, 700 * p, 8000 * p, width=0.8, rise=0.65, curve=1.6) * 0.7
    sparkle = crackle(ctx, dur, 90, 4500, 11500, 0.002) * env_swell(dur, 0.5, 0.12) * 0.35
    zing = place(modal(ctx, 3136 * p, 0.4, GLASS, decay=0.2, shimmer=0.3) * 0.4, 0.44)
    return reverb(S(whine, air, sparkle, zing), "plate", 0.32)


def attack_reaper(ctx, take):
    p = PV[take]
    dur = 0.62
    swing = whoosh(ctx, dur, 130 * p, 1100 * p, width=0.8, rise=0.5, curve=1.5, color="pink") * 1.3
    whisper = sweep_filter(noise(ctx, dur), freq_curve(dur, 700 * p, 1900 * p), width=0.25) * env_swell(dur, 0.38, 0.24, 1.3) * 0.45
    sub = sine(freq_curve(dur, 52 * p, 92 * p), dur) * env_swell(dur, 0.42, 0.2) * 0.5
    edge = place(modal(ctx, 540 * p, 0.45, BELL, decay=0.24, brightness=0.7) * 0.28, 0.27)
    return reverb(S(swing, whisper, sub, edge), "stone", 0.2)


def enemy_attack(ctx, take):
    p = PV[take]
    dur = 0.6
    swing = whoosh(ctx, dur, 90 * p, 1500 * p, width=0.9, rise=0.55, curve=1.6, color="pink") * 1.3
    growl = lp(saw(68 * p * (1 + 0.04 * np.sin(TAU * 9 * t_axis(dur))), dur, 14), 700) * env_swell(dur, 0.4, 0.2, 1.4) * 0.55
    air = whoosh(ctx, 0.4, 500 * p, 4500 * p, width=0.6, rise=0.55, curve=1.4, color="white") * 0.4
    return reverb(S(swing, growl, air), "stone", 0.2)


# =========================================================================== hits
def hit_steel(ctx, take):
    p = PV[take]
    dur = 0.6
    core = impact(ctx, p, 0.8, dur, crack=0.7)
    ring = steel_ring(ctx, p, ctx.uniform(1050, 1450), dur, 0.32, 0.38)
    spark = hp(noise(ctx, 0.2), 6500) * env_exp(0.2, 0.025, 0.0005) * 0.22
    scrape = place(debris(ctx, 0.3, 60, 1800, 8000, 0.003, 0.25), 0.02)
    mixd = S(core, ring, spark, scrape)
    mixd = transient_shape(saturate(mixd, 1.5), 1.6, 1.0)
    return reverb(mixd, "stone", 0.2)


def hit_arcane(ctx, take):
    p = PV[take]
    dur = 1.0
    crackp = hp(noise(ctx, dur), 3200) * env_exp(dur, 0.012, 0.0004) * 0.8
    zap = fm(2300 * p, 2.7, 9 * np.exp(-t_axis(0.25) * 28), 0.25) * env_exp(0.25, 0.07, 0.0005) * 0.6
    burst = bp(noise(ctx, dur), 900, 7000, 2) * env_exp(dur, 0.06, 0.001) * 0.8
    punch = thump(ctx, 0.3, 160 * p, 56 * p, 0.09, click=0.0) * 0.9
    ring = modal(ctx, 1760 * p, dur, GLASS, decay=0.55, shimmer=0.35) * 0.4
    sizzle = place(crackle(ctx, 0.6, 110, 3500, 11000, 0.003) * env_exp(0.6, 0.22, 0.01) * 0.4, 0.02)
    return reverb(transient_shape(S(crackp, zap, burst, punch, ring, sizzle), 1.5, 1.0), "plate", 0.35)


def hit_reaper(ctx, take):
    p = PV[take]
    dur = 0.8
    rip = sweep_filter(noise(ctx, 0.4, "white"), freq_curve(0.4, 6500 * p, 260 * p), width=0.7) * env_exp(0.4, 0.14, 0.001) * 1.3
    deep = thump(ctx, dur, 100 * p, 34 * p, 0.2, click=0.0) * 1.0
    soul = choir(ctx, [hz(38) * p, hz(45) * p], 0.7, "oo", voices=3, breath=0.3) * env_swell(0.7, 0.18, 0.5, 1.0) * 0.5
    rattle = place(crackle(ctx, 0.5, 60, 300, 2200, 0.012) * env_exp(0.5, 0.2, 0.01) * 0.5, 0.04)
    crk = hp(noise(ctx, 0.2), 3000) * env_exp(0.2, 0.008, 0.0004) * 0.6
    return reverb(transient_shape(saturate(S(rip, deep, soul, rattle, crk), 1.6), 1.5, 1.0), "stone", 0.3)


def hit_heavy(ctx, take):
    p = PV[take]
    dur = 1.1
    core = impact(ctx, p, 1.6, dur, crack=0.9, body_hz=(70, 700), sub_to=30.0) * 1.2
    groan = modal(ctx, 160 * p, dur, GONG, decay=0.9, brightness=0.8) * 0.28
    rubble = debris(ctx, 0.8, 100, 350, 3200, 0.012, 0.55, 0.03)
    crackle2 = hp(noise(ctx, 0.2), 4500) * env_exp(0.2, 0.012, 0.0004) * 0.7
    blast = lp(noise(ctx, 0.5, "pink"), 500) * env_exp(0.5, 0.18, 0.002) * 1.1
    return reverb(transient_shape(saturate(S(core, groan, rubble, crackle2, blast), 2.0), 1.7, 1.0), "hall", 0.28)


def player_hurt(ctx, take):
    p = PV[take]
    dur = 0.7
    thud = thump(ctx, dur, 110 * p, 52 * p, 0.12, click=0.0) * 1.1
    body = lp(noise(ctx, dur), 1100) * env_exp(dur, 0.075, 0.001) * 1.2
    slap = bp(noise(ctx, 0.1), 1500, 5000, 2) * env_exp(0.1, 0.01, 0.0005) * 0.6
    ring = sine(freq_curve(0.5, 3100, 2900), 0.5) * env_exp(0.5, 0.18, 0.02) * 0.05
    groan = lp(saw(76 * p, 0.55, 12), 420) * env_swell(0.55, 0.05, 0.5, 1.0) * 0.35
    return reverb(saturate(S(thud, body, slap, ring, groan), 1.7), "chamber", 0.18)


# =========================================================================== defence
def gain_block(ctx, take):
    p = PV[take]
    dur = 0.6
    raise_s = steel_ring(ctx, p, 720, dur, 0.34, 0.5, PLATE)
    shing = place(hp(noise(ctx, 0.2), 3500) * env_exp(0.2, 0.05, 0.002) * 0.3, 0.01)
    low = thump(ctx, 0.25, 150 * p, 80 * p, 0.08, click=0.1) * 0.8
    rise = sine(freq_curve(0.16, 620 * p, 1300 * p), 0.16) * env_exp(0.16, 0.07, 0.003) * 0.25
    clasp = place(click(ctx, 0.012, 2500, 9000) * 0.6, 0.12)
    return reverb(S(raise_s, shing, low, rise, clasp), "chamber", 0.22)


def block_hit(ctx, take):
    p = PV[take]
    dur = 0.5
    clang = steel_ring(ctx, p, ctx.uniform(480, 560), dur, 0.3, 0.8, PLATE)
    thud = thump(ctx, 0.2, 150 * p, 70 * p, 0.06, click=0.25) * 0.8
    tick = hp(noise(ctx, 0.05), 4500) * env_exp(0.05, 0.006, 0.0004) * 0.5
    return reverb(transient_shape(S(clang, thud, tick), 1.5, 1.0), "chamber", 0.2)


def block_break(ctx, take):
    p = PV[take]
    dur = 1.0
    burst = hp(noise(ctx, 0.5), 1800) * env_exp(0.5, 0.08, 0.0004) * 1.0
    glass = crackle(ctx, 0.7, 170, 3000, 11500, 0.006, bias_to_start=1.4) * 0.9
    shards = modal(ctx, 2600 * p, 0.5, GLASS, decay=0.22, shimmer=0.2) * 0.35
    boom = thump(ctx, 0.35, 125 * p, 48 * p, 0.1, click=0.2) * 0.95
    clang = steel_ring(ctx, p, 620, 0.7, 0.4, 0.35, PLATE)
    return reverb(transient_shape(saturate(S(burst, glass, shards, boom, clang), 1.5), 1.6, 1.0), "stone", 0.26)


# =========================================================================== resources and magic
def heal(ctx, take):
    p = PV[take]
    dur = 1.0
    notes = [D5, A5, D6]
    chime = np.zeros(n_of(dur))
    for i, f in enumerate(notes):
        chime += place(modal(ctx, f * p, dur, BELL, decay=0.65, brightness=0.7, shimmer=0.2) * 0.6, 0.075 * i, dur)[: len(chime)]
    pad = S(sine(D4 * p, dur), sine(A4 * p, dur) * 0.7, sine(D5 * p, dur) * 0.4) * env_swell(dur, 0.3, 0.7, 1.2) * 0.3
    spark = shimmer_cluster(ctx, 2400 * p, dur, count=6, decay=0.5, amp=0.35)
    return reverb(S(chime, pad, spark), "hall", 0.34)


def power(ctx, take):
    p = PV[take]
    dur = 1.0
    sub = sine(freq_curve(dur, 46 * p, 58 * p), dur) * env_swell(dur, 0.18, 0.8, 1.0) * 0.8
    rise = whoosh(ctx, 0.5, 220 * p, 1800 * p, width=0.9, rise=0.9, curve=2.0, color="pink") * 0.7
    gong = place(modal(ctx, 220 * p, 0.9, GONG, decay=0.8) * 0.6, 0.18, dur)
    hit = place(thump(ctx, 0.3, 110 * p, 44 * p, 0.12, click=0.15) * 0.8, 0.18, dur)
    shimmer = place(shimmer_cluster(ctx, 1500 * p, 0.8, count=7, decay=0.55, amp=0.4), 0.2, dur)
    return reverb(S(sub, rise, gong, hit, shimmer), "hall", 0.3)


def burn(ctx, take):
    p = PV[take]
    dur = 1.0
    ignite = whoosh(ctx, 0.2, 400 * p, 3200 * p, width=1.0, rise=0.35, curve=1.2, color="pink") * 1.0
    fwoomp = thump(ctx, 0.3, 110 * p, 52 * p, 0.1, click=0.0) * 0.7
    crack = crackle(ctx, 0.9, 85, 1500, 7500, 0.005, bias_to_start=0.7) * env_exp(dur, 0.4, 0.02) * 0.85
    hiss = hp(noise(ctx, 0.8), 3500) * env_swell(0.8, 0.05, 0.75, 1.0) * 0.12
    return reverb(S(ignite, fwoomp, crack, hiss), "tiny", 0.2)


def debuff(ctx, take):
    p = PV[take]
    dur = 0.75
    pair = S(saw(freq_curve(dur, 330 * p, 215 * p), dur, 10), saw(freq_curve(dur, 349 * p, 227 * p), dur, 10)) * 0.5
    pair = lp(pair, 1500) * env_swell(dur, 0.06, 0.69, 1.0) * 0.55
    bellc = fm(freq_curve(dur, 880 * p, 520 * p), 1.414, 5, dur) * env_exp(dur, 0.3, 0.003) * 0.4
    swirl = sweep_filter(noise(ctx, dur), freq_curve(dur, 3000, 500), width=0.7) * env_swell(dur, 0.08, 0.67) * 0.35
    low = thump(ctx, 0.25, 100 * p, 55 * p, 0.09, click=0.0) * 0.5
    return reverb(S(pair, bellc, swirl, low), "stone", 0.24)


def resonance(ctx, take):
    p = PV[take]
    dur = 0.85
    bowl = modal(ctx, G4 * p, dur, BELL, decay=0.85, brightness=0.75, shimmer=0.5) * 0.9
    swell = S(sine(G4 * 2 * p, dur), sine(D5 * p, dur) * 0.5) * env_swell(dur, 0.25, 0.6, 1.2) * 0.25
    return reverb(S(bowl, swell), "plate", 0.28)


def energy(ctx, take):
    p = PV[take]
    ping = modal(ctx, 1320 * p, 0.3, GLASS, decay=0.15, brightness=0.8) * 0.9
    blip = fm(freq_curve(0.09, 1200 * p, 2500 * p), 2.0, 3, 0.09) * env_exp(0.09, 0.035, 0.001) * 0.35
    tick = click(ctx, 0.008, 4000, 10000) * 0.4
    return reverb(S(ping, blip, tick), "tiny", 0.18)


# =========================================================================== rewards, economy
def gold_collect(ctx, take):
    p = PV[take]
    dur = 0.7
    out = np.zeros(n_of(dur))
    for i in range(7):
        f = ctx.uniform(2100, 3500) * p
        out += place(modal(ctx, f, 0.4, COIN, decay=0.17, brightness=0.8) * ctx.uniform(0.4, 0.9), ctx.uniform(0, 0.2), dur)[: len(out)]
    ding = place(modal(ctx, A5 * p, 0.5, BELL, decay=0.3, brightness=0.7) * 0.4, 0.16, dur)
    return reverb(S(out, ding), "plate", 0.25)


def gold_spend(ctx, take):
    p = PV[take]
    dur = 0.7
    out = np.zeros(n_of(dur))
    for i in range(4):
        f = (2900 - i * 260) * p
        out += place(modal(ctx, f, 0.4, COIN, decay=0.13, brightness=0.8) * (0.8 - i * 0.12), i * 0.07, dur)[: len(out)]
    drop = place(thump(ctx, 0.15, 180 * p, 90 * p, 0.04, click=0.2) * 0.5, 0.28, dur)
    return reverb(S(out, drop), "chamber", 0.2)


def reward_reveal(ctx, take):
    p = PV[take]
    dur = 1.1
    rs = riser(ctx, 0.75, 1500 * p, 10000 * p, 0.5, curve=2.0)
    chord = S(*[sine(f * p, dur) * a for f, a in ((D5, 1.0), (F5, 0.7), (A5, 0.6))]) * env_swell(dur, 0.7, 0.4, 1.4) * 0.22
    chime = place(modal(ctx, D6 * p, 0.6, BELL, decay=0.5, shimmer=0.3) * 0.6, 0.72, dur)
    spark = place(shimmer_cluster(ctx, 2200 * p, 0.5, count=8, decay=0.4, amp=0.5), 0.6, dur)
    return reverb(S(rs, chord, chime, spark), "hall", 0.34)


def reward_card(ctx, take):
    p = PV[take]
    dur = 0.6
    flip = bp(noise(ctx, 0.12), 1500, 7000, 2) * env_exp(0.12, 0.02, 0.001) * 0.8
    ting = place(modal(ctx, hz(82) * p, dur, BELL, decay=0.4, brightness=0.7) * 0.8, 0.05, dur)
    sparkle = place(shimmer_cluster(ctx, 2600 * p, 0.4, count=5, decay=0.3, amp=0.35), 0.06, dur)
    return reverb(S(flip, ting, sparkle), "plate", 0.28)


def reward_relic(ctx, take):
    p = PV[take]
    dur = 1.3
    bell = modal(ctx, D4 * p, dur, BELL, decay=1.1, brightness=0.8, shimmer=0.25) * 0.9
    oct_ = modal(ctx, D5 * p, dur, BELL, decay=0.9, brightness=0.7) * 0.4
    ch = choir(ctx, [D3 * p, A3 * p, D4 * p], dur, "ah", voices=3, breath=0.1) * env_swell(dur, 0.45, 0.75, 1.3) * 0.28
    sub = thump(ctx, 0.3, 90 * p, 44 * p, 0.1, click=0.0) * 0.5
    spark = shimmer_cluster(ctx, 2100 * p, dur, count=6, decay=0.6, amp=0.3)
    return reverb(S(bell, oct_, ch, sub, spark), "hall", 0.36)


def reward_shard(ctx, take):
    p = PV[take]
    dur = 1.0
    ring = modal(ctx, G5 * p, dur, GLASS, decay=0.9, brightness=0.8, shimmer=0.5) * 0.9
    ring2 = place(modal(ctx, D6 * p, 0.8, GLASS, decay=0.6, shimmer=0.4) * 0.5, 0.06, dur)
    whine = sine(freq_curve(0.5, 3600, 2400), 0.5) * env_exp(0.5, 0.2, 0.01) * 0.1
    ping = click(ctx, 0.01, 5000, 12000) * 0.5
    return reverb(S(ring, ring2, whine, ping), "plate", 0.34)


def binding(ctx, take):
    p = PV[take]
    dur = 0.6
    clasp = click(ctx, 0.016, 1800, 8000, 0.005) * 0.9
    ring = steel_ring(ctx, p, 920, dur, 0.28, 0.55, PLATE)
    low = thump(ctx, 0.2, 140 * p, 70 * p, 0.07, click=0.15) * 0.7
    rise = sine(freq_curve(0.22, 500 * p, 1500 * p), 0.22) * env_swell(0.22, 0.12, 0.1) * 0.15
    return reverb(S(clasp, ring, low, rise), "chamber", 0.22)


def fateweave(ctx, take):
    p = PV[take]
    dur = 1.1
    out = np.zeros(n_of(dur))
    for i, f in enumerate([D5, A5, F5, C6]):
        pl = pluck(ctx, f * p, 0.8, 0.9965, 0.6) * env_exp(0.8, 0.4, 0.001)
        out += place(pl * (0.7 - i * 0.07), 0.07 * i, dur)[: len(out)]
    pad = S(sine(D4 * p, dur), sine(A4 * p, dur) * 0.6) * env_swell(dur, 0.35, 0.65, 1.2) * 0.2
    air = whoosh(ctx, 0.5, 1200 * p, 7000 * p, width=0.9, rise=0.7, curve=1.6) * 0.25
    return reverb(S(out, pad, air), "hall", 0.34)


def upgrade(ctx, take):
    p = PV[take]
    dur = 0.9
    strike = thump(ctx, 0.2, 170 * p, 80 * p, 0.06, click=0.4) * 0.9
    anvil = steel_ring(ctx, p, 1050, dur, 0.5, 0.6, PLATE)
    sh = place(sweep_filter(noise(ctx, 0.6), freq_curve(0.6, 1500 * p, 9000 * p), width=0.5) * env_swell(0.6, 0.25, 0.35) * 0.35, 0.05, dur)
    ding = place(modal(ctx, D6 * p, 0.6, BELL, decay=0.45, shimmer=0.2) * 0.5, 0.1, dur)
    return reverb(S(strike, anvil, sh, ding), "stone", 0.26)


def remove_card(ctx, take):
    p = PV[take]
    dur = 0.8
    tear = bp(noise(ctx, 0.28), 1200, 7500, 2) * (0.5 + 0.5 * np.abs(noise(ctx, 0.28, "pink"))) * env_swell(0.28, 0.03, 0.25, 0.8) * 1.5
    snip = place(S(modal(ctx, 3400 * p, 0.2, PLATE, decay=0.06) * 0.6, hp(noise(ctx, 0.05), 5000) * env_exp(0.05, 0.008) * 0.4), 0.04, dur)
    thud = place(thump(ctx, 0.2, 130 * p, 70 * p, 0.06, click=0.1) * 0.6, 0.3, dur)
    wisp = place(sweep_filter(noise(ctx, 0.4), freq_curve(0.4, 5000, 900), width=0.8) * env_exp(0.4, 0.14, 0.01) * 0.3, 0.12, dur)
    return reverb(S(tear, snip, thud, wisp), "chamber", 0.2)


# =========================================================================== outcomes and set pieces
def victory(ctx, take):
    p = PV[take]
    dur = 2.4
    chord = np.zeros(n_of(dur))
    for i, f in enumerate([D3, A3, D4, hz(66), A4]):   # D major, open voicing
        chord += place(brass(ctx, f * p, 1.9, attack=0.08 + i * 0.01, open_to=4200) * (0.9 - i * 0.08), 0.05 * i, dur)[: len(chord)]
    timp = thump(ctx, 0.5, 100 * p, 38 * p, 0.2, click=0.2) * 0.9
    cym = place(hp(noise(ctx, 1.6), 5000) * env_swell(1.6, 0.35, 1.25, 1.2) * 0.3, 0.0, dur)
    bellh = place(modal(ctx, D5 * p, 1.8, BELL, decay=1.4, shimmer=0.3) * 0.5, 0.12, dur)
    spark = place(shimmer_cluster(ctx, 2400 * p, 1.6, count=9, decay=0.9, amp=0.5), 0.3, dur)
    return reverb(S(chord * 0.55, timp, cym, bellh, spark), "hall", 0.38)


def defeat(ctx, take):
    p = PV[take]
    dur = 2.4
    toll = S(modal(ctx, D3 * p, dur, BELL, decay=1.8, brightness=0.8) * 1.0, place(modal(ctx, hz(46) * p, dur, BELL, decay=1.6, brightness=0.75) * 0.8, 0.7, dur))
    drone = lp(saw(freq_curve(dur, 55 * p, 41 * p), dur, 18), 380) * env_swell(dur, 0.2, 1.8, 1.0) * 0.45
    fall = sweep_filter(noise(ctx, dur), freq_curve(dur, 2200, 130), width=0.9) * env_swell(dur, 0.1, 1.7, 1.0) * 0.3
    hit = thump(ctx, 0.5, 90 * p, 34 * p, 0.2, click=0.0) * 0.9
    return reverb(S(toll * 0.8, drone, fall, hit), "cathedral", 0.4)


def boss_intro(ctx, take):
    p = PV[take]
    dur = 2.6
    drone = S(lp(saw(41 * p, dur, 20), 220), lp(saw(82.4 * p, dur, 16), 500) * 0.5) * env_swell(dur, 0.7, 1.9, 1.5) * 0.7
    rs = place(riser(ctx, 0.7, 150, 5500, 0.6, 2.0), 0.0, dur)
    boom = place(thump(ctx, 1.0, 85 * p, 30 * p, 0.35, click=0.15) * 1.0, 0.7, dur)
    gong = place(modal(ctx, 95 * p, 1.9, GONG, decay=1.6) * 0.55, 0.7, dur)
    ch = place(choir(ctx, [D2 * p * 2, A2 * p * 2, D3 * p], 1.8, "oo", voices=3, breath=0.1) * env_swell(1.8, 0.5, 1.3) * 0.3, 0.6, dur)
    return reverb(S(drone, rs, boom, gong, ch), "cathedral", 0.38)


def boss_phase(ctx, take):
    p = PV[take]
    dur = 1.8
    rs = riser(ctx, 0.9, 300, 11000, 0.8, 2.2)
    boom = place(thump(ctx, 0.8, 95 * p, 30 * p, 0.28, click=0.2) * 1.1, 0.9, dur)
    crack = place(S(crackle(ctx, 0.6, 140, 800, 9000, 0.006, bias_to_start=1.0) * 0.8, hp(noise(ctx, 0.2), 3500) * env_exp(0.2, 0.01) * 0.8), 0.9, dur)
    groan = place(modal(ctx, 140 * p, 1.2, GONG, decay=0.9) * 0.5, 0.92, dur)
    return reverb(S(rs, boom, crack, groan), "hall", 0.32)


def map_step(ctx, take):
    p = PV[take]
    step = S(thump(ctx, 0.14, 125 * p, 62 * p, 0.04, click=0.0) * 0.8, lp(noise(ctx, 0.1), 1700) * env_exp(0.1, 0.02, 0.001) * 0.6)
    thread = place(pluck(ctx, hz(81) * p, 0.4, 0.995, 0.5) * env_exp(0.4, 0.2, 0.001) * 0.4, 0.03)
    return reverb(S(step, thread), "stone", 0.18)


def event_reveal(ctx, take):
    p = PV[take]
    dur = 0.7
    swell = sweep_filter(noise(ctx, 0.35), freq_curve(0.35, 800, 5200), width=0.7) * env_swell(0.35, 0.3, 0.05, 1.5) * 0.45
    chime = place(modal(ctx, hz(84) * p, dur, BELL, decay=0.5, shimmer=0.3) * 0.7, 0.28, dur)
    return reverb(S(swell, chime), "hall", 0.3)


def event_clock(ctx, take):
    p = PV[take]
    out = np.zeros(n_of(0.6))
    for i, at in enumerate((0.0, 0.3)):
        tk = S(modal(ctx, 2100 * p * (1 - 0.05 * i), 0.1, WOOD, decay=0.03) * 0.8, click(ctx, 0.01, 2500, 8000) * 0.6)
        out += place(tk, at, 0.6)[: len(out)]
    res = modal(ctx, 160 * p, 0.6, WOOD, decay=0.25) * 0.4
    return reverb(S(out, res), "stone", 0.25)


def event_whisper(ctx, take):
    p = PV[take]
    dur = 1.0
    out = np.zeros(n_of(dur))
    for i in range(4):
        f1, f2 = ctx.uniform(500, 900), ctx.uniform(1500, 2600)
        seg = sweep_filter(noise(ctx, 0.28), freq_curve(0.28, f1, f2 * 0.9), width=0.2)
        out += place(seg * env_swell(0.28, 0.1, 0.18) * 0.7, 0.18 * i, dur)[: len(out)]
    chill = sine(freq_curve(0.6, 3300 * p, 2700 * p), 0.6) * env_swell(0.6, 0.25, 0.35) * 0.05
    return reverb(S(out, chill), "cathedral", 0.42)


def event_thread(ctx, take):
    p = PV[take]
    dur = 0.9
    pl = pluck(ctx, G3 * p, 0.8, 0.997, 0.55) * env_exp(0.8, 0.5, 0.001) * 0.8
    over = shimmer_cluster(ctx, 1400 * p, dur, count=5, decay=0.5, amp=0.3)
    gliss = sweep_filter(noise(ctx, 0.5), freq_curve(0.5, 6000, 1500), width=0.5) * env_swell(0.5, 0.05, 0.45) * 0.18
    return reverb(S(pl, over, gliss), "hall", 0.3)


def end_turn(ctx, take):
    p = PV[take]
    dur = 0.8
    thunk = thump(ctx, 0.25, 140 * p, 62 * p, 0.08, click=0.3) * 0.95
    wood = modal(ctx, 310 * p, 0.4, WOOD, decay=0.1) * 0.6
    gong = modal(ctx, 110 * p, dur, GONG, decay=0.55) * 0.45
    tick = hp(noise(ctx, 0.03), 4000) * env_exp(0.03, 0.005) * 0.4
    return reverb(S(thunk, wood, gong, tick), "stone", 0.26)


def turn_start(ctx, take):
    p = PV[take]
    dur = 1.0
    bell = modal(ctx, D5 * p, dur, BELL, decay=0.85, brightness=0.75, shimmer=0.3) * 0.85
    air = whoosh(ctx, 0.5, 500 * p, 4500 * p, width=0.9, rise=0.6, curve=1.6, color="pink") * 0.6
    sub = sine(freq_curve(0.5, 55 * p, 73 * p), 0.5) * env_swell(0.5, 0.18, 0.32) * 0.4
    return reverb(S(bell, air, sub), "hall", 0.3)


# =========================================================================== new cues: the opening sequence (8.5 s of black-and-gold)
def intro_awaken(ctx, take):
    p = PV[take]
    dur = 2.6
    rumble = lp(noise(ctx, dur, "brown"), 240) * env_swell(dur, 1.9, 0.7, 1.8) * 1.6
    wind = sweep_filter(noise(ctx, dur, "pink"), freq_curve(dur, 250, 900), width=0.8) * env_swell(dur, 1.4, 1.2, 1.5) * 0.35
    scrape = place(modal(ctx, 70 * p, 2.2, GONG, decay=1.8, brightness=0.8) * 0.28, 0.3, dur)
    whisper = place(sweep_filter(noise(ctx, 1.4), freq_curve(1.4, 900, 2300), width=0.18) * env_swell(1.4, 0.6, 0.8) * 0.25, 0.9, dur)
    sub = sine(freq_curve(dur, 34 * p, 46 * p), dur) * env_swell(dur, 1.8, 0.8, 1.6) * 0.7
    return reverb(S(rumble, wind, scrape, whisper, sub), "cavern", 0.45)


def intro_door(ctx, take):
    p = PV[take]
    dur = 1.7
    n = n_of(dur)
    grind = bp(noise(ctx, dur, "brown"), 60, 650, 2) * (1 + 0.55 * saw(11 * p, dur, 6)) * env_swell(dur, 0.25, 1.4, 1.0) * 1.8
    sub = thump(ctx, 1.2, 60 * p, 30 * p, 0.5, click=0.0) * 0.9
    seam = S(*[modal(ctx, f * p, dur, BELL, decay=1.2, brightness=0.8, shimmer=0.5) * 0.25 for f in (G5, D6, A5)]) * env_swell(dur, 1.2, 0.5, 1.6)
    dust = crackle(ctx, dur, 45, 600, 3500, 0.01) * env_swell(dur, 0.2, 1.4) * 0.25
    return reverb(S(grind, sub, seam, dust), "cathedral", 0.36)


def intro_flash(ctx, take):
    p = PV[take]
    dur = 1.7
    boom = thump(ctx, 1.0, 70 * p, 32 * p, 0.3, click=0.1) * 1.0
    shh = sweep_filter(noise(ctx, 1.0), freq_curve(1.0, 9000, 1200), width=0.9) * env_exp(1.0, 0.28, 0.003) * 0.9
    cluster = shimmer_cluster(ctx, 1800 * p, 1.6, count=9, decay=1.1, amp=0.9)
    down = sine(freq_curve(1.2, 1500 * p, 180 * p), 1.2) * env_exp(1.2, 0.3, 0.003) * 0.25
    return reverb(transient_shape(S(boom, shh, cluster, down), 1.5, 1.0), "hall", 0.4)


def intro_streaks(ctx, take):
    p = PV[take]
    dur = 0.95
    low = lp(saw(freq_curve(dur, 70 * p, 300 * p, "pow"), dur, 14), 900) * env_swell(dur, 0.9, 0.05, 2.0) * 0.6
    mid = fm(freq_curve(dur, 260 * p, 1300 * p, "pow"), 2.01, 3, dur) * env_swell(dur, 0.9, 0.05, 2.2) * 0.35
    high = sweep_filter(noise(ctx, dur), freq_curve(dur, 900, 6500, "pow"), width=0.45) * env_swell(dur, 0.9, 0.05, 2.0) * 0.7
    suck = place(sine(freq_curve(0.15, 2500, 200), 0.15) * env_swell(0.15, 0.03, 0.12) * 0.15, 0.8, dur)
    return reverb(S(low, mid, high, suck), "hall", 0.22)


def intro_clash(ctx, take):
    p = PV[take]
    dur = 3.6
    boom = thump(ctx, 1.8, 72 * p, 26 * p, 0.55, click=0.2) * 1.2
    crack = S(hp(noise(ctx, 0.4), 2800) * env_exp(0.4, 0.02, 0.0004) * 0.9, hp(noise(ctx, 0.6), 700) * env_exp(0.6, 0.09, 0.001) * 0.7)
    clang = S(modal(ctx, 150 * p, dur, GONG, decay=1.7, brightness=0.82) * 0.5, steel_ring(ctx, p, 880, 2.0, 1.0, 0.45, PLATE))
    magic = S(crackle(ctx, 1.4, 160, 1500, 11000, 0.004, bias_to_start=1.0) * 0.7, shimmer_cluster(ctx, 1500 * p, 2.0, count=10, decay=1.2, amp=0.7))
    blast = lp(noise(ctx, 1.6, "pink"), 600) * env_exp(1.6, 0.4, 0.002) * 1.0
    return reverb(transient_shape(saturate(S(boom, crack, clang, magic, blast), 1.8), 1.8, 1.0), "cathedral", 0.38)


def intro_crown(ctx, take):
    p = PV[take]
    dur = 3.8
    boom = thump(ctx, 1.6, 92 * p, 31 * p, 0.5, click=0.2) * 1.3
    bell = modal(ctx, D3 * p, dur, BELL, decay=2.6, brightness=0.85, shimmer=0.2) * 0.9
    high = modal(ctx, D6 * p, 2.4, BELL, decay=1.4, shimmer=0.3) * 0.35
    metal = steel_ring(ctx, p, 1175, 1.8, 0.9, 0.3, PLATE)
    rumble = lp(noise(ctx, 2.4, "brown"), 200) * env_exp(2.4, 0.9, 0.01) * 1.1
    crack = hp(noise(ctx, 0.25), 3000) * env_exp(0.25, 0.012, 0.0004) * 0.8
    return reverb(transient_shape(saturate(S(boom, bell, high, metal, rumble, crack), 1.7), 1.7, 1.0), "cathedral", 0.44)


def intro_shimmer(ctx, take):
    p = PV[take]
    dur = 3.0
    out = np.zeros(n_of(dur))
    for i, f in enumerate([D5, F5, A5, C6, D6, F5 * 2, A5 * 2]):
        out += place(modal(ctx, f * p, 2.0, BELL, decay=1.2 - 0.07 * i, brightness=0.7, shimmer=0.3) * (0.7 - i * 0.05), 0.07 * i + ctx.uniform(0, 0.03), dur)[: len(out)]
    pad = S(*[sine(f * p, dur) * a for f, a in ((D4, 1.0), (A4, 0.7), (D5, 0.5), (F5, 0.35))]) * env_swell(dur, 0.9, 2.0, 1.2) * 0.2
    spark = shimmer_cluster(ctx, 2600 * p, 2.6, count=12, decay=1.0, amp=0.7)
    return reverb(S(out, pad, spark), "hall", 0.46)


# =========================================================================== new cues: combat extras
def enemy_death(ctx, take):
    p = PV[take]
    dur = 1.5
    whomp = thump(ctx, 0.8, 95 * p, 32 * p, 0.28, click=0.1) * 1.1
    soul = sweep_filter(noise(ctx, 1.0), freq_curve(1.0, 4500 * p, 200 * p), width=0.7) * env_swell(1.0, 0.08, 0.92, 1.0) * 0.7
    rubble = debris(ctx, 0.9, 85, 350, 3000, 0.012, 0.5, 0.05)
    clank = place(steel_ring(ctx, p, 330, 0.8, 0.5, 0.45, PLATE), 0.22, dur)
    groan = lp(saw(freq_curve(1.1, 90 * p, 38 * p), 1.1, 14), 380) * env_swell(1.1, 0.08, 1.0, 1.0) * 0.5
    return reverb(transient_shape(saturate(S(whomp, soul, rubble, clank, groan), 1.5), 1.4, 1.0), "stone", 0.32)


def crit_hit(ctx, take):
    p = PV[take]
    dur = 1.2
    core = hit_heavy(ctx, take)
    core = core if core.ndim == 1 else core.mean(1)
    gold = place(modal(ctx, hz(93) * p, 1.0, BELL, decay=0.7, shimmer=0.25) * 0.55, 0.0, dur)
    rev = whoosh(ctx, 0.3, 800, 9000, width=0.8, rise=0.97, curve=2.0) * 0.4
    return reverb(S(core[: n_of(dur)] * 0.9, gold, rev), "hall", 0.22)


DESIGNS = {
    # UI
    "UiHover": ui_hover, "UiConfirm": ui_confirm, "UiBack": ui_back, "UiDenied": ui_denied,
    # cards
    "CardDraw": card_draw, "CardPickup": card_pickup, "CardReturn": card_return, "CardDiscard": card_discard, "CardExhaust": card_exhaust, "CardShuffle": card_shuffle,
    # attacks and hits
    "AttackSteel": attack_steel, "AttackArcane": attack_arcane, "AttackReaper": attack_reaper, "EnemyAttack": enemy_attack,
    "HitSteel": hit_steel, "HitArcane": hit_arcane, "HitReaper": hit_reaper, "HitHeavy": hit_heavy, "PlayerHurt": player_hurt,
    # defence, resources, magic
    "GainBlock": gain_block, "BlockHit": block_hit, "BlockBreak": block_break, "Heal": heal, "Power": power, "Burn": burn, "Debuff": debuff, "Resonance": resonance, "Energy": energy,
    # rewards and economy
    "GoldCollect": gold_collect, "GoldSpend": gold_spend, "RewardReveal": reward_reveal, "RewardCard": reward_card, "RewardRelic": reward_relic, "RewardShard": reward_shard,
    "Binding": binding, "Fateweave": fateweave, "Upgrade": upgrade, "RemoveCard": remove_card,
    # outcomes and set pieces
    "Victory": victory, "Defeat": defeat, "BossIntro": boss_intro, "BossPhase": boss_phase, "MapStep": map_step,
    "EventReveal": event_reveal, "EventClock": event_clock, "EventWhisper": event_whisper, "EventThread": event_thread, "EndTurn": end_turn, "TurnStart": turn_start,
    # new cues
    "IntroAwaken": intro_awaken, "IntroDoor": intro_door, "IntroFlash": intro_flash, "IntroStreaks": intro_streaks, "IntroClash": intro_clash, "IntroCrown": intro_crown, "IntroShimmer": intro_shimmer,
    "EnemyDeath": enemy_death, "CardPlay": card_play, "CritHit": crit_hit,
}

# Loudness (RMS of the loudest quarter second, dBFS). Cues that exist in the original set start from what the original recordings measure,
# measured the same way, so the catalog's gains and the mix stay balanced; impacts sit a little hotter, very quiet UI a little louder.
ORIGINAL_LOUD = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "original_loudness.json")))
DELTA = {"HitSteel": 2.0, "HitHeavy": 6.0, "HitArcane": 2.0, "HitReaper": 2.0, "PlayerHurt": 1.5, "BlockBreak": 1.5, "BlockHit": 1.0, "EnemyAttack": 1.5, "AttackSteel": 1.0,
         "UiConfirm": 4.0, "UiBack": 4.5, "UiHover": 0.0, "TurnStart": 3.5, "Binding": 1.5, "RemoveCard": 2.0, "BossPhase": 3.0, "Victory": 2.5, "RewardShard": 1.5}
NEW_CUE_LOUD = {"IntroAwaken": -26.0, "IntroDoor": -23.0, "IntroFlash": -20.0, "IntroStreaks": -23.0, "IntroClash": -18.5, "IntroCrown": -18.5, "IntroShimmer": -24.0,
                "EnemyDeath": -19.5, "CardPlay": -27.0, "CritHit": -18.0}
TARGET_RMS = {c: (ORIGINAL_LOUD[c] + DELTA.get(c, 0.0)) if c in ORIGINAL_LOUD else NEW_CUE_LOUD[c] for c in DESIGNS}

# How long each reverb may ring on after the dry sound ends, where the room's default is not right (seconds).
TAIL_OVERRIDE = {"UiConfirm": 0.28, "UiBack": 0.2, "GoldCollect": 0.45, "CardExhaust": 0.45, "Heal": 0.9, "Power": 0.9, "RewardReveal": 0.8, "RewardRelic": 1.0, "Fateweave": 0.8,
                 "Resonance": 0.5, "TurnStart": 0.7, "EventReveal": 0.6, "EventWhisper": 1.2, "EventThread": 0.8, "Victory": 1.1, "Defeat": 1.6, "BossIntro": 1.6, "BossPhase": 0.9,
                 "IntroAwaken": 1.6, "IntroDoor": 1.2, "IntroFlash": 1.2, "IntroStreaks": 0.3, "IntroClash": 1.8, "IntroCrown": 2.0, "IntroShimmer": 1.6, "EnemyDeath": 0.8, "CritHit": 0.8}
