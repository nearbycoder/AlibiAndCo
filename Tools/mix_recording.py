#!/usr/bin/env python3
"""Rebuild a recording's soundtrack from the per-frame voice log written by VideoRecorder.

    mix_recording.py <recording dir>      reads audio.log, writes audio.wav next to it

Each log line is "frame source clip volume pitch loop started". Recording runs slower than real
time, so playback positions are reconstructed here: a voice starts at the frame it was triggered
and advances by pitch * 1/fps per frame. One-shots play to their end (or until the voice is
reused); loops play while they're logged, with their volume ramped frame to frame.
"""
import os
import sys
import wave
from collections import defaultdict

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 44100

_clips = {}


def clip(name):
    if name not in _clips:
        with wave.open(os.path.join(AUDIO, name + ".wav")) as w:
            assert w.getframerate() == SR and w.getsampwidth() == 2, name
            data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
            data = data.reshape(-1, w.getnchannels())
            if data.shape[1] == 1:
                data = np.repeat(data, 2, axis=1)
        _clips[name] = data
    return _clips[name]


class Voice:
    def __init__(self, name, vol, pitch, loop):
        self.data = clip(name)
        self.pos = 0.0
        self.vol = self.prev = vol
        self.pitch = pitch
        self.loop = loop
        self.alive = True

    def render(self, n):
        length = len(self.data)
        idx = self.pos + self.pitch * np.arange(n)
        self.pos += self.pitch * n
        if self.loop:
            idx = np.mod(idx, length)
        else:
            keep = idx < length - 1
            if not keep.any():
                self.alive = False
                return None
            idx = np.where(keep, idx, 0)
        i0 = idx.astype(np.int64)
        f = (idx - i0)[:, None]
        i1 = np.minimum(i0 + 1, length - 1)
        out = self.data[i0] * (1 - f) + self.data[i1] * f
        if not self.loop:
            out[~keep] = 0
            if self.pos >= length:
                self.alive = False
        gain = np.linspace(self.prev, self.vol, n, endpoint=False)[:, None]
        self.prev = self.vol
        return out * gain


def main(rec_dir):
    fps, master = 30, 1.0
    frames = defaultdict(list)
    last = 0
    with open(os.path.join(rec_dir, "audio.log")) as f:
        for line in f:
            if line.startswith("#"):
                parts = line.split()
                fps = int(parts[parts.index("fps") + 1])
                master = float(parts[parts.index("master") + 1])
                continue
            fr, src, name, vol, pitch, loop, start = line.split()
            fr = int(fr)
            frames[fr].append((int(src), name, float(vol), float(pitch), loop == "1", start == "1"))
            last = max(last, fr)
    spf = SR / fps
    total = int(round(last * spf)) + SR
    mix = np.zeros((total, 2), np.float32)
    voices = {}
    for fr in range(1, last + 1):
        seen = set()
        for src, name, vol, pitch, loop, start in frames.get(fr, ()):
            seen.add(src)
            v = voices.get(src)
            if start or v is None or not v.alive:
                voices[src] = Voice(name, vol, pitch, loop)
            else:
                v.vol, v.pitch = vol, pitch
        for src in [s for s, v in voices.items() if v.loop and s not in seen]:
            del voices[src]
        a, b = int(round((fr - 1) * spf)), int(round(fr * spf))
        for src in list(voices):
            v = voices[src]
            out = v.render(b - a)
            if out is not None:
                mix[a:b] += out
            if not v.alive:
                del voices[src]
    mix *= master
    # Soft knee above -2 dBFS instead of hard clipping.
    knee = 0.8
    over = np.abs(mix) > knee
    mix[over] = np.sign(mix[over]) * (knee + (1 - knee) * np.tanh((np.abs(mix[over]) - knee) / (1 - knee)))
    out_path = os.path.join(rec_dir, "audio.wav")
    with wave.open(out_path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(mix, -1, 1) * 32767).astype(np.int16).tobytes())
    peak = float(np.abs(mix).max())
    print(f"[mix] {last} frames ({last / fps:.1f}s), peak {peak:.2f} -> {out_path}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "/tmp/alibi-record")
