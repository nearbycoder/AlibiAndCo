#!/usr/bin/env python3
"""Procedural textures and icons for Alibi & Co.

Writes PNGs into Assets/Resources/Textures and Assets/Resources/Icons.
Everything is generated (noise, strokes, shapes); no third-party images.

    .venv/bin/python Tools/make_textures.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Assets", "Resources", "Textures")
ICO = os.path.join(ROOT, "Assets", "Resources", "Icons")
os.makedirs(TEX, exist_ok=True)
os.makedirs(ICO, exist_ok=True)
rng = np.random.default_rng(1986)


# --------------------------------------------------------------------------- noise helpers

def tile_noise(size, scale, octaves=4, seed=0):
    """Tileable value noise via random lattice + cosine interpolation, summed over octaves."""
    r = np.random.default_rng(seed)
    out = np.zeros((size, size), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = max(1, int(scale * (2 ** o)))
        lattice = r.random((cells, cells)).astype(np.float32)
        coords = np.arange(size) * cells / size
        i0 = np.floor(coords).astype(int)
        f = coords - i0
        f = (1 - np.cos(f * np.pi)) / 2
        i1 = (i0 + 1) % cells
        i0 %= cells
        a = lattice[np.ix_(i0, i0)]
        b = lattice[np.ix_(i0, i1)]
        c = lattice[np.ix_(i1, i0)]
        d = lattice[np.ix_(i1, i1)]
        fx = f[None, :]
        fy = f[:, None]
        v = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
        out += v * amp
        total += amp
        amp *= 0.5
    return out / total


def normalize(a):
    a = a - a.min()
    return a / max(1e-6, a.max())


def save_rgb(arr, name):
    arr = np.clip(arr, 0, 1)
    Image.fromarray((arr * 255).astype(np.uint8)).save(os.path.join(TEX, name + ".png"))


def save_rgba(arr, name, folder=TEX):
    arr = np.clip(arr, 0, 1)
    Image.fromarray((arr * 255).astype(np.uint8), "RGBA").save(os.path.join(folder, name + ".png"))


def height_to_normal(h, strength):
    """Tileable normal map from a height field (OpenGL convention, as Unity expects)."""
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * strength
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * strength
    n = np.stack([-dx, dy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def tint(gray, dark, light):
    dark = np.array(dark, np.float32) / 255
    light = np.array(light, np.float32) / 255
    return dark[None, None, :] * (1 - gray[..., None]) + light[None, None, :] * gray[..., None]


# --------------------------------------------------------------------------- surfaces

def paper():
    s = 512
    fibres = tile_noise(s, 48, 3, seed=1)
    blotch = tile_noise(s, 4, 4, seed=2)
    g = 0.86 + 0.08 * normalize(blotch) + 0.06 * normalize(fibres)
    speck = rng.random((s, s)) > 0.9985
    g[speck] -= 0.25
    img = np.stack([g, g * 0.985, g * 0.955], -1)
    save_rgb(img, "paper")


def cork():
    s = 1024
    base = tile_noise(s, 160, 2, seed=3)
    granules = normalize(base) ** 1.6
    mid = tile_noise(s, 10, 4, seed=4)
    h = 0.6 * granules + 0.4 * normalize(mid)
    pits = rng.random((s, s)) > 0.996
    h[pits] *= 0.3
    h = normalize(h)
    img = tint(h, (120, 82, 46), (226, 182, 128))
    save_rgb(img, "cork")
    save_rgb(height_to_normal(h, 3.5), "cork_n")


def wood():
    s = 1024
    y = np.linspace(0, 1, s, endpoint=False)
    warp = tile_noise(s, 3, 4, seed=5)
    fine = tile_noise(s, 64, 2, seed=6)
    warp2 = tile_noise(s, 9, 3, seed=15)
    rings = np.sin((y[:, None] * 14 + warp * 5.0 + warp2 * 1.2) * 2 * np.pi)
    grain = 0.5 + 0.5 * rings
    streak = tile_noise(s, 256, 1, seed=7)
    h = 0.55 * grain ** 3 + 0.25 * normalize(fine) + 0.2 * normalize(streak)
    h = normalize(h)
    img = tint(h, (58, 34, 19), (128, 82, 48))
    save_rgb(img, "wood")
    save_rgb(height_to_normal(h, 1.4), "wood_n")


def leather():
    s = 512
    n = normalize(tile_noise(s, 90, 3, seed=8))
    cells = normalize(tile_noise(s, 30, 2, seed=9))
    h = normalize(0.6 * n + 0.4 * cells)
    img = tint(h, (34, 52, 40), (78, 104, 82))
    save_rgb(img, "leather")


def brushed():
    s = 256
    n = rng.random((s, s)).astype(np.float32)
    k = np.ones(31) / 31
    n = np.apply_along_axis(lambda r: np.convolve(np.concatenate([r, r[:30]]), k, "valid")[: s], 1, n)
    g = 0.75 + 0.25 * normalize(n)
    save_rgb(np.stack([g, g, g], -1), "brushed")


# --------------------------------------------------------------------------- sprites

def soft_dot():
    s = 64
    y, x = np.mgrid[0:s, 0:s] / (s - 1) * 2 - 1
    a = np.clip(1 - np.sqrt(x * x + y * y), 0, 1) ** 2
    save_rgba(np.stack([np.ones_like(a), np.ones_like(a), np.ones_like(a), a], -1), "dot")


def soft_shadow():
    """Blurred rounded rectangle, used under lifted cards."""
    s = 256
    im = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([40, 40, s - 40, s - 40], radius=14, fill=255)
    im = im.filter(ImageFilter.GaussianBlur(18))
    a = np.asarray(im, np.float32) / 255
    save_rgba(np.stack([np.zeros_like(a)] * 3 + [a], -1), "shadow")


def soft_rect():
    """White blurred rounded rectangle: tinted glows around cards."""
    s = 256
    im = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([46, 46, s - 46, s - 46], radius=18, fill=255)
    im = im.filter(ImageFilter.GaussianBlur(20))
    a = np.asarray(im, np.float32) / 255
    save_rgba(np.stack([np.ones_like(a)] * 3 + [a], -1), "softrect")


def hatch():
    """Diagonal red hatching for impossible stretches of a ribbon (tileable)."""
    s = 64
    y, x = np.mgrid[0:s, 0:s]
    v = ((x + y) % 16) < 7
    a = np.where(v, 1.0, 0.0).astype(np.float32)
    im = Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))
    a = np.asarray(im, np.float32) / 255
    save_rgba(np.stack([np.ones_like(a)] * 3 + [a], -1), "hatch")


def dash():
    s = 64
    a = np.zeros((8, s), np.float32)
    a[:, :34] = 1
    save_rgba(np.stack([np.ones_like(a)] * 3 + [a], -1), "dash")


def ruled():
    """Index-card ruling: blue lines and one red margin line."""
    w, h = 512, 320
    img = np.ones((h, w, 4), np.float32)
    img[..., 3] = 0
    for y in range(70, h, 34):
        img[y:y + 2, :, :3] = np.array([0.45, 0.6, 0.78])
        img[y:y + 2, :, 3] = 0.55
    img[44:47, :, :3] = np.array([0.8, 0.3, 0.3])
    img[44:47, :, 3] = 0.6
    save_rgba(img, "ruled")


def grunge():
    """Ink-stamp mask: mostly opaque with worn patches."""
    s = 256
    n = normalize(tile_noise(s, 18, 4, seed=11))
    a = np.clip((n - 0.18) * 3.0, 0, 1)
    speck = rng.random((s, s)) > 0.97
    a[speck] *= 0.2
    save_rgba(np.stack([np.ones_like(a)] * 3 + [a], -1), "grunge")


def vignette_card():
    """Subtle edge darkening for paper (multiply)."""
    s = 256
    y, x = np.mgrid[0:s, 0:s] / (s - 1) * 2 - 1
    d = np.maximum(np.abs(x), np.abs(y))
    g = 1 - 0.12 * np.clip((d - 0.75) / 0.25, 0, 1) ** 2
    save_rgb(np.stack([g, g * 0.99, g * 0.97], -1), "paper_edge")


# --------------------------------------------------------------------------- icons

SS = 4          # supersampling
IS = 128        # final icon size


class Pen:
    def __init__(self):
        self.im = Image.new("L", (IS * SS, IS * SS), 0)
        self.d = ImageDraw.Draw(self.im)

    def p(self, x, y):
        return (x * SS * IS / 100, y * SS * IS / 100)

    def w(self, v):
        return int(v * SS * IS / 100)

    def line(self, pts, width=8):
        self.d.line([self.p(*q) for q in pts], fill=255, width=self.w(width), joint="curve")
        for q in (pts[0], pts[-1]):
            self.circle(q[0], q[1], width / 2)

    def poly(self, pts, fill=255):
        self.d.polygon([self.p(*q) for q in pts], fill=fill)

    def rect(self, x0, y0, x1, y1, r=0, fill=255):
        self.d.rounded_rectangle([self.p(x0, y0), self.p(x1, y1)], radius=self.w(r), fill=fill)

    def rect_o(self, x0, y0, x1, y1, r=0, width=7):
        self.d.rounded_rectangle([self.p(x0, y0), self.p(x1, y1)], radius=self.w(r), outline=255, width=self.w(width))

    def circle(self, x, y, r, fill=255):
        self.d.ellipse([self.p(x - r, y - r), self.p(x + r, y + r)], fill=fill)

    def ring(self, x, y, r, width=7):
        self.d.ellipse([self.p(x - r, y - r), self.p(x + r, y + r)], outline=255, width=self.w(width))

    def arc(self, x, y, r, a0, a1, width=7):
        self.d.arc([self.p(x - r, y - r), self.p(x + r, y + r)], a0, a1, fill=255, width=self.w(width))

    def erase_circle(self, x, y, r):
        self.circle(x, y, r, fill=0)

    def erase_rect(self, x0, y0, x1, y1, r=0):
        self.rect(x0, y0, x1, y1, r, fill=0)

    def save(self, name):
        im = self.im.resize((IS, IS), Image.LANCZOS)
        a = np.asarray(im, np.float32) / 255
        save_rgba(np.stack([np.ones_like(a)] * 3 + [a], -1), name, ICO)


def icons():
    I = {}

    def icon(fn):
        I[fn.__name__] = fn
        return fn

    @icon
    def anchor(p):
        p.ring(50, 18, 8, 7); p.line([(50, 26), (50, 86)], 8); p.line([(34, 40), (66, 40)], 8)
        p.arc(50, 58, 30, 20, 160, 8); p.poly([(14, 68), (24, 62), (22, 74)]); p.poly([(86, 68), (76, 62), (78, 74)])

    @icon
    def cup(p):
        p.rect(20, 34, 68, 82, 10); p.ring(74, 52, 12, 7); p.rect(14, 84, 82, 90, 3)
        p.line([(34, 12), (30, 24)], 5); p.line([(46, 10), (42, 24)], 5); p.line([(58, 12), (54, 24)], 5)

    @icon
    def pint(p):
        p.poly([(24, 18), (70, 18), (64, 88), (30, 88)]); p.erase_rect(30, 30, 64, 34)
        p.ring(76, 48, 11, 6)

    @icon
    def pier(p):
        p.rect(10, 34, 90, 46, 2)
        for x in (18, 38, 58, 78): p.rect(x, 46, x + 6, 84)
        p.arc(30, 92, 14, 200, 340, 5); p.arc(66, 92, 14, 200, 340, 5)

    @icon
    def music(p):
        p.circle(30, 76, 12); p.circle(70, 66, 12); p.line([(40, 76), (40, 22)], 7); p.line([(80, 66), (80, 14)], 7)
        p.poly([(40, 18), (80, 10), (80, 24), (40, 32)])

    @icon
    def star(p):
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            r = 40 if i % 2 == 0 else 17
            pts.append((50 + r * math.cos(a), 54 + r * math.sin(a)))
        p.poly(pts)

    @icon
    def path(p):
        for i, (x, y) in enumerate([(20, 84), (34, 66), (52, 54), (70, 40), (82, 20)]):
            p.circle(x, y, 7 - i * 0.6)

    @icon
    def light(p):
        p.poly([(38, 90), (62, 90), (58, 34), (42, 34)]); p.rect(36, 22, 64, 34, 3); p.poly([(40, 22), (60, 22), (50, 10)])
        p.line([(68, 26), (90, 16)], 5); p.line([(68, 30), (92, 30)], 5); p.line([(32, 26), (10, 16)], 5); p.line([(32, 30), (8, 30)], 5)
        p.erase_rect(46, 48, 54, 56)

    @icon
    def key(p):
        p.ring(30, 50, 16, 9); p.line([(46, 50), (88, 50)], 9); p.line([(76, 50), (76, 66)], 8); p.line([(86, 50), (86, 62)], 8)

    @icon
    def bread(p):
        p.rect(14, 40, 86, 80, 12); p.circle(50, 42, 30); p.erase_rect(0, 82, 100, 100)
        for x in (34, 50, 66): p.line([(x - 6, 30), (x + 4, 52)], 3) if False else None
        p.d.line([p.p(30, 36), p.p(40, 54)], fill=0, width=p.w(4))
        p.d.line([p.p(48, 32), p.p(56, 52)], fill=0, width=p.w(4))
        p.d.line([p.p(64, 36), p.p(72, 54)], fill=0, width=p.w(4))

    @icon
    def clock(p):
        p.ring(50, 52, 36, 8); p.line([(50, 52), (50, 28)], 7); p.line([(50, 52), (66, 60)], 7)

    @icon
    def bus(p):
        p.rect(14, 18, 86, 78, 10); p.erase_rect(22, 26, 78, 50, 3); p.circle(30, 84, 9); p.circle(70, 84, 9)
        p.erase_rect(22, 60, 34, 68, 2); p.erase_rect(66, 60, 78, 68, 2)

    @icon
    def film(p):
        p.rect(18, 14, 82, 86, 6)
        for y in (22, 38, 54, 70):
            p.erase_rect(22, y, 30, y + 8, 2); p.erase_rect(70, y, 78, y + 8, 2)
        p.erase_rect(36, 22, 64, 46, 2); p.erase_rect(36, 54, 64, 78, 2)

    @icon
    def bell(p):
        p.poly([(50, 14), (70, 30), (76, 70), (86, 78), (14, 78), (24, 70), (30, 30)]); p.circle(50, 86, 8)

    @icon
    def train(p):
        p.rect(22, 12, 78, 74, 14); p.erase_rect(30, 22, 70, 44, 4); p.erase_circle(36, 60, 5); p.erase_circle(64, 60, 5)
        p.line([(30, 76), (18, 92)], 6); p.line([(70, 76), (82, 92)], 6)

    @icon
    def leaf(p):
        p.poly([(18, 84), (24, 46), (46, 22), (84, 14), (78, 52), (54, 78)]); p.d.line([p.p(20, 86), p.p(70, 30)], fill=0, width=p.w(5))

    # card kinds
    @icon
    def statement(p):
        p.rect(10, 16, 90, 70, 14); p.poly([(26, 66), (46, 66), (22, 88)])
        p.erase_circle(34, 44, 7); p.erase_circle(50, 44, 7); p.erase_circle(66, 44, 7)

    @icon
    def receipt(p):
        pts = [(22, 8), (78, 8), (78, 82)]
        for i in range(6):
            x = 78 - (i + 0.5) * 56 / 6
            pts += [(x, 92), (78 - (i + 1) * 56 / 6, 82)]
        p.poly(pts)
        for y in (22, 34, 46, 58): p.erase_rect(32, y, 68, y + 5)

    @icon
    def ledger(p):
        p.rect(18, 10, 82, 90, 6); p.erase_rect(28, 18, 74, 82, 2)
        for y in (28, 40, 52, 64): p.rect(32, y, 70, y + 5)
        p.rect(18, 10, 28, 90, 2)

    @icon
    def call(p):
        p.poly([(22, 14), (40, 14), (44, 32), (34, 40), (60, 66), (68, 56), (86, 60), (86, 78), (74, 88), (52, 82), (18, 48), (14, 26)])

    @icon
    def ticket(p):
        p.rect(8, 26, 92, 74, 4); p.erase_circle(8, 50, 10); p.erase_circle(92, 50, 10)
        for y in (32, 44, 56, 68): p.erase_rect(62, y, 66, y + 6)

    @icon
    def photo(p):
        p.rect(10, 26, 90, 82, 8); p.rect(24, 16, 46, 30, 3); p.erase_circle(50, 54, 18); p.circle(50, 54, 11); p.erase_circle(78, 38, 4)

    @icon
    def note(p):
        p.rect(14, 30, 86, 84, 8); p.erase_circle(36, 58, 14); p.circle(36, 58, 7)
        for y in (46, 56, 66): p.erase_rect(58, y, 78, y + 4)
        p.line([(30, 30), (70, 12)], 5)

    # ui
    @icon
    def lock(p):
        p.rect(18, 44, 82, 90, 8); p.arc(50, 44, 22, 180, 360, 9); p.line([(28, 44), (28, 38)], 9); p.line([(72, 44), (72, 38)], 9)
        p.erase_circle(50, 62, 7); p.erase_rect(47, 62, 53, 78)

    @icon
    def unlock(p):
        p.rect(18, 44, 82, 90, 8); p.arc(64, 30, 20, 180, 360, 9); p.line([(44, 30), (44, 42)], 9)
        p.erase_circle(50, 62, 7); p.erase_rect(47, 62, 53, 78)

    @icon
    def walk(p):
        p.circle(56, 14, 9); p.line([(52, 28), (46, 56)], 10); p.line([(46, 56), (60, 74), (58, 92)], 9); p.line([(46, 56), (36, 74), (24, 88)], 9)
        p.line([(50, 32), (64, 46), (74, 48)], 7); p.line([(50, 32), (36, 42), (30, 54)], 7)

    @icon
    def clip(p):
        p.arc(50, 24, 16, 180, 360, 7); p.line([(34, 24), (34, 76)], 7); p.line([(66, 24), (66, 64)], 7)
        p.arc(42, 76, 8, 0, 180, 7); p.line([(50, 76), (50, 32)], 7)

    @icon
    def alert(p):
        p.poly([(50, 8), (94, 88), (6, 88)]); p.erase_rect(46, 32, 54, 62, 3); p.erase_circle(50, 74, 5)

    @icon
    def check(p):
        p.line([(16, 54), (40, 78), (86, 24)], 12)

    @icon
    def cross(p):
        p.line([(20, 20), (80, 80)], 12); p.line([(80, 20), (20, 80)], 12)

    @icon
    def question(p):
        p.arc(50, 34, 20, 200, 400, 11); p.line([(58, 50), (50, 60), (50, 66)], 11); p.circle(50, 84, 7)

    @icon
    def pin(p):
        p.circle(50, 34, 24); p.line([(50, 56), (50, 94)], 5)

    @icon
    def badge(p):
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            r = 44 if i % 2 == 0 else 20
            pts.append((50 + r * math.cos(a), 52 + r * math.sin(a)))
        p.poly(pts)

    @icon
    def eye(p):
        p.d.ellipse([p.p(8, 28), p.p(92, 72)], fill=255); p.erase_circle(50, 50, 18); p.circle(50, 50, 10)

    @icon
    def hourglass(p):
        p.poly([(22, 10), (78, 10), (54, 50), (78, 90), (22, 90), (46, 50)]); p.erase_rect(30, 18, 70, 22)

    for name, fn in I.items():
        pen = Pen()
        fn(pen)
        pen.save(name)
    return list(I.keys())


if __name__ == "__main__":
    paper(); cork(); wood(); leather(); brushed()
    soft_dot(); soft_shadow(); soft_rect(); hatch(); dash(); ruled(); grunge(); vignette_card()
    names = icons()
    print("textures ->", TEX)
    print("icons ->", ", ".join(names))
