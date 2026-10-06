#!/usr/bin/env python3
"""Front pages of the Wrenhaven Gazette, one per case, for the newspaper lying on the desk.

    .venv/bin/python Tools/newspaper.py [case4 ...]     writes Assets/Resources/Textures/news_<case>.png

Each page reports the incident the morning after (never a suspect), with a halftoned picture,
a masthead, column rules and body text set small enough to read as newsprint texture.
"""
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Assets", "Resources", "Textures")
FONTS = os.path.join(ROOT, "Assets", "Fonts")
W, H = 1400, 980
INK = (28, 26, 24)

PAGES = {
    "case1": dict(date="SATURDAY 11 OCTOBER 1986", head="BAKE-OFF SHOWSTOPPER SABOTAGED",
                  sub="Four tiers of lemon and lavender found face-down on Penhallow's floor",
                  pic=("Textures/map_town.png", (0.42, 0.18, 0.66, 0.5)), caption="Penhallow's Bakery, Harbour Street, where the cake was found."),
    "case2": dict(date="SUNDAY 19 OCTOBER 1986", head="REGATTA CUP STOLEN",
                  sub="Century-old silver trophy lifted from Yacht Club boathouse in under an hour",
                  pic=("Textures/map_town.png", (0.05, 0.45, 0.35, 0.85)), caption="The Yacht Club boathouse at low water."),
    "case3": dict(date="MONDAY 3 NOVEMBER 1986", head="WREN POINT LIGHT GOES DARK",
                  sub="Trawler Merry Wren lost on Mallow Rocks as Harvest Ball gale rages",
                  pic=("Photos/flicker.png", (0.0, 0.0, 1.0, 1.0)), caption="Harvest Ball at the Grand, Saturday. Photograph: Wendell Fry."),
    "case4": dict(date="THURSDAY 6 NOVEMBER 1986", head="LIFEBOAT BOX STOLEN ON BONFIRE NIGHT",
                  sub="A year of the Appeal taken from the Mayor's parlour as the town watched the sky",
                  pic=("Textures/map_town.png", (0.46, 0.22, 0.70, 0.54)), caption="Market Square and the Town Hall, whose clock struck the hour."),
    "case5": dict(date="MONDAY 15 DECEMBER 1986", head="WRENHAVEN LILY CUT ON EVE OF SHOW",
                  sub="Once-in-seven-years orchid lifted from the Park Glasshouse in a hard frost",
                  pic=("Textures/map_town.png", (0.02, 0.30, 0.30, 0.66)), caption="Penrose Park and its Victorian glasshouse."),
}

WORDS = ("the harbour council said on friday that a new survey of the quay would begin after the winter storms and that "
         "fishermen should expect delays at the slipway while the work is done residents of upper town have asked for "
         "better lighting along the cliff path and the lifeboat crew will hold its annual dinner at the lantern next month "
         "tickets from the post office the parish notes that the jumble sale raised a record sum for the church roof").split()


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def halftone(img, box, size):
    w, h = img.size
    crop = img.crop((int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h))).convert("L").resize(size, Image.LANCZOS)
    crop = ImageOps.autocontrast(crop, cutoff=1)          # press photos are dark; level them before screening
    a = (np.asarray(crop, np.float32) / 255) ** 0.6
    a = 0.08 + 0.9 * a
    out = Image.new("L", size, 235)
    d = ImageDraw.Draw(out)
    step = 7
    for y in range(0, size[1], step):
        for x in range(0, size[0], step):
            v = a[min(y + step // 2, size[1] - 1), min(x + step // 2, size[0] - 1)]
            r = (1 - v) * step * 0.72
            if r > 0.4:
                cx, cy = x + step / 2 + (step / 2 if (y // step) % 2 else 0), y + step / 2
                d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=30)
    return out.convert("RGB")


def body_text(d, x0, y0, x1, y1, rnd, size=13):
    f = font("DejaVuSans.ttf", size)
    y = y0
    while y < y1 - size:
        line, x = [], x0
        while True:
            w = rnd.choice(WORDS)
            ww = d.textlength(w + " ", font=f)
            if x + ww > x1:
                break
            line.append(w)
            x += ww
        if rnd.random() < 0.08:
            line = line[: max(2, len(line) // 2)]   # paragraph end
        d.text((x0, y), " ".join(line), font=f, fill=(70, 66, 60))
        y += size + 4


def page(cid, p):
    rnd = random.Random(cid)
    img = Image.new("RGB", (W, H), (233, 228, 214))
    noise = (np.random.default_rng(len(cid)).normal(0, 6, (H, W, 1))).astype(np.int16)
    img = Image.fromarray(np.clip(np.asarray(img, np.int16) + noise, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    m = 50
    # Masthead.
    mast = font("AbrilFatface-Regular.ttf", 92)
    title = "The Wrenhaven Gazette"
    tw = d.textlength(title, font=mast)
    d.text(((W - tw) / 2, 28), title, font=mast, fill=INK)
    d.line((m, 140, W - m, 140), fill=INK, width=3)
    d.line((m, 146, W - m, 146), fill=INK, width=1)
    small = font("IBMPlexSansCondensed-SemiBold.ttf", 22)
    d.text((m, 152), p["date"], font=small, fill=INK)
    price = "No. 4,812   ·   15p"
    d.text((W - m - d.textlength(price, font=small), 152), price, font=small, fill=INK)
    d.line((m, 184, W - m, 184), fill=INK, width=1)
    # Headline across the page.
    hf = font("PlayfairDisplay.ttf", 84)
    hw = d.textlength(p["head"], font=hf)
    scale = min(1.0, (W - 2 * m) / hw)
    if scale < 1:
        hf = font("PlayfairDisplay.ttf", int(84 * scale))
        hw = d.textlength(p["head"], font=hf)
    d.text(((W - hw) / 2, 196), p["head"], font=hf, fill=INK, stroke_width=2, stroke_fill=INK)
    sf = font("PlayfairDisplay-Italic.ttf", 34)
    sw = d.textlength(p["sub"], font=sf)
    d.text(((W - sw) / 2, 302), p["sub"], font=sf, fill=(50, 46, 42))
    d.line((m, 356, W - m, 356), fill=INK, width=2)
    # Picture on the left two columns, text in the rest.
    src = Image.open(os.path.join(ROOT, "Assets", "Resources", p["pic"][0]))
    pw, ph = 560, 400
    img.paste(halftone(src, p["pic"][1], (pw, ph)), (m, 376))
    d.rectangle((m, 376, m + pw, 376 + ph), outline=INK, width=2)
    cap = font("IBMPlexSansCondensed-Medium.ttf", 19)
    d.text((m, 784), p["caption"], font=cap, fill=(60, 56, 50))
    body_text(d, m, 818, m + pw, H - 20, rnd)
    cols = 3
    cx0 = m + pw + 30
    cw = (W - m - cx0 - (cols - 1) * 24) / cols
    for i in range(cols):
        x0 = cx0 + i * (cw + 24)
        if i > 0:
            d.line((x0 - 12, 376, x0 - 12, H - 20), fill=(120, 114, 104), width=1)
        y0 = 376
        if i == 2:   # a second, smaller story
            kf = font("PlayfairDisplay.ttf", 30)
            d.text((x0, y0), rnd.choice(["Harbour lights", "Council notes", "Lifeboat dinner"]), font=kf, fill=INK, stroke_width=1, stroke_fill=INK)
            y0 += 48
        body_text(d, x0, y0, x0 + cw, H - 20, rnd)
    img = img.filter(ImageFilter.GaussianBlur(0.4))
    out = os.path.join(TEX, f"news_{cid}.png")
    img.save(out)
    print("[news]", out)


if __name__ == "__main__":
    import sys
    only = sys.argv[1:]
    for cid, p in PAGES.items():
        if not only or cid in only:
            page(cid, p)
