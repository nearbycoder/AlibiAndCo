#!/usr/bin/env python3
"""App icon: a tilted index card pinned to cork with a red pin and a loop of red string, the
"&" from the logo typed across it. Built from the game's own cork/paper textures and fonts.

    .venv/bin/python Tools/make_icon.py      writes Assets/Resources/Textures/app_icon.png (1024 px)
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEX = os.path.join(ROOT, "Assets", "Resources", "Textures")
FONTS = os.path.join(ROOT, "Assets", "Fonts")
S = 1024
SS = 2  # supersample


def tile(path, size, tint=None):
    src = Image.open(path).convert("RGB")
    out = Image.new("RGB", (size, size))
    for y in range(0, size, src.height):
        for x in range(0, size, src.width):
            out.paste(src, (x, y))
    if tint:
        out = Image.blend(out, Image.new("RGB", out.size, tint), 0.25)
    return out


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size - 1, size - 1), radius, fill=255)
    return m


def main():
    n = S * SS
    base = tile(os.path.join(TEX, "cork.png"), n, (150, 98, 58))
    # Lamp pool: warm centre, dark corners.
    light = Image.new("L", (n, n), 40)
    ImageDraw.Draw(light).ellipse((n * 0.08, n * 0.02, n * 0.92, n * 0.9), fill=255)
    light = light.filter(ImageFilter.GaussianBlur(n * 0.14))
    dark = Image.new("RGB", (n, n), (24, 16, 12))
    base = Image.composite(base, dark, light)

    # Card: paper texture, slightly tilted, soft shadow.
    cw, ch = int(n * 0.70), int(n * 0.50)
    card = tile(os.path.join(TEX, "paper.png"), max(cw, ch), (246, 236, 212)).crop((0, 0, cw, ch))
    d = ImageDraw.Draw(card)
    # Ruled lines and a red header rule like an index card.
    for i in range(4):
        y = int(ch * (0.38 + i * 0.15))
        d.line((int(cw * 0.06), y, int(cw * 0.94), y), fill=(150, 170, 196), width=max(2, n // 400))
    d.line((int(cw * 0.06), int(ch * 0.24), int(cw * 0.94), int(ch * 0.24)), fill=(190, 70, 64), width=max(3, n // 300))
    font = ImageFont.truetype(os.path.join(FONTS, "PlayfairDisplay-Italic.ttf"), int(ch * 0.78))
    box = d.textbbox((0, 0), "&", font=font)
    tx = (cw - (box[2] - box[0])) / 2 - box[0]
    ty = (ch * 1.1 - (box[3] - box[1])) / 2 - box[1]
    d.text((tx, ty), "&", font=font, fill=(31, 42, 58))
    card = card.convert("RGBA")
    card.putalpha(rounded_mask(max(cw, ch), int(n * 0.02)).crop((0, 0, cw, ch)))

    angle = -7
    rot = card.rotate(angle, resample=Image.BICUBIC, expand=True)
    cx, cy = n // 2, int(n * 0.54)
    pos = (cx - rot.width // 2, cy - rot.height // 2)
    shadow = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    sh = Image.new("RGBA", rot.size, (0, 0, 0, 150))
    sh.putalpha(rot.split()[3].point(lambda a: int(a * 0.6)))
    shadow.paste(sh, (pos[0] + n // 60, pos[1] + n // 34), sh)
    shadow = shadow.filter(ImageFilter.GaussianBlur(n // 70))
    img = base.convert("RGBA")
    img = Image.alpha_composite(img, shadow)
    img.paste(rot, pos, rot)

    # Red string from the pin out past the edge.
    pin = (int(n * 0.5), int(n * 0.29))
    d = ImageDraw.Draw(img)
    d.line([pin, (int(n * 0.72), int(n * 0.16)), (int(n * 1.05), int(n * 0.10))], fill=(163, 39, 42), width=n // 110, joint="curve")
    d.line([pin, (int(n * 0.26), int(n * 0.12)), (int(n * -0.05), int(n * 0.07))], fill=(163, 39, 42), width=n // 110, joint="curve")
    # Pin head with a highlight.
    r = n // 26
    ps = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(ps).ellipse((pin[0] - r + n // 70, pin[1] - r + n // 45, pin[0] + r + n // 70, pin[1] + r + n // 45), fill=(0, 0, 0, 110))
    img = Image.alpha_composite(img, ps.filter(ImageFilter.GaussianBlur(n // 160)))
    d = ImageDraw.Draw(img)
    d.ellipse((pin[0] - r, pin[1] - r, pin[0] + r, pin[1] + r), fill=(184, 52, 44))
    d.ellipse((pin[0] - r * 0.55, pin[1] - r * 0.65, pin[0] - r * 0.05, pin[1] - r * 0.2), fill=(240, 150, 130))

    img = img.resize((S, S), Image.LANCZOS)
    img.putalpha(rounded_mask(S, int(S * 0.18)))
    out = os.path.join(TEX, "app_icon.png")
    img.save(out)
    print("[icon]", out)


if __name__ == "__main__":
    main()
