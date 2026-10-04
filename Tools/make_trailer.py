#!/usr/bin/env python3
"""Cut the feature trailer, its poster frame and the README teaser loop from a trailer capture.

    Tools/capture_trailer.sh                       # the game plays itself and logs a marker per beat
    .venv/bin/python Tools/make_trailer.py [capture dir] [--out docs/media] [--only trailer|poster|teaser|stills]

The capture (Captures/trailer by default) holds video.mp4 (1920x1080, 30 fps, scripted mouse and
keyboard input), markers.txt ("frame label" per beat), audio_sfx.wav (effects and ambience rebuilt
from the game's voice log) and cursor-free stills. This script:

  * resolves the edit decision list below against the markers, so every shot is a named beat;
  * renders the title card, end card and every caption with Pillow in the game's own typefaces
    (Abril Fatface, Playfair Display, IBM Plex Sans Condensed) and palette;
  * assembles picture with one ffmpeg filter graph (captions slide and fade in, segments dissolve);
  * builds the soundtrack in numpy: the effects stem follows the picture, the music bed is laid out
    from the game's own cues (title theme, investigation loop, reveal, case-closed sting) with fades,
    and is ducked under the effects with an envelope follower; then it's loudness-normalised
    (EBU R128, -14 LUFS, -1.5 dBTP) by ffmpeg's two-pass loudnorm;
  * writes alibi-and-co-trailer.mp4 (H.264 + AAC), trailer-poster.jpg (with a play button),
    teaser.webp (a 7 s seamless loop) and the README screenshots (screenshot-*.jpg).
Everything is deterministic given the same capture.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "Assets", "Fonts")
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
W, H, FPS, SR = 1920, 1080, 30, 44100
XF = 0.4            # default dissolve between beats, seconds
URL = "github.com/nearbycoder/AlibiAndCo"

# The game's palette (Assets/Scripts/Game/Art.cs).
CREAM, CREAM_DIM, LAMP = (241, 230, 207), (201, 191, 168), (242, 182, 90)
OXBLOOD, INK, PANEL = (142, 43, 43), (31, 42, 58), (20, 22, 27)


def font(name, size, variation=None):
    f = ImageFont.truetype(os.path.join(FONTS, name), size)
    if variation:
        f.set_variation_by_name(variation)
    return f


def display(size): return font("AbrilFatface-Regular.ttf", size)
def serif(size, weight="SemiBold"): return font("PlayfairDisplay.ttf", size, weight)
def serif_italic(size): return font("PlayfairDisplay-Italic.ttf", size)
def sans(size): return font("IBMPlexSansCondensed-SemiBold.ttf", size)


def run(cmd):
    p = subprocess.run(cmd, capture_output=True, text=True)
    if p.returncode != 0:
        sys.stderr.write(p.stderr[-4000:])
        raise SystemExit(f"command failed: {' '.join(cmd[:6])} …")
    return p


# ---------------------------------------------------------------------------------------- markers

class Markers:
    def __init__(self, path):
        self.items = []
        with open(path) as f:
            for line in f:
                fr, label = line.rstrip("\n").split(" ", 1)
                self.items.append((int(fr) / FPS, label))

    def has(self, label):
        return any(lab.startswith(label) for _, lab in self.items)

    def t(self, label, after=None, nth=0):
        """Seconds of the nth marker starting with label (after the first marker starting with `after`)."""
        lo = self.t(after) if after else -1
        hits = [t for t, lab in self.items if lab.startswith(label) and t > lo]
        if len(hits) <= nth:
            raise KeyError(f"marker '{label}'" + (f" after '{after}'" if after else "") + " not found")
        return hits[nth]


# ---------------------------------------------------------------------------------------- the edit

def edit(m):
    """The trailer, beat by beat. Each shot: source window, caption, and how it dissolves in."""
    c1, c2, c3 = "case case1", "case case2", "case case3"

    def shot(start, dur, kicker=None, title=None, sub=None, xf=XF, pos="bottom", cap_in=0.35, cap_out=None,
             crop=None, cap_static=False):
        return dict(kind="game", start=start, dur=dur, kicker=kicker, title=title, sub=sub, xf=xf, pos=pos,
                    cap_in=cap_in, cap_out=cap_out, crop=crop, cap_static=cap_static)

    S = []
    # Cold open: the designer's trailer moment. One receipt moves five minutes, an alibi collapses.
    t_link = m.t("link r_", after=c2)
    S.append(shot(t_link + 0.5, 7.2, None, "Move one receipt five minutes earlier…", None, xf=0, pos="top", cap_in=0.6))
    t_dot = m.t("confront m_dot", after=c2)
    t_open = m.t("open marlow", after=c2)
    S.append(shot(t_dot + 0.9, (t_open + 0.5) - (t_dot + 0.9), None, "…and a perfect alibi falls apart.", None,
                  xf=0.3, pos="top", cap_in=0.4, cap_out=99))
    # Punch in on Marlow's line as the MISTAKEN stamp lands and his lock breaks open, one minute to spare.
    S.append(shot(t_open + 0.38, 2.5, None, "…and a perfect alibi falls apart.", None, xf=0.12, pos="top",
                  crop=(823, 48, 1097, 617), cap_static=True))
    S.append(dict(kind="title", dur=6.0, xf=0.6))

    # One beat per feature.
    t_intro = m.t("intro case1")
    S.append(shot(t_intro - 1.6, 6.2, "WRENHAVEN · AUTUMN 1986",
                  "Three crimes. One cork board.", "You're the “& Co.” at a retired inspector's two-desk agency."))
    t_drag = m.t("drag ", after=c1)
    S.append(shot(t_drag - 0.2, 4.6, "PIN THE EVIDENCE", "Drag each card onto its person's line.", None, pos="top"))
    t_conf = m.t("conflict 1", after=c1)
    t_rib = max(t_conf - 1.6, t_drag - 0.2 + 4.6 - XF)
    S.append(shot(t_rib, max(5.2, t_conf + 3.6 - t_rib), "WALKING-TIME RIBBONS", "The board does the arithmetic.",
                  "Blue: there was time to walk it. Red: the story is impossible."))
    t_hov = m.t("hover ", after=c1)
    S.append(shot(t_hov + 0.1, 3.9, "INSPECT ANYTHING", "Hover a card to read it and trace the walk.",
                  "Every route is measured on the town map."))
    t_cf = m.t("confront a_claim", after=c1)
    S.append(shot(t_cf + 1.4, (m.t("struck a_claim") + 2.4) - (t_cf + 1.4), "CONFRONT",
                  "Confront a statement in the red.", "Paper never lies. People do, though a lie isn't guilt."))
    t_firm = m.t("confront i_sid", after=c2)
    S.append(shot(t_firm + 1.4, (m.t("badge-lost", after=c2) + 2.6) - (t_firm + 1.4), "STANDS FIRM",
                  "Confront the truth and it stands firm.", "Wrong calls cost a badge."))
    t_l1 = m.t("link t_", after=c2)
    S.append(shot(t_l1 + 0.9, (m.t("clock ", after=c2) + 3.6) - (t_l1 + 0.9), "LINK THE CLOCKS",
                  "Same moment, two clocks? Link them.", "Catch a clock that runs slow, and every card it timed slides."))
    t_map = m.t("map", after=c2)
    S.append(shot(t_map + 0.2, 3.6, "THE TOWN", "Wrenhaven, street by street.",
                  "Walking times come from the map itself."))
    # Case 3's twist stays off screen: the unknown photo is shown while two faces remain, and the
    # camera beat ends before that photo flies to its owner's line (about 0.4 s after the link lands).
    t_unk = m.t("unknown u_photo", after=c3) if m.has("unknown u_photo") else m.t("struck c_peng") + 1.2
    S.append(shot(t_unk + 0.3, 3.9, "UNKNOWN FACES", "Who's the figure in the photograph?",
                  "Every suspect the records rule out is crossed off, until one face fits.", pos="top"))
    t_cam = m.t("link t_", after=c3)
    S.append(shot(t_cam + 0.6, (m.t("clock camera", after=c3) + 0.45) - (t_cam + 0.6), "CAMERAS LIE TOO",
                  "A press camera's date-back runs fast.", "Link it to the power cut, and every photograph moves."))
    t_wl = m.t("wrong-link", after=c3)
    S.append(shot(t_wl + 0.4, 4.6, "NOT THE SAME MOMENT", "Guess wrong and Connie notices.",
                  "Three badges a case. Mistakes cost one."))
    t_hint = m.t("hint", after=c3)
    S.append(shot(t_hint - 0.1, 3.6, "ASK CONNIE", "Stuck? Ask for a hint.",
                  "Typed memos point you at the next step, never the answer."))
    t_nb = m.t("notebook", after=c2)
    S.append(shot(t_nb + 0.1, 3.9, "THE NOTEBOOK", "Every memo, question and clock, kept.",
                  "Press Tab to see where the case stands."))
    t_acc = m.t("accuse case1")
    S.append(shot(t_acc + 0.2, (m.t("pin-incident case1") + 1.6) - (t_acc + 0.2), "THE ACCUSATION",
                  "Drag the incident across the suspects.", "It only fits the one alibi with a hole in it."))
    t_rec = m.t("flow Closing", after=c1)
    S.append(shot(t_rec + 0.4, 4.6, "THE RECONSTRUCTION", "Pin it, and the night replays.",
                  "A pawn walks the culprit's route across town.", pos="top"))
    t_cl = m.t("flow Closed", after=c1)
    S.append(shot(t_cl + 0.2, 4.4, "CASE CLOSED", "Rated, timed, and on the front page.", None))
    t_set = m.t("settings", after=c3)
    S.append(shot(t_set + 0.4, 3.3, "YOUR DESK, YOUR WAY", "Text size, reduced motion, resolution, volume.", None))
    t_fin = m.t("case-files-final")
    S.append(shot(t_fin + 0.6, 3.6, "THREE HANDCRAFTED CASES", "Proven airtight by a solver.",
                  "Exactly one answer each. No guesswork, no wrong accusation."))

    # Escalation montage: quick dissolves, no captions, the reveal cue underneath.
    mont = [
        (m.t("struck c_claim", after=c1) + 0.2, 1.3),
        (m.t("clock ", after=c2) + 0.5, 1.5),
        (m.t("struck m_claim", after=c2) + 0.2, 1.2),
        (m.t("struck c_peng", after=c3) + 0.2, 1.3),
        (m.t("struck n_claim", after=c3) + 0.2, 1.4),
        (m.t("pin-incident case2") + 0.1, 1.5),
        (m.t("open marlow", after=c2) - 0.1, 1.4),
        (m.t("flow Closed", after=c2) + 1.0, 1.9),
    ]
    for i, (s, d) in enumerate(mont):
        S.append(dict(shot(s, d, xf=0.5 if i == 0 else 0.12), montage=True))
    S.append(dict(kind="end", dur=8.5, xf=0.8))
    return S


# ---------------------------------------------------------------------------------------- drawing

def tracked(draw, xy, text, fnt, fill, tracking):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=fnt, fill=fill)
        x += draw.textlength(ch, font=fnt) + tracking
    return x


def tracked_width(draw, text, fnt, tracking):
    return sum(draw.textlength(ch, font=fnt) for ch in text) + tracking * (len(text) - 1)


def shadowed(img, layer, radius=18, offset=(0, 10), alpha=0.55):
    """Composite layer onto img with a soft drop shadow made from layer's alpha (blurred at 1/4 size)."""
    a = layer.split()[3]
    small = a.resize((a.width // 4, a.height // 4), Image.BILINEAR).filter(ImageFilter.GaussianBlur(radius / 4))
    a = small.resize(layer.size, Image.BILINEAR).point(lambda v: int(v * alpha))
    sh = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    sh.putalpha(a)
    img.alpha_composite(sh, offset)
    img.alpha_composite(layer)


def caption_png(path, kicker, title, sub, pos):
    """A dark plate with an oxblood rule: amber kicker, cream serif headline, italic subline."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fk, ft, fs = sans(25), serif(54), serif_italic(31)
    pad_x, pad_y, gap = 46, (22 if pos == "top" else 30), 10
    wk = tracked_width(d, kicker, fk, 5) if kicker else 0
    wt = d.textlength(title, font=ft)
    ws = d.textlength(sub, font=fs) if sub else 0
    inner = max(wk, wt, ws)
    bw = int(inner + pad_x * 2 + 10)
    hk = 30 if kicker else 0
    ht = 66
    hs = 44 if sub else 0
    bh = pad_y * 2 + hk + (gap if kicker else 0) + ht + (gap if sub else 0) + hs
    x0 = (W - bw) // 2
    y0 = 34 if pos == "top" else H - 64 - bh
    plate = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    pd = ImageDraw.Draw(plate)
    pd.rounded_rectangle((x0, y0, x0 + bw, y0 + bh), 12, fill=PANEL + (232,))
    pd.rounded_rectangle((x0, y0, x0 + 10, y0 + bh), 4, fill=OXBLOOD + (255,))
    pd.line((x0 + 22, y0 + 1, x0 + bw - 14, y0 + 1), fill=LAMP + (70,), width=1)
    shadowed(img, plate, 22, (0, 12), 0.6)
    d = ImageDraw.Draw(img)
    cx = x0 + 10 + (bw - 10) / 2
    y = y0 + pad_y
    if kicker:
        tracked(d, (cx - wk / 2, y), kicker, fk, LAMP, 5)
        y += hk + gap
    d.text((cx - wt / 2, y - 6), title, font=ft, fill=CREAM)
    y += ht + (gap if sub else 0)
    if sub:
        d.text((cx - ws / 2, y - 4), sub, font=fs, fill=CREAM_DIM)
    img.save(path)


def ease_out_cubic(k): return 1 - (1 - min(max(k, 0), 1)) ** 3
def ease_out_back(k, s=1.9):
    k = min(max(k, 0), 1) - 1
    return 1 + (s + 1) * k ** 3 + s * k ** 2


def backdrop(still, crop, centred=False):
    """A still cropped and blurred into a moody background, darkened towards the left (or all over)."""
    im = Image.open(still).convert("RGB").crop(crop).resize((int(W * 1.12), int(H * 1.12)), Image.LANCZOS)
    if centred:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    im = im.filter(ImageFilter.GaussianBlur(5.5 if centred else 3.2))
    arr = np.asarray(im).astype(np.float32) / 255
    h, w = arr.shape[:2]
    xs = np.linspace(0, 1, w)[None, :, None]
    ys = np.linspace(-1, 1, h)[:, None, None]
    if centred:
        shade = 0.30 + 0.0 * xs
    else:
        shade = 0.16 + 0.62 * np.clip((xs - 0.08) / 0.85, 0, 1) ** 1.3
    vign = 1 - 0.35 * ys ** 2
    arr = arr * shade * vign
    arr = arr * np.array([1.0, 0.95, 0.88])           # warm it towards the lamp
    return Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8)).convert("RGBA")


def card_frames(out_dir, kind, dur, stills):
    """Render the animated title or end card as a PNG sequence."""
    n = int(round(dur * FPS))
    if os.path.isdir(out_dir) and len(os.listdir(out_dir)) == n:
        return                                                          # reuse frames from a kept work dir
    os.makedirs(out_dir, exist_ok=True)
    # The polaroid wall from the title screen, cropped clear of the game's own logo and menu.
    bg = backdrop(stills["title"], (880, 0, 1920, 585), centred=(kind == "end"))
    n = int(round(dur * FPS))
    f_logo, f_kick, f_tag = display(200), sans(30), serif_italic(46)
    f_url, f_small = sans(34), sans(26)
    probe = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    for i in range(n):
        t = i / FPS
        z = 1.0 + 0.05 * (t / dur)                                       # slow push-in
        bw, bh = int(W * z), int(H * z)
        frame = bg.resize((bw, bh), Image.BILINEAR).crop(((bw - W) // 2, (bh - H) // 2, (bw - W) // 2 + W, (bh - H) // 2 + H))
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        if kind == "title":
            x = 170
            a_k = ease_out_cubic((t - 0.35) / 0.6)
            kick = "A  WRENHAVEN  MYSTERY  ·  1986"
            tracked(d, (x + 4 + (1 - a_k) * -30, 330), kick, f_kick, LAMP + (int(255 * a_k),), 7)
            # The logo is stamped down: it drops in from slightly larger with a little overshoot.
            k = (t - 0.9) / 0.42
            if k > 0:
                s = 1.0 + 0.32 * (1 - ease_out_back(k))
                logo = Image.new("RGBA", (1300, 300), (0, 0, 0, 0))
                ImageDraw.Draw(logo).text((10, 0), "Alibi & Co.", font=f_logo, fill=CREAM + (255,))
                logo = logo.resize((int(1300 * s), int(300 * s)), Image.BICUBIC)
                logo.putalpha(logo.split()[3].point(lambda v: int(v * min(1, k * 2.5))))
                layer.alpha_composite(logo, (int(x - 10 - (s - 1) * 300), int(372 - (s - 1) * 150)))
            for j, line in enumerate(["Pin the evidence to the timeline.", "Then find the alibi that can't be true."]):
                aa = ease_out_cubic((t - 1.9 - j * 0.35) / 0.8)
                d.text((x + 6, 640 + j * 62 + (1 - aa) * 14), line, font=f_tag, fill=CREAM_DIM + (int(255 * aa),))
        else:
            # End card: centred logo, the pitch, and where to get it.
            k = (t - 0.3) / 0.45
            if k > 0:
                s = 1.0 + 0.28 * (1 - ease_out_back(k))
                lw = probe.textlength("Alibi & Co.", font=f_logo)
                logo = Image.new("RGBA", (int(lw) + 40, 300), (0, 0, 0, 0))
                ImageDraw.Draw(logo).text((20, 0), "Alibi & Co.", font=f_logo, fill=CREAM + (255,))
                logo = logo.resize((int(logo.width * s), int(300 * s)), Image.BICUBIC)
                logo.putalpha(logo.split()[3].point(lambda v: int(v * min(1, k * 2.5))))
                layer.alpha_composite(logo, ((W - logo.width) // 2, int(250 - (s - 1) * 150)))
            a = ease_out_cubic((t - 1.3) / 0.8)
            tag = "The board does the arithmetic. You do the doubting."
            tw = probe.textlength(tag, font=f_tag)
            d.text(((W - tw) / 2, 545 + (1 - a) * 12), tag, font=f_tag, fill=CREAM_DIM + (int(255 * a),))
            a2 = ease_out_cubic((t - 2.2) / 0.8)
            facts = "THREE HANDCRAFTED CASES  ·  LINUX  ·  FREE ON GITHUB"
            fw = tracked_width(probe, facts, f_small, 4)
            tracked(d, ((W - fw) / 2, 668), facts, f_small, LAMP + (int(255 * a2),), 4)
            a3 = ease_out_cubic((t - 2.9) / 0.8)
            uw = probe.textlength(URL, font=f_url)
            plate_w = uw + 70
            d.rounded_rectangle(((W - plate_w) / 2, 744, (W + plate_w) / 2, 812), 10,
                                fill=PANEL + (int(225 * a3),), outline=OXBLOOD + (int(255 * a3),), width=3)
            d.text(((W - uw) / 2, 755), URL, font=f_url, fill=CREAM + (int(255 * a3),))
        shadowed(frame, layer, 16, (0, 8), 0.7)
        frame.convert("RGB").save(os.path.join(out_dir, f"f{i:04d}.png"), compress_level=1)


# ---------------------------------------------------------------------------------------- audio

def read_wav(path):
    with wave.open(path) as w:
        assert w.getframerate() == SR and w.getsampwidth() == 2, path
        a = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768
        a = a.reshape(-1, w.getnchannels())
    return np.repeat(a, 2, axis=1) if a.shape[1] == 1 else a


def write_wav(path, a):
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(a, -1, 1) * 32767).astype(np.int16).tobytes())


def fade(n, kind="in"):
    k = np.linspace(0, 1, max(n, 1), endpoint=False)
    k = np.sin(k * np.pi / 2)            # equal-power
    return k if kind == "in" else k[::-1]


def place(dst, src, at, gain=1.0):
    i = int(round(at * SR))
    if i >= len(dst):
        return
    n = min(len(src), len(dst) - i)
    dst[i:i + n] += src[:n] * gain


def envelope(x, attack=0.01, release=0.35):
    """Peak follower on a mono signal, decimated for speed and expanded back."""
    hop = 256
    m = np.abs(x).reshape(-1)[: (len(x) // hop) * hop].reshape(-1, hop).max(1)
    a, r = np.exp(-hop / (SR * attack)), np.exp(-hop / (SR * release))
    out = np.empty_like(m)
    v = 0.0
    for i, s in enumerate(m):
        v = a * v + (1 - a) * s if s > v else r * v + (1 - r) * s
        out[i] = v
    env = np.repeat(out, hop)
    return np.pad(env, (0, len(x) - len(env)), mode="edge")


def soundtrack(shots, timeline, total, sfx_path, out_wav):
    sfx_src = read_wav(sfx_path)
    n = int(round(total * SR)) + SR
    sfx = np.zeros((n, 2), np.float32)
    for i, (sh, (t0, _)) in enumerate(zip(shots, timeline)):
        if sh["kind"] != "game":
            continue
        a, b = int(round(sh["start"] * SR)), int(round((sh["start"] + sh["dur"]) * SR))
        seg = sfx_src[a:b].copy()
        # Crossfade the effects exactly as long as the picture dissolves (a quick cut stays quick).
        nxt = shots[i + 1] if i + 1 < len(shots) else None
        fi = int(max(sh["xf"], 0.03) * SR)
        fo = int(max(nxt["xf"] if nxt is not None and nxt["kind"] == "game" else 0.4, 0.03) * SR)
        seg[:fi] *= fade(fi, "in")[:, None]
        seg[-fo:] *= fade(fo, "out")[:, None]
        place(sfx, seg, t0, 1.0)

    # Hand-placed hits on the cards: the lamp clicks on, the logo is stamped, the end card lands.
    tt = dict((sh["kind"], t0) for sh, (t0, _) in zip(shots, timeline) if sh["kind"] in ("title", "end"))
    place(sfx, read_wav(os.path.join(AUDIO, "lamp_click.wav")), tt["title"] + 0.15, 0.9)
    place(sfx, read_wav(os.path.join(AUDIO, "stamp.wav")), tt["title"] + 1.12, 1.0)
    place(sfx, read_wav(os.path.join(AUDIO, "stamp.wav")), tt["end"] + 0.5, 0.85)

    # Music bed from the game's cues: the investigation loop under the cold open, the title theme
    # from the logo through the feature beats, the reveal cue under the montage, the case-closed
    # sting over the end card.
    mus = np.zeros_like(sfx)
    t_title, t_end = tt["title"], tt["end"]
    t_mont = next(t0 for sh, (t0, _) in zip(shots, timeline) if sh.get("montage"))

    def cue(name, start, stop, gain, fin=1.0, fout=1.0, offset=0.0):
        c = read_wav(os.path.join(AUDIO, name + ".wav"))
        length = int(round((stop - start) * SR))
        reps = int(np.ceil((length + offset * SR) / len(c))) + 1
        c = np.tile(c, (reps, 1))[int(offset * SR):int(offset * SR) + length].copy()
        c[: int(fin * SR)] *= fade(int(fin * SR), "in")[:, None]
        c[-int(fout * SR):] *= fade(int(fout * SR), "out")[:, None]
        place(mus, c, start, gain)

    cue("music_board", 0.0, t_title + 0.5, 0.55, fin=1.5, fout=0.8)
    cue("music_title", t_title + 0.05, t_mont + 0.6, 0.95, fin=0.25, fout=1.4)
    cue("music_reveal", t_mont - 0.3, t_end + 1.2, 0.95, fin=0.6, fout=1.6)
    cue("music_closed", t_end + 0.35, total + 0.2, 0.95, fin=0.4, fout=2.2)

    # Duck the music under the effects: up to 7 dB when the effects stem is busy.
    env = envelope(sfx.mean(1))
    env_db = 20 * np.log10(env + 1e-6)
    duck_db = -np.clip((env_db - (-30)) * 0.55, 0, 7)
    mus *= (10 ** (duck_db / 20))[:, None]

    mix = mus + sfx * 1.15
    end = int(round(total * SR))
    mix = mix[:end]
    tail = int(1.6 * SR)
    mix[-tail:] *= fade(tail, "out")[:, None]
    write_wav(out_wav, mix)


def loudnorm(src, dst, target=-14.0, tp=-1.5):
    probe = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", src, "-af",
                            f"loudnorm=I={target}:TP={tp}:LRA=11:print_format=json", "-f", "null", "-"],
                           capture_output=True, text=True).stderr
    stats = json.loads(probe[probe.rindex("{"):probe.rindex("}") + 1])
    af = (f"loudnorm=I={target}:TP={tp}:LRA=11:measured_I={stats['input_i']}:measured_TP={stats['input_tp']}:"
          f"measured_LRA={stats['input_lra']}:measured_thresh={stats['input_thresh']}:offset={stats['target_offset']}:linear=true")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", src, "-af", af, "-ar", str(SR), dst], check=True)


# ---------------------------------------------------------------------------------------- picture

def build(cap, out_dir, work):
    m = Markers(os.path.join(cap, "markers.txt"))
    shots = edit(m)
    stills = {os.path.splitext(f)[0]: os.path.join(cap, "stills", f) for f in os.listdir(os.path.join(cap, "stills"))}

    # Timeline: each shot starts when the previous one starts dissolving away.
    timeline, t = [], 0.0
    for i, sh in enumerate(shots):
        if i > 0:
            t -= sh["xf"]
        timeline.append((t, t + sh["dur"]))
        t += sh["dur"]
    total = t
    print(f"[trailer] {len(shots)} shots, {total:.1f}s")

    args, filters = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"], []
    idx = 0
    labels = []
    for i, sh in enumerate(shots):
        if sh["kind"] == "game":
            args += ["-ss", f"{sh['start']:.3f}", "-t", f"{sh['dur']:.3f}", "-i", os.path.join(cap, "video.mp4")]
            vin = idx
            idx += 1
            chain = f"[{vin}:v]setpts=PTS-STARTPTS,fps={FPS}"
            if sh["crop"]:
                x, y, w, h = sh["crop"]
                chain += f",crop={w}:{h}:{x}:{y},scale={W}:{H}:flags=lanczos"
            chain += ",format=yuv420p,setsar=1"
            if sh.get("title"):
                png = os.path.join(work, f"cap{i:02d}.png")
                caption_png(png, sh["kicker"], sh["title"], sh["sub"], sh["pos"])
                args += ["-loop", "1", "-framerate", str(FPS), "-t", f"{sh['dur']:.3f}", "-i", png]
                cin = idx
                idx += 1
                # A caption carried across a cut (cap_static) is simply there; otherwise it slides
                # and fades in, and fades out before the shot dissolves away (cap_out=99: it doesn't).
                t_in = -1.0 if sh["cap_static"] else sh["cap_in"]
                t_out = sh["cap_out"] if sh["cap_out"] is not None else sh["dur"] - 0.55
                fades = "" if sh["cap_static"] else f",fade=t=in:st={t_in:.2f}:d=0.45:alpha=1"
                fades += f",fade=t=out:st={t_out:.2f}:d=0.35:alpha=1" if t_out < sh["dur"] else ""
                filters.append(f"[{cin}:v]format=rgba{fades}[c{i}]")
                filters.append(f"{chain}[b{i}]")
                slide = "0" if sh["cap_static"] else f"-56*pow(clip(1-(t-{t_in:.2f})/0.55,0,1),3)"
                filters.append(f"[b{i}][c{i}]overlay=x='{slide}':y=0:shortest=1:format=auto,format=yuv420p[s{i}]")
            else:
                filters.append(f"{chain}[s{i}]")
        else:
            seq = os.path.join(work, sh["kind"])
            card_frames(seq, sh["kind"], sh["dur"], stills)
            args += ["-framerate", str(FPS), "-i", os.path.join(seq, "f%04d.png")]
            filters.append(f"[{idx}:v]fps={FPS},format=yuv420p,setsar=1[s{i}]")
            idx += 1
        labels.append(f"s{i}")

    prev = labels[0]
    for i in range(1, len(shots)):
        off = timeline[i][0]
        sh = shots[i]
        trans = "fadeblack" if sh["kind"] in ("title", "end") or shots[i - 1]["kind"] == "title" else "fade"
        if sh["xf"] <= 0:
            filters.append(f"[{prev}][{labels[i]}]concat=n=2:v=1:a=0[x{i}]")
        else:
            filters.append(f"[{prev}][{labels[i]}]xfade=transition={trans}:duration={sh['xf']:.2f}:offset={off:.3f}[x{i}]")
        prev = f"x{i}"
    filters.append(f"[{prev}]fade=t=in:st=0:d=0.5,fade=t=out:st={total - 1.2:.3f}:d=1.2,format=yuv420p[vout]")

    graph = os.path.join(work, "graph.txt")
    with open(graph, "w") as f:
        f.write(";\n".join(filters))
    video = os.path.join(work, "picture.mp4")
    run(["nice"] + args + ["-/filter_complex", graph, "-map", "[vout]", "-c:v", "libx264", "-preset", "slow",
         "-crf", "19", "-maxrate", "3200k", "-bufsize", "6400k", "-pix_fmt", "yuv420p", "-r", str(FPS), "-an", video])

    raw = os.path.join(work, "mix.wav")
    soundtrack(shots, timeline, total, os.path.join(cap, "audio_sfx.wav"), raw)
    norm = os.path.join(work, "mix_norm.wav")
    loudnorm(raw, norm)

    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "alibi-and-co-trailer.mp4")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", video, "-i", norm, "-map", "0:v", "-map", "1:a",
                    "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart",
                    "-metadata", "title=Alibi & Co. — feature trailer", out], check=True)
    with open(os.path.join(work, "beats.txt"), "w") as f:
        for sh, (a, b) in zip(shots, timeline):
            f.write(f"{a:7.2f} {b:7.2f}  {sh['kind']:5s}  {sh.get('kicker') or ''} | {sh.get('title') or ''}\n")
    print(f"[trailer] {out} ({os.path.getsize(out) / 1e6:.1f} MB)")
    return shots, timeline, m


def poster(cap, out_dir, frame_png, duration_s):
    """A still from the trailer with a play button, for the README (GitHub can't embed the video)."""
    im = Image.open(frame_png).convert("RGBA").resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(2.5))
    arr = np.asarray(im).astype(np.float32)
    ys = np.linspace(-1, 1, H)[:, None, None]
    xs = np.linspace(-1, 1, W)[None, :, None]
    # Darkest behind the button and title, lifting towards the edges so the board still reads.
    r = np.sqrt((xs / 0.62) ** 2 + (ys / 0.75) ** 2)
    arr[..., :3] *= np.clip(0.30 + 0.32 * r, 0.30, 0.62)
    im = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy, r = W // 2, H // 2 - 30, 118
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=CREAM + (244,), outline=OXBLOOD + (255,), width=8)
    tri = [(cx - 38, cy - 62), (cx - 38, cy + 62), (cx + 70, cy)]
    d.polygon(tri, fill=OXBLOOD + (255,))
    f_t, f_k = display(92), sans(34)
    title = "Watch the trailer"
    tw = d.textlength(title, font=f_t)
    d.text(((W - tw) / 2, cy + r + 40), title, font=f_t, fill=CREAM + (255,))
    mins = f"{int(duration_s) // 60}:{int(round(duration_s)) % 60:02d}"
    kick = f"ALIBI & CO.  ·  FEATURE TRAILER  ·  {mins}"
    kw = tracked_width(d, kick, f_k, 6)
    tracked(d, ((W - kw) / 2, cy - r - 86), kick, f_k, LAMP + (255,), 6)
    shadowed(im, layer, 20, (0, 10), 0.75)
    out = os.path.join(out_dir, "trailer-poster.jpg")
    im.convert("RGB").resize((1280, 720), Image.LANCZOS).save(out, quality=88, optimize=True, progressive=True)
    print(f"[poster] {out} ({os.path.getsize(out) / 1e3:.0f} kB)")


def teaser(cap, out_dir, m, work):
    """A 7 s seamless loop for the top of the README: a red ribbon, a FALSE stamp, an alibi breaking."""
    c1, c2 = "case case1", "case case2"
    marlow = (823, 48, 1097, 617)          # the cold open's punch-in on Marlow's line
    shots = [
        (m.t("conflict 1", after=c1) - 1.5, 2.9, None),
        (m.t("struck a_claim", after=c1) - 0.2, 2.4, None),
        (m.t("open marlow", after=c2) + 0.38, 2.5, marlow),
    ]
    xf, head = 0.35, 0.45
    shots.append((shots[0][0], head, None))   # crossfade back into the opening frames for a seamless loop
    args = ["nice", "ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
    filt = []
    for i, (s, d, crop) in enumerate(shots):
        args += ["-ss", f"{s:.3f}", "-t", f"{d:.3f}", "-i", os.path.join(cap, "video.mp4")]
        cr = f"crop={crop[2]}:{crop[3]}:{crop[0]}:{crop[1]}," if crop else ""
        filt.append(f"[{i}:v]setpts=PTS-STARTPTS,fps=15,{cr}scale=960:540:flags=lanczos,format=yuv420p,setsar=1[t{i}]")
    prev, t = "t0", shots[0][1]
    for i in range(1, len(shots)):
        d = head if i == len(shots) - 1 else xf
        t -= d
        filt.append(f"[{prev}][t{i}]xfade=transition=fade:duration={d:.2f}:offset={t:.3f}[y{i}]")
        t += shots[i][1]
        prev = f"y{i}"
    filt.append(f"[{prev}]trim=start={head:.2f},setpts=PTS-STARTPTS[out]")
    out = os.path.join(out_dir, "teaser.webp")
    subprocess.run(args + ["-filter_complex", ";".join(filt), "-map", "[out]", "-c:v", "libwebp_anim", "-lossless", "0",
                           "-q:v", "72", "-compression_level", "6", "-loop", "0", "-an", out], check=True)
    print(f"[teaser] {out} ({os.path.getsize(out) / 1e6:.2f} MB)")


# README screenshots: cursor-free 1920x1080 stills saved by the capture, one per thing worth showing.
SCREENSHOTS = {
    "title": "screenshot-title",                          # the title screen's polaroid wall
    "case1-intro": "screenshot-case-file",                # a case file, typed up
    "case1-hover-route": "screenshot-board-ribbons",      # the core mechanic: pins, ribbons, contradictions
    "case2-open-marlow": "screenshot-alibi-breaks",       # the trailer moment: a corrected clock breaks an alibi
    "case2-map-zoom": "screenshot-town-map",              # walking times on the town map
    "case3-unknown-faces": "screenshot-unknown-faces",    # the twist mechanic: identity by elimination
    "case3-hint": "screenshot-late-game",                 # the last case: four suspects, a town lane, a hint
    "case2-notebook": "screenshot-notebook",              # the UI: Connie's notebook
    "case-files-final": "screenshot-case-files",          # progression: all three cases closed
}


def screenshots(cap, out_dir):
    for src, dst in SCREENSHOTS.items():
        path = os.path.join(cap, "stills", src + ".png")
        out = os.path.join(out_dir, dst + ".jpg")
        Image.open(path).convert("RGB").save(out, quality=90, subsampling=0, optimize=True, progressive=True)
        print(f"[stills] {out} ({os.path.getsize(out) / 1e3:.0f} kB)")


def main():
    argv = sys.argv[1:]
    out_dir = os.path.join(ROOT, "docs", "media")
    only = None
    if "--out" in argv:
        out_dir = argv[argv.index("--out") + 1]
        del argv[argv.index("--out"):argv.index("--out") + 2]
    if "--only" in argv:
        only = argv[argv.index("--only") + 1]
        del argv[argv.index("--only"):argv.index("--only") + 2]
    cap = argv[0] if argv else os.path.join(ROOT, "Captures", "trailer")
    work = os.environ.get("WORK") or tempfile.mkdtemp(prefix="alibi-trailer.")
    os.makedirs(work, exist_ok=True)
    try:
        os.makedirs(out_dir, exist_ok=True)
        m = Markers(os.path.join(cap, "markers.txt"))
        if only in (None, "trailer", "poster"):
            if only != "poster":
                build(cap, out_dir, work)
            trailer = os.path.join(out_dir, "alibi-and-co-trailer.mp4")
            dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", trailer],
                                       capture_output=True, text=True).stdout)
            frame = os.path.join(work, "poster_src.png")
            t = m.t("clock ", after="case case2") + 2.2
            subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-ss", f"{t:.3f}", "-i",
                            os.path.join(cap, "video.mp4"), "-frames:v", "1", frame], check=True)
            poster(cap, out_dir, frame, dur)
        if only in (None, "teaser"):
            teaser(cap, out_dir, m, work)
        if only in (None, "stills"):
            screenshots(cap, out_dir)
    finally:
        if os.environ.get("KEEP_WORK") or os.environ.get("WORK"):
            print("[trailer] work files in", work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
