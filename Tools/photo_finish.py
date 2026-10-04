#!/usr/bin/env python3
"""Give Blender renders a 1980s print look (warm toning, soft contrast, grain, vignette).

    .venv/bin/python Tools/photo_finish.py portraits     ArtSource/renders/portrait_*.png -> Assets/Resources/Portraits/
    .venv/bin/python Tools/photo_finish.py photos        ArtSource/renders/photo_*.png    -> Assets/Resources/Photos/
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def finish(img, size, warmth=0.22, grain=0.035, vignette=0.38, seed=0, flash=False, mono=0.0):
    rng = np.random.default_rng(seed)
    im = img.convert("RGB").resize((size, size) if isinstance(size, int) else size, Image.LANCZOS)
    a = np.asarray(im, np.float32) / 255
    lum = a @ np.array([0.299, 0.587, 0.114], np.float32)
    # Desaturate a little (or a lot for monochrome press prints).
    sat = 0.82 - mono * 0.82
    a = lum[..., None] + (a - lum[..., None]) * sat
    # Warm print toning: tint shadows brown, highlights cream.
    sepia = np.stack([lum * 1.07 + 0.04, lum * 0.95 + 0.02, lum * 0.78], -1)
    a = a * (1 - warmth) + sepia * warmth
    # Soft S-curve, lifted blacks, slightly rolled highlights.
    a = 0.04 + 0.92 * (a ** 0.95)
    a = a + 0.08 * np.sin((a - 0.5) * np.pi) * 0.5
    h, w = a.shape[:2]
    y, x = np.mgrid[0:h, 0:w]
    d = np.sqrt(((x - w / 2) / (w / 2)) ** 2 + ((y - h / 2) / (h / 2)) ** 2)
    a *= (1 - vignette * np.clip(d - 0.35, 0, None) ** 1.6)[..., None]
    if flash:
        a += 0.12 * np.exp(-d * 2.2)[..., None]
    n = rng.standard_normal((h, w)).astype(np.float32) * grain
    a += n[..., None]
    a = np.clip(a, 0, 1)
    out = Image.fromarray((a * 255).astype(np.uint8))
    return out.filter(ImageFilter.GaussianBlur(0.35))


if __name__ == "__main__":
    kind = sys.argv[1] if len(sys.argv) > 1 else "portraits"
    if kind == "portraits":
        dst = os.path.join(ROOT, "Assets", "Resources", "Portraits")
        os.makedirs(dst, exist_ok=True)
        for i, f in enumerate(sorted(glob.glob(os.path.join(ROOT, "ArtSource", "renders", "portrait_*.png")))):
            cid = os.path.basename(f)[len("portrait_"):-4]
            finish(Image.open(f), 384, seed=i).save(os.path.join(dst, cid + ".png"))
            print("portrait", cid)
    else:
        dst = os.path.join(ROOT, "Assets", "Resources", "Photos")
        os.makedirs(dst, exist_ok=True)
        for i, f in enumerate(sorted(glob.glob(os.path.join(ROOT, "ArtSource", "renders", "photo_*.png")))):
            pid = os.path.basename(f)[len("photo_"):-4]
            finish(Image.open(f), 512, warmth=0.35, grain=0.05, vignette=0.5, seed=100 + i, flash=True, mono=0.35).save(os.path.join(dst, pid + ".png"))
            print("photo", pid)
