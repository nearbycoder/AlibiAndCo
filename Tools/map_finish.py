#!/usr/bin/env python3
"""Paper finish for the Blender town-map render -> Assets/Resources/Textures/map_town.png"""
import os
import numpy as np
from PIL import Image, ImageFilter
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = Image.open(os.path.join(ROOT, "ArtSource", "renders", "map_town_raw.png")).convert("RGB")
a = np.asarray(src, np.float32) / 255
h, w = a.shape[:2]
rng = np.random.default_rng(3)
# Paper fibres and blotches.
def noise(scale):
    n = rng.standard_normal((h // scale + 2, w // scale + 2)).astype(np.float32)
    return np.asarray(Image.fromarray(n).resize((w, h), Image.BICUBIC), np.float32)
paper = 1 + 0.03 * noise(64) + 0.02 * noise(8) + 0.015 * rng.standard_normal((h, w)).astype(np.float32)
a *= paper[..., None]
# Ink bleed: slight blur and a touch of darkening at edges.
img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
a = np.asarray(img, np.float32) / 255
y, x = np.mgrid[0:h, 0:w]
d = np.maximum(np.abs(x / w - 0.5), np.abs(y / h - 0.5)) * 2
a *= (1 - 0.18 * np.clip(d - 0.82, 0, None) / 0.18)[..., None]
a = a * np.array([1.0, 0.985, 0.95])
Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).save(os.path.join(ROOT, "Assets", "Resources", "Textures", "map_town.png"))
print("map_town.png", w, h)
