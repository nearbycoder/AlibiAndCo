#!/usr/bin/env python3
"""Lay PNG previews out in a labelled grid:  contact_sheet.py out.png a.png b.png ...  (or a glob dir)"""
import os, sys, glob
from PIL import Image, ImageDraw
out = sys.argv[1]
files = []
for a in sys.argv[2:]:
    files += sorted(glob.glob(os.path.join(a, "*.png"))) if os.path.isdir(a) else sorted(glob.glob(a))
cols = int(os.environ.get("COLS", 4)); W = int(os.environ.get("CELL", 400))
H = int(W * 0.75) + 22
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (cols * W, rows * H), (24, 24, 28))
d = ImageDraw.Draw(sheet)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB")
    im.thumbnail((W - 6, H - 26))
    x, y = (i % cols) * W, (i // cols) * H
    sheet.paste(im, (x + 3, y + 20))
    d.text((x + 6, y + 4), os.path.basename(f), fill=(235, 225, 200))
sheet.save(out)
print(out, len(files))
