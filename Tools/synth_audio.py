#!/usr/bin/env python3
"""Synthesises every sound in Alibi & Co. (no samples, no third-party audio).

SFX are built from filtered noise, modal resonators (wood, glass, metal), Karplus-Strong strings and
FM tones. The music is brushed noir jazz: Karplus-Strong walking bass, FM Rhodes, vibraphone,
brushes and ride, played from small hand-written scores and rendered with a synthetic room reverb.

    .venv/bin/python Tools/synth_audio.py            # everything
    .venv/bin/python Tools/synth_audio.py sfx        # only sound effects
    .venv/bin/python Tools/synth_audio.py music      # only music + ambience
"""
import math
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1986)


# --------------------------------------------------------------------------- basics

def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def lufs(x):
    """Integrated loudness (ITU-R BS.1770 K-weighting, 400 ms blocks, absolute + relative gates)."""
    x = x if x.ndim == 2 else x[:, None]
    b1, a1 = [1.53512485958697, -2.69169618940638, 1.19839281085285], [1, -1.69065929318241, 0.73248077421585]
    b2, a2 = [1, -2, 1], [1, -1.99004745483398, 0.99007225036621]
    y = signal.lfilter(b2, a2, signal.lfilter(b1, a1, x, axis=0), axis=0)
    blk, hop = int(0.4 * SR), int(0.1 * SR)
    ms = np.array([(y[i:i + blk] ** 2).mean(0).sum() for i in range(0, max(1, len(y) - blk + 1), hop)])
    loud = -0.691 + 10 * np.log10(ms + 1e-12)
    gated = ms[loud > -70]
    if gated.size == 0:
        return -99.0
    rel = -0.691 + 10 * np.log10(gated.mean()) - 10
    gated = ms[(loud > -70) & (loud > rel)]
    return -0.691 + 10 * np.log10(gated.mean())


def write(name, x, peak=0.89, target_lufs=None):
    """Write mono (1-D) or stereo (N x 2) float audio as 16-bit WAV, peak-normalised, then (for
    music) turned down to a loudness target so cues match when they crossfade."""
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x)) if x.size else 0
    if m > 0:
        x = x / m * peak
    if target_lufs is not None:
        gain = 10 ** ((target_lufs - lufs(x)) / 20)
        x = x * min(gain, peak / max(1e-9, np.max(np.abs(x))))
        print(f"  {name}: {lufs(x):.1f} LUFS")
    if x.ndim == 1:
        ch, data = 1, x
    else:
        ch, data = 2, x.reshape(-1)
    pcm = np.clip(data * 32767, -32768, 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def sos(kind, f, order=2):
    if kind == "bp":
        return signal.butter(order, [max(20, f[0]), min(SR / 2 - 100, f[1])], btype="bandpass", fs=SR, output="sos")
    return signal.butter(order, min(SR / 2 - 100, max(20, f)), btype=kind, fs=SR, output="sos")


def lp(x, f, order=2):
    return signal.sosfilt(sos("low", f, order), x)


def hp(x, f, order=2):
    return signal.sosfilt(sos("high", f, order), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(sos("bp", (lo, hi), order), x)


def noise(n, color="white"):
    w = rng.standard_normal(n)
    if color == "white":
        return w
    if color == "pink":
        # Voss-McCartney-ish via filtering: -3 dB/oct approximation.
        b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
        a = [1, -2.494956002, 2.017265875, -0.522189400]
        return signal.lfilter(b, a, w) * 3
    if color == "brown":
        x = np.cumsum(w)
        x -= np.linspace(x[0], x[-1], n)
        return x / (np.max(np.abs(x)) + 1e-9)
    return w


def env_exp(n, tau, attack=0.002):
    t = np.arange(n) / SR
    e = np.exp(-t / max(tau, 1e-4))
    a = int(attack * SR)
    if a > 0:
        e[:a] *= np.linspace(0, 1, a)
    return e


def env_adsr(n, a, d, s, r):
    e = np.zeros(n)
    ai, di, ri = int(a * SR), int(d * SR), int(r * SR)
    si = max(0, n - ai - di - ri)
    i = 0
    e[i:i + ai] = np.linspace(0, 1, ai, endpoint=False); i += ai
    e[i:i + di] = np.linspace(1, s, di, endpoint=False); i += di
    e[i:i + si] = s; i += si
    e[i:i + ri] = np.linspace(s, 0, min(ri, n - i))[: n - i] if i < n else []
    return e


def modal(freqs, decays, amps, dur, jitter=0.0):
    t = t_axis(dur)
    x = np.zeros_like(t)
    for f, d, a in zip(freqs, decays, amps):
        f2 = f * (1 + jitter * rng.uniform(-1, 1))
        x += a * np.sin(2 * np.pi * f2 * t + rng.uniform(0, 6.28)) * np.exp(-t / d)
    return x


def pad(x, dur):
    n = int(dur * SR)
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def at(x, delay, total=None):
    d = int(delay * SR)
    out = np.zeros(d + len(x) if total is None else int(total * SR))
    end = min(len(out), d + len(x))
    out[d:end] += x[: end - d]
    return out


def reverb_ir(seconds=1.8, damp=6000, pre=0.012, stereo=True, seed=0):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    chans = []
    for c in range(2 if stereo else 1):
        x = r.standard_normal(n) * np.exp(-t * 6.9 / seconds)
        # Darker as it decays.
        lo = signal.sosfilt(sos("low", damp), x)
        lo2 = signal.sosfilt(sos("low", damp * 0.35), x)
        k = np.clip(t / seconds, 0, 1)
        x = lo * (1 - k) + lo2 * k
        x = np.concatenate([np.zeros(int(pre * SR)), x])
        chans.append(x / np.sqrt(np.sum(x ** 2)))
    return np.stack(chans, -1) if stereo else chans[0]


def reverb(x, seconds=1.6, mix_wet=0.25, damp=6000, seed=0):
    """Mono or stereo in, stereo out."""
    ir = reverb_ir(seconds, damp, seed=seed)
    if x.ndim == 1:
        x = np.stack([x, x], -1)
    wet = np.stack([signal.fftconvolve(x[:, c], ir[:, c]) for c in range(2)], -1)
    dry = np.zeros_like(wet)
    dry[: len(x)] = x
    return dry * (1 - mix_wet) + wet * mix_wet * 2.2


def fade(x, fin=0.003, fout=0.02):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)[:, None] if x.ndim == 2 else np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)[:, None] if x.ndim == 2 else np.linspace(1, 0, b)
    return x


def mono(x):
    return x if x.ndim == 1 else x.mean(axis=1)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


# --------------------------------------------------------------------------- instruments

def ks_pluck(freq, dur, damping=0.996, bright=0.5, seed=None):
    """Karplus-Strong plucked string."""
    r = np.random.default_rng(seed)
    n = int(dur * SR)
    period = max(2, int(round(SR / freq)))
    buf = r.uniform(-1, 1, period)
    buf = lp(buf, 400 + bright * 3000, 1)
    out = np.zeros(n)
    idx = 0
    prev = 0.0
    for i in range(n):
        v = buf[idx]
        nv = damping * 0.5 * (v + prev)
        prev = v
        buf[idx] = nv
        out[i] = v
        idx = (idx + 1) % period
    return out


def upright_bass(freq, dur, vel=1.0):
    # Excite each string from the track's seeded rng so a rebuild reproduces the same take.
    x = ks_pluck(freq, dur + 0.25, damping=0.9965, bright=0.25 + 0.2 * vel, seed=int(rng.integers(1 << 31)))
    body = lp(x, 900, 2) + 0.35 * bp(x, 70, 200)
    thump = lp(noise(int(0.03 * SR)), 300) * env_exp(int(0.03 * SR), 0.01) * 0.6
    body[: len(thump)] += thump
    e = np.ones(len(body))
    r = int(0.08 * SR)
    stop = int(dur * SR)
    e[stop:stop + r] = np.linspace(1, 0, min(r, len(e) - stop))
    e[stop + r:] = 0
    return body * e * vel


def rhodes(freq, dur, vel=0.8):
    t = t_axis(dur + 0.6)
    idx = 1.6 * vel * np.exp(-t / 0.5) + 0.25
    mod = np.sin(2 * np.pi * freq * t) * idx
    car = np.sin(2 * np.pi * freq * t + mod)
    tine = 0.18 * vel * np.sin(2 * np.pi * freq * 14.2 * t) * np.exp(-t / 0.035)
    bark = 0.25 * np.sin(2 * np.pi * freq * 2 * t + mod * 0.5) * np.exp(-t / 0.2)
    x = (car + tine + bark) * np.exp(-t / (1.6 + 200 / freq))
    n = len(t)
    stop = int(dur * SR)
    rel = np.ones(n)
    rel[stop:] = np.exp(-np.arange(n - stop) / (0.12 * SR))
    trem = 1 + 0.12 * np.sin(2 * np.pi * 5.2 * t)
    return x * rel * trem * vel


def vibes(freq, dur, vel=0.8):
    t = t_axis(dur + 1.5)
    partials = [(1, 1.0, 2.6), (3.92, 0.28, 0.6), (9.2, 0.1, 0.18)]
    x = np.zeros_like(t)
    for ratio, a, d in partials:
        x += a * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t / d)
    motor = 1 + 0.28 * np.sin(2 * np.pi * 4.6 * t)
    strike = lp(noise(len(t)), 4000) * np.exp(-t / 0.004) * 0.15
    return (x * motor + strike) * vel


def ride(dur=1.2, vel=0.6, seed=None):
    r = np.random.default_rng(seed)
    t = t_axis(dur)
    freqs = [3150, 4310, 5430, 6280, 7120, 8540, 9300]
    x = np.zeros_like(t)
    for f in freqs:
        x += np.sin(2 * np.pi * f * (1 + r.uniform(-0.01, 0.01)) * t + r.uniform(0, 6.28)) * np.exp(-t / r.uniform(0.4, 1.1))
    ping = hp(r.standard_normal(len(t)), 5000) * np.exp(-t / 0.05) * 0.8
    wash = hp(r.standard_normal(len(t)), 6500) * np.exp(-t / 0.6) * 0.3
    return hp(x * 0.12 + ping + wash, 2500) * vel


def brush_swish(dur=0.35, vel=0.5):
    n = int(dur * SR)
    x = bp(noise(n), 1800, 9000)
    e = np.sin(np.linspace(0, np.pi, n)) ** 1.5
    return x * e * vel * 0.5


def brush_tap(vel=0.6):
    n = int(0.18 * SR)
    x = bp(noise(n), 1200, 8000) * env_exp(n, 0.035)
    body = modal([190, 330], [0.05, 0.03], [0.4, 0.2], 0.18)
    return (x + body * 0.5) * vel


def soft_kick(vel=0.5):
    t = t_axis(0.35)
    f = 55 + 40 * np.exp(-t / 0.03)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.12)
    return x * vel


# --------------------------------------------------------------------------- sound effects

def sfx_paper_lift(seed):
    n = int(0.28 * SR)
    r = np.random.default_rng(seed)
    x = bp(r.standard_normal(n), 900, 7000) * env_adsr(n, 0.04, 0.08, 0.3, 0.12)
    crinkle = np.zeros(n)
    for _ in range(9):
        p = r.integers(0, n - 400)
        crinkle[p:p + 300] += hp(r.standard_normal(300), 3000) * np.exp(-np.arange(300) / 40) * r.uniform(0.3, 1)
    return reverb(x * 0.6 + crinkle * 0.35, 0.5, 0.12)


def sfx_paper_drop(seed):
    n = int(0.22 * SR)
    r = np.random.default_rng(seed)
    slap = bp(r.standard_normal(n), 300, 5000) * env_exp(n, 0.03)
    thud = modal([140, 230], [0.04, 0.03], [0.5, 0.3], 0.22)
    return reverb(slap * 0.7 + thud * 0.4, 0.4, 0.1)


def sfx_paper_touch(seed):
    n = int(0.09 * SR)
    r = np.random.default_rng(seed)
    return bp(r.standard_normal(n), 2000, 8000) * env_exp(n, 0.02) * 0.4


def sfx_paper_slide(seed):
    n = int(0.55 * SR)
    r = np.random.default_rng(seed)
    x = bp(r.standard_normal(n), 600, 5000)
    e = np.sin(np.linspace(0, np.pi, n)) ** 0.8
    grain = 1 + 0.4 * lp(r.standard_normal(n), 30)
    return reverb(x * e * grain * 0.5, 0.5, 0.1)


def sfx_paper_deal(seed):
    n = int(0.2 * SR)
    r = np.random.default_rng(seed)
    flick = hp(r.standard_normal(n), 2500) * env_exp(n, 0.025)
    slide = bp(r.standard_normal(n), 800, 4000) * env_adsr(n, 0.01, 0.05, 0.3, 0.1)
    return reverb(flick * 0.6 + slide * 0.4, 0.4, 0.12)


def sfx_pin():
    # Push-pin into cork: a dull thunk with a tiny metallic tick on top.
    n = int(0.35 * SR)
    thunk = modal([110, 175, 260], [0.07, 0.05, 0.03], [0.8, 0.5, 0.3], 0.35)
    cork = lp(noise(n), 1500) * env_exp(n, 0.02) * 0.7
    tick = modal([3200, 5100], [0.012, 0.008], [0.4, 0.25], 0.35)
    return reverb(thunk + cork + tick, 0.5, 0.12)


def sfx_conflict():
    # Low bowed-string tension swell + a dissonant pluck cluster.
    dur = 1.6
    t = t_axis(dur)
    x = np.zeros_like(t)
    for f, a in [(73.4, 1.0), (77.8, 0.7), (110, 0.4)]:
        saw = signal.sawtooth(2 * np.pi * f * t * (1 + 0.003 * np.sin(2 * np.pi * 5 * t)))
        x += a * lp(saw, 900)
    env = np.clip(t / 0.25, 0, 1) * np.exp(-np.clip(t - 0.3, 0, None) / 0.5)
    pl = mix(ks_pluck(146.8, 1.0, 0.993, 0.6, 1) * 0.5, ks_pluck(155.6, 1.0, 0.993, 0.6, 2) * 0.45)
    rattle = bp(noise(len(t)), 2000, 7000) * env_exp(len(t), 0.06) * 0.25
    return reverb(x * env * 0.45 + pad(pl, dur) + rattle, 1.8, 0.3)


def sfx_resolve():
    a = vibes(midi(74), 0.4, 0.6)
    b = at(vibes(midi(81), 0.6, 0.55), 0.11)
    return reverb(mix(a, b), 1.8, 0.3)


def sfx_stamp(big=False):
    n = int(0.6 * SR)
    thump = modal([90, 140, 210, 330], [0.09, 0.06, 0.04, 0.03], [1, 0.6, 0.4, 0.2], 0.6)
    slap = bp(noise(n), 400, 4000) * env_exp(n, 0.012) * 0.9
    ink = bp(noise(n), 2500, 9000) * env_adsr(n, 0.005, 0.04, 0.1, 0.05) * 0.2
    x = thump + slap + ink
    if big:
        x = x + at(soft_kick(0.8), 0.0, 0.6)
        return reverb(x, 2.2, 0.35)
    return reverb(x, 0.9, 0.18)


def sfx_confront():
    # A held breath: soft low piano-ish note with a rising airy swell.
    dur = 0.9
    t = t_axis(dur)
    tone = rhodes(midi(38), 0.6, 0.5)
    air = bp(noise(len(t)), 500, 3000) * (t / dur) ** 2 * 0.25
    return reverb(mix(tone * 0.7, air), 1.4, 0.25)


def sfx_firm():
    dur = 0.7
    a = rhodes(midi(41), 0.3, 0.6)
    b = at(rhodes(midi(40), 0.4, 0.55), 0.16)
    knock = modal([160, 260], [0.05, 0.04], [0.6, 0.4], 0.3)
    return reverb(mix(a, b, knock * 0.6), 1.2, 0.22)


def sfx_wrong():
    a = at(modal([180, 300], [0.06, 0.04], [0.7, 0.4], 0.3), 0)
    b = at(modal([150, 250], [0.07, 0.05], [0.7, 0.4], 0.3), 0.12)
    tone = mix(at(vibes(midi(62), 0.3, 0.3), 0.0) * 0.3, at(vibes(midi(61), 0.4, 0.3), 0.14) * 0.3)
    return reverb(mix(a, b, tone), 0.9, 0.2)


def sfx_nope():
    a = modal([420, 760], [0.03, 0.02], [0.6, 0.3], 0.15)
    b = at(modal([360, 650], [0.03, 0.02], [0.6, 0.3], 0.15), 0.1)
    return reverb(mix(a, b) * 0.6, 0.5, 0.15)


def sfx_link_clip():
    # Paper clip: bright metallic snap with a short ring.
    n = int(0.4 * SR)
    snap = hp(noise(n), 3000) * env_exp(n, 0.006)
    ring = modal([2650, 4420, 6110], [0.09, 0.06, 0.04], [0.5, 0.35, 0.2], 0.4, 0.01)
    chime = at(vibes(midi(79), 0.3, 0.35), 0.06)
    return reverb(mix(snap * 0.8 + ring, chime), 1.0, 0.2)


def tick_sound(pitch=1.0, vel=1.0):
    n = int(0.08 * SR)
    x = modal([2400 * pitch, 3900 * pitch, 1300 * pitch], [0.01, 0.007, 0.015], [0.6, 0.4, 0.3], 0.08)
    x += hp(noise(n), 4000) * env_exp(n, 0.003) * 0.4
    return x * vel


def sfx_clock_ratchet():
    # Clockwork ratchet: ticks accelerating then settling, as cards slide to their true time.
    times = []
    t = 0.0
    gap = 0.11
    while t < 1.05:
        times.append(t)
        t += gap
        gap = max(0.045, gap * 0.9) if t < 0.6 else gap * 1.18
    out = np.zeros(int(1.4 * SR))
    for i, tt in enumerate(times):
        out += at(tick_sound(1.0 + 0.04 * (i % 2), 0.8), tt, 1.4)
    clunk = at(modal([300, 520], [0.06, 0.04], [0.6, 0.3], 0.3), 1.15, 1.4)
    return reverb(out + clunk, 0.8, 0.15)


def sfx_lock_break():
    # Porcelain/glass crack + a low minor piano chord.
    n = int(2.2 * SR)
    crack = hp(noise(n), 2500) * env_exp(n, 0.02)
    shards = np.zeros(n)
    for i in range(14):
        f = rng.uniform(2500, 7500)
        shards += at(modal([f, f * 1.52], [0.05, 0.03], [0.3, 0.2], 0.2), 0.02 + i * rng.uniform(0.01, 0.04), 2.2)
    chord = mix(*[at(rhodes(midi(m), 1.4, 0.55), 0.03) for m in (38, 45, 50, 53)])
    return reverb(pad(crack * 0.6, 2.2) + shards * 0.6 + pad(chord, 2.2) * 0.7, 2.4, 0.32)


def sfx_type(seed):
    n = int(0.09 * SR)
    r = np.random.default_rng(seed)
    clack = modal([r.uniform(1800, 2600), r.uniform(3200, 4200), r.uniform(700, 1000)], [0.012, 0.008, 0.02], [0.7, 0.4, 0.5], 0.09)
    slap = bp(r.standard_normal(n), 1000, 6000) * env_exp(n, 0.006) * 0.8
    return reverb(clack + slap, 0.3, 0.08)


def sfx_type_bell():
    return reverb(modal([2093, 2093 * 2.76, 2093 * 5.4], [0.5, 0.2, 0.08], [0.6, 0.25, 0.1], 1.0), 0.8, 0.2)


def sfx_pencil():
    n = int(0.35 * SR)
    x = bp(noise(n), 1500, 7000)
    scratch = 1 + 0.8 * np.sin(2 * np.pi * 22 * np.arange(n) / SR) ** 2
    e = env_adsr(n, 0.02, 0.1, 0.6, 0.12)
    return x * scratch * e * 0.5


def sfx_footsteps():
    out = np.zeros(int(1.2 * SR))
    for i, tt in enumerate([0.0, 0.42, 0.84]):
        n = int(0.15 * SR)
        step = lp(noise(n), 1200) * env_exp(n, 0.03) + modal([110, 180], [0.04, 0.03], [0.4, 0.2], 0.15)
        grit = bp(noise(n), 2000, 6000) * env_exp(n, 0.015) * 0.3
        out += at((step + grit) * (0.9 if i % 2 else 1.0), tt, 1.2)
    return reverb(out, 1.0, 0.2)


def sfx_folder():
    n = int(0.4 * SR)
    flap = bp(noise(n), 300, 3000) * env_adsr(n, 0.01, 0.06, 0.2, 0.15)
    pat = modal([120, 200], [0.06, 0.04], [0.5, 0.3], 0.4)
    return reverb(flap * 0.6 + pat * 0.6, 0.6, 0.15)


def sfx_lamp_click():
    a = modal([1800, 3100, 900], [0.015, 0.01, 0.03], [0.6, 0.3, 0.4], 0.2)
    hum = at(np.sin(2 * np.pi * 100 * t_axis(0.5)) * env_exp(int(0.5 * SR), 0.15) * 0.06, 0.02)
    return reverb(mix(a, hum), 0.6, 0.15)


def sfx_badge_lost():
    a = vibes(midi(67), 0.3, 0.5)
    b = at(vibes(midi(63), 0.6, 0.45), 0.12)
    return reverb(mix(a, b), 1.2, 0.25)


def sfx_ui_hover():
    return modal([2600, 4100], [0.01, 0.006], [0.4, 0.2], 0.05) * 0.5


def sfx_ui_click():
    n = int(0.1 * SR)
    return reverb(modal([900, 1500, 2600], [0.02, 0.015, 0.01], [0.6, 0.4, 0.2], 0.1) + hp(noise(n), 3000) * env_exp(n, 0.004) * 0.3, 0.3, 0.1)


def make_sfx():
    for i in range(1, 4):
        write(f"paper_lift_{i}", sfx_paper_lift(10 + i))
        write(f"paper_drop_{i}", sfx_paper_drop(20 + i))
        write(f"paper_touch_{i}", sfx_paper_touch(30 + i), 0.5)
        write(f"paper_deal_{i}", sfx_paper_deal(40 + i))
    write("paper_slide", sfx_paper_slide(5))
    write("pin", sfx_pin())
    write("conflict", sfx_conflict(), 0.8)
    write("resolve", sfx_resolve(), 0.7)
    write("stamp", sfx_stamp(False))
    write("stamp_big", sfx_stamp(True))
    write("confront", sfx_confront(), 0.75)
    write("firm", sfx_firm(), 0.8)
    write("wrong", sfx_wrong(), 0.8)
    write("nope", sfx_nope(), 0.6)
    write("link_clip", sfx_link_clip())
    write("clock_ratchet", sfx_clock_ratchet(), 0.8)
    write("tick", reverb(tick_sound(), 0.5, 0.15), 0.6)
    write("lock_break", sfx_lock_break())
    for i in range(1, 5):
        write(f"type_{i}", sfx_type(50 + i), 0.7)
    write("type_bell", sfx_type_bell(), 0.6)
    write("pencil", sfx_pencil(), 0.7)
    write("footsteps", sfx_footsteps(), 0.7)
    write("folder", sfx_folder())
    write("lamp_click", sfx_lamp_click())
    write("badge_lost", sfx_badge_lost(), 0.7)
    write("ui_hover", sfx_ui_hover(), 0.4)
    write("ui_click", sfx_ui_click(), 0.6)


# --------------------------------------------------------------------------- ambience

def make_ambience():
    dur = 24.0
    n = int(dur * SR)
    # Rain on a window: pink bed + random droplet ticks, gently modulated; seamless loop.
    bed = lp(hp(noise(n, "pink"), 400), 6000) * 0.25
    swell = 1 + 0.25 * np.sin(2 * np.pi * np.arange(n) / n * 3) + 0.1 * np.sin(2 * np.pi * np.arange(n) / n * 7)
    drops = np.zeros(n)
    for _ in range(2600):
        p = rng.integers(0, n - 2000)
        f = rng.uniform(2500, 9000)
        L = 600
        drops[p:p + L] += np.sin(2 * np.pi * f * np.arange(L) / SR) * np.exp(-np.arange(L) / rng.uniform(25, 90)) * rng.uniform(0.05, 0.4)
    gutter = hp(lp(noise(n, "brown"), 300), 30, 2) * 0.25   # brown noise drifts: keep it off DC
    rain = hp(bed * swell + hp(drops, 2000) * 0.35 + gutter, 20, 2)
    st = np.stack([rain, np.roll(rain, 911)], -1)
    xf = int(2.0 * SR)
    st[:xf] = st[:xf] * np.linspace(0, 1, xf)[:, None] + st[-xf:] * np.linspace(1, 0, xf)[:, None]
    st = st[:-xf]
    write("amb_rain", st, 0.6)

    # Desk clock: tick-tock, slightly different pitches, 4 s loop.
    period = int(4.0 * SR)
    out = np.zeros(period)
    for i in range(8):
        out += at(tick_sound(1.0 if i % 2 == 0 else 0.86, 0.7), i * 0.5, 4.0)
    wet = reverb(out, 0.6, 0.15)
    loop = wet[:period].copy()
    tail = wet[period:]
    loop[:len(tail)] += tail[:period]
    write("amb_clock", loop, 0.5)


# --------------------------------------------------------------------------- music

class Score:
    """Renders note events (seconds) into a stereo buffer with per-instrument pan and gain."""

    def __init__(self, dur):
        self.n = int(dur * SR)
        self.buf = np.zeros((self.n + int(4 * SR), 2))

    def add(self, x, start, gain=1.0, pan=0.0):
        s = int(start * SR)
        if s >= len(self.buf):
            return
        x = x[: len(self.buf) - s]
        l = math.cos((pan + 1) * math.pi / 4)
        r = math.sin((pan + 1) * math.pi / 4)
        self.buf[s:s + len(x), 0] += x * gain * l
        self.buf[s:s + len(x), 1] += x * gain * r

    def loop_render(self, reverb_sec=2.2, wet=0.22, tail_into_start=True):
        """Mix the tail back over the start so the loop is seamless."""
        out = reverb(self.buf, reverb_sec, wet)
        main = out[: self.n].copy()
        tail = out[self.n:]
        if tail_into_start:
            L = min(len(tail), self.n)
            main[:L] += tail[:L]
        # Gentle tape colour: soft saturation + rolled-off highs.
        main = np.tanh(main * 0.9) / 0.9
        # Run the filter over [end of loop, loop] and keep the second part, so its state at the
        # loop point is what it would be mid-song (a cold start clicks at the seam).
        pre = int(0.5 * SR)
        wrapped = np.concatenate([main[-pre:], main])
        main = np.stack([lp(wrapped[:, c], 11000, 1)[pre:] for c in range(2)], -1)
        return main


CHORDS = {
    # name: (bass root midi, voicing midi notes)
    "Dm9": (38, [53, 57, 60, 64]),
    "Gm9": (43, [53, 58, 62, 65]),
    "Em7b5": (40, [50, 55, 58, 62]),
    "A7b9": (45, [55, 58, 61, 64]),
    "Bbmaj7": (46, [53, 57, 62, 65]),
    "A7alt": (45, [55, 60, 61, 65]),
    "Dm6": (38, [53, 57, 59, 62]),
    "Cm9": (36, [51, 55, 58, 62]),
    "F7": (41, [51, 55, 57, 60]),
    "Ebmaj7": (39, [50, 55, 58, 62]),
}

SCALE_D_DORIAN = [50, 52, 53, 55, 57, 59, 60, 62, 64, 65, 67, 69, 71, 72, 74]


def walking_line(prog, beats_per_chord=4):
    """Walking quarter notes: root, chord tone, passing tone, chromatic approach to the next root."""
    notes = []
    for i, ch in enumerate(prog):
        root = CHORDS[ch][0]
        nxt = CHORDS[prog[(i + 1) % len(prog)]][0]
        third = root + (3 if "m" in ch and "maj" not in ch else 4)
        fifth = root + (6 if "b5" in ch else 7)
        approach = nxt + (1 if rng.random() < 0.5 else -1)
        pattern = [root, third if rng.random() < 0.6 else fifth, fifth if rng.random() < 0.6 else root + 10, approach]
        notes.extend(pattern[:beats_per_chord])
    return notes


def jazz_track(name, prog, bpm, bars_per_chord=1, melody=None, density=1.0, seed=0, ride_on=True, brushes=True,
               rhodes_gain=0.5, vibes_gain=0.55, bass_gain=0.9, reverb_sec=2.4, wet=0.24, target_lufs=-13.5):
    global rng
    rng = np.random.default_rng(seed)
    beat = 60.0 / bpm
    swing = beat * 0.66  # swung 8th offset
    chords = []
    for ch in prog:
        chords += [ch] * bars_per_chord
    total_beats = len(chords) * 4
    dur = total_beats * beat
    sc = Score(dur)

    # Bass.
    line = walking_line(chords, 4)
    for i, m in enumerate(line):
        vel = 0.85 + 0.15 * (i % 4 == 0)
        sc.add(upright_bass(midi(m), beat * 0.92, vel), i * beat + rng.uniform(0, 0.008), bass_gain, -0.1)

    # Drums: ride (ding, ding-da ding), brush swirl, feathered kick.
    for b in range(total_beats):
        t0 = b * beat
        if ride_on and density > 0.4:
            sc.add(ride(1.0, 0.33 + 0.05 * (b % 2 == 0), seed=b), t0 + rng.uniform(0, 0.006), 0.32, 0.35)
            if b % 2 == 1 and rng.random() < 0.85 * density:
                sc.add(ride(0.6, 0.22, seed=1000 + b), t0 + swing, 0.25, 0.35)
        if brushes:
            if b % 2 == 1:
                sc.add(brush_tap(0.5), t0 + rng.uniform(0, 0.01), 0.35, -0.2)
            sc.add(brush_swish(beat * 0.9, 0.45), t0, 0.22, -0.3 + 0.6 * (b % 2))
        if b % 4 == 0:
            sc.add(soft_kick(0.5), t0, 0.35)

    # Rhodes comping: Charleston-ish rhythm, varied.
    for bar, ch in enumerate(chords):
        t0 = bar * 4 * beat
        voicing = CHORDS[ch][1]
        hits = [(0.0, 1.4), (1.0 + 0.66, 0.6)] if rng.random() < 0.6 else [(0.66, 0.9), (2.66, 0.9)]
        if density < 0.6:
            hits = [(0.0, 3.6)]
        for off, length in hits:
            for k, m in enumerate(voicing):
                sc.add(rhodes(midi(m), length * beat, 0.55 + rng.uniform(-0.08, 0.05)), t0 + off * beat + k * 0.006, rhodes_gain * 0.32, 0.25 - k * 0.12)

    # Melody on vibes.
    if melody:
        for (start_beat, length_beats, m) in melody:
            if start_beat >= total_beats:
                continue
            sc.add(vibes(midi(m), length_beats * beat, 0.7), start_beat * beat, vibes_gain * 0.55, 0.15)

    out = sc.loop_render(reverb_sec, wet)
    write(name, out, 0.85, target_lufs)


def generate_melody(prog, bars_per_chord, seed, sparse=0.5, register=(62, 79)):
    """Sparse, singing phrases from D dorian, landing on chord tones on strong beats."""
    r = np.random.default_rng(seed)
    beats = len(prog) * bars_per_chord * 4
    mel = []
    b = 2.0
    last = 69
    scale = [m for m in SCALE_D_DORIAN + [m + 12 for m in SCALE_D_DORIAN] if register[0] <= m <= register[1]]
    while b < beats - 4:
        phrase_len = r.integers(3, 7)
        for _ in range(phrase_len):
            chord = prog[int(b // (4 * bars_per_chord)) % len(prog)]
            tones = [m for m in CHORDS[chord][1] + [x + 12 for x in CHORDS[chord][1]] if register[0] <= m <= register[1]]
            strong = abs(b - round(b)) < 0.01 and int(round(b)) % 2 == 0
            pool = tones if strong and tones else scale
            cands = sorted(pool, key=lambda m: abs(m - last) + r.uniform(0, 3))
            m = cands[0] if r.random() < 0.7 else cands[min(len(cands) - 1, 2)]
            length = r.choice([0.66, 1.0, 1.34, 2.0], p=[0.25, 0.35, 0.2, 0.2])
            mel.append((b, length * 0.95, m))
            last = m
            b += length
        b += r.uniform(2.0, 5.0) / max(0.2, sparse)
    return mel


def make_music():
    # Title: "Wrenhaven After Dark", a slow minor blues-ish form with a vibes melody.
    prog_title = ["Dm9", "Dm9", "Gm9", "Gm9", "Em7b5", "A7b9", "Dm9", "A7alt", "Bbmaj7", "Gm9", "Em7b5", "A7b9"] * 2
    jazz_track("music_title", prog_title, 84, 1, generate_melody(prog_title, 1, 7, 0.7), seed=3,
               rhodes_gain=0.55, vibes_gain=0.7)
    # Board: sparse, unobtrusive, loopable for long thinking.
    prog_board = ["Dm9", "Dm6", "Gm9", "Gm9", "Em7b5", "A7b9", "Dm9", "Dm9", "Cm9", "F7", "Bbmaj7", "Ebmaj7", "Em7b5", "A7b9", "Dm9", "A7alt"] * 2
    jazz_track("music_board", prog_board, 72, 1, generate_melody(prog_board, 1, 11, 0.32, (64, 76)), density=0.75, seed=5,
               rhodes_gain=0.42, vibes_gain=0.42, bass_gain=0.8, target_lufs=-15.0)   # a bed to think over
    # Reveal: slower, darker, drums out, heartbeat kick, then resolves.
    prog_reveal = ["Em7b5", "A7alt", "Dm9", "Dm9", "Gm9", "A7b9", "Dm9", "Dm9"]
    jazz_track("music_reveal", prog_reveal, 60, 1, generate_melody(prog_reveal, 1, 21, 0.5, (57, 72)), density=0.35, seed=9,
               ride_on=False, rhodes_gain=0.6, vibes_gain=0.5, reverb_sec=3.0, wet=0.32)
    # Case closed: warm major-leaning turnaround that rings out.
    prog_closed = ["Bbmaj7", "Gm9", "Em7b5", "A7b9", "Dm9", "Gm9", "A7alt", "Dm9"]
    jazz_track("music_closed", prog_closed, 92, 1, generate_melody(prog_closed, 1, 31, 0.9, (65, 81)), density=1.0, seed=13,
               rhodes_gain=0.6, vibes_gain=0.75)


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("all", "sfx"):
        make_sfx()
        print("sfx done")
    if what in ("all", "music"):
        make_ambience()
        print("ambience done")
        make_music()
        print("music done")
