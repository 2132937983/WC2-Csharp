#!/usr/bin/env python3
"""Draws the TNO 1962 flags of the mod (mods/tno-1962/flag_<id>.png).

The game draws a map flag from a 65 x 115 picture: a pole with the cloth at the top right,
anchored at (21, 99) — the pole base. A mod's own picture is picked up by CGameRes.LoadFlagImage
when the army atlas has no flag for the country.

Nothing here uses a banned symbol: the German Reich flies the black-white-red colours of
1871-1918, and the Reichskommissariate carry those colours in a hoist stripe instead.

Usage: python Tools/tno/make-flags.py
"""

import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "mods", "tno-1962")
SOURCE = os.path.join(ROOT, "Tools", "tno", "source", "flags")

W, H = 65, 115                 # the atlas flag picture
CLOTH = (21, 13, 64, 42)       # left, top, right, bottom of the cloth in that picture
POLE_X = 15

BLACK = (26, 26, 28, 255)
WHITE = (238, 238, 232, 255)
RED = (176, 42, 42, 255)
REICH_RED = (150, 30, 30, 255)


def flag(painter):
    """A whole flag picture: the cloth, the pole, the finial and the shadow."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    # shadow at the pole base
    draw.ellipse([18 - 13, 106 - 6, 18 + 13, 106 + 6], fill=(0, 0, 0, 70))
    # pole: dark wood with a lit left edge, climbing to a finial
    draw.rectangle([POLE_X - 1, 8, POLE_X + 3, 108], fill=(96, 68, 44, 255))
    draw.rectangle([POLE_X - 1, 8, POLE_X, 108], fill=(140, 104, 66, 255))
    draw.polygon([(POLE_X - 2, 2), (POLE_X + 4, 2), (POLE_X + 1, 12)], fill=(196, 196, 190, 255))

    left, top, right, bottom = CLOTH
    cw, ch = right - left, bottom - top
    cloth = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    painter(ImageDraw.Draw(cloth), cw, ch)
    ImageDraw.Draw(cloth).rectangle([0, 0, cw - 1, ch - 1], outline=(20, 20, 20, 170))
    img.paste(cloth, (left, top), cloth)
    return img


def hbands(colours):
    def paint(d, w, h):
        n = len(colours)
        for i, c in enumerate(colours):
            d.rectangle([0, round(i * h / n) - (1 if i else 0), w, round((i + 1) * h / n)], fill=c)
    return paint


def vbands(colours):
    def paint(d, w, h):
        n = len(colours)
        for i, c in enumerate(colours):
            d.rectangle([round(i * w / n) - (1 if i else 0), 0, round((i + 1) * w / n), h], fill=c)
    return paint


def hoist_stripe(inner, width=6):
    """the inner design plus a black-white-red hoist stripe (German administration)"""
    def paint(d, w, h):
        inner(d, w, h)
        step = max(1, width // 3)
        for i, c in enumerate((BLACK, WHITE, REICH_RED)):
            d.rectangle([i * step, 0, (i + 1) * step, h], fill=c)
    return paint


def nordic_cross(background, cross, band=0.22):
    def paint(d, w, h):
        d.rectangle([0, 0, w, h], fill=background)
        t = max(2, round(h * band))
        cx = round(w * 0.36)
        d.rectangle([cx - t // 2, 0, cx + t // 2, h], fill=cross)
        d.rectangle([0, h // 2 - t // 2, w, h // 2 + t // 2], fill=cross)
    return paint


def disc(background, colour, r=0.34):
    def paint(d, w, h):
        d.rectangle([0, 0, w, h], fill=background)
        rr = round(min(w, h) * r)
        d.ellipse([w // 2 - rr, h // 2 - rr, w // 2 + rr, h // 2 + rr], fill=colour)
    return paint


def korea(d, w, h):
    d.rectangle([0, 0, w, h], fill=WHITE)
    rr = round(min(w, h) * 0.24)
    cx, cy = w // 2, h // 2
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(190, 50, 60, 255))
    d.chord([cx - rr, cy - rr, cx + rr, cy + rr], 90, 270, fill=(40, 60, 150, 255))


def union_jack(d, w, h, x0=0, y0=0):
    d.rectangle([x0, y0, x0 + w, y0 + h], fill=(32, 52, 120, 255))
    t = max(2, round(h * 0.18))
    d.line([x0, y0, x0 + w, y0 + h], fill=WHITE, width=t)
    d.line([x0 + w, y0, x0, y0 + h], fill=WHITE, width=t)
    d.line([x0, y0, x0 + w, y0 + h], fill=(190, 40, 50, 255), width=max(1, t // 2))
    d.line([x0 + w, y0, x0, y0 + h], fill=(190, 40, 50, 255), width=max(1, t // 2))
    d.rectangle([x0, y0 + h // 2 - t // 2, x0 + w, y0 + h // 2 + t // 2], fill=WHITE)
    d.rectangle([x0 + w // 2 - t // 2, y0, x0 + w // 2 + t // 2, y0 + h], fill=WHITE)
    d.rectangle([x0, y0 + h // 2 - max(1, t // 4), x0 + w, y0 + h // 2 + max(1, t // 4)],
                fill=(190, 40, 50, 255))
    d.rectangle([x0 + w // 2 - max(1, t // 4), y0, x0 + w // 2 + max(1, t // 4), y0 + h],
                fill=(190, 40, 50, 255))


def greece(d, w, h):
    n = 5
    for i in range(n):
        d.rectangle([0, round(i * h / n) - (1 if i else 0), w, round((i + 1) * h / n)],
                    fill=(40, 80, 160, 255) if i % 2 == 0 else WHITE)
    cw, ch = round(w * 0.44), round(h * 0.44)
    d.rectangle([0, 0, cw, ch], fill=(40, 80, 160, 255))
    t = max(2, round(ch * 0.34))
    d.rectangle([0, ch // 2 - t // 2, cw, ch // 2 + t // 2], fill=WHITE)
    d.rectangle([cw // 2 - t // 2, 0, cw // 2 + t // 2, ch], fill=WHITE)


def usa(d, w, h):
    for i in range(7):
        d.rectangle([0, round(i * h / 7) - (1 if i else 0), w, round((i + 1) * h / 7)],
                    fill=RED if i % 2 == 0 else WHITE)
    cw, ch = round(w * 0.46), round(h * 0.52)
    d.rectangle([0, 0, cw, ch], fill=(32, 52, 120, 255))
    for i in range(3):
        for j in range(4):
            cx = round(cw * (0.14 + 0.24 * j))
            cy = round(ch * (0.18 + 0.32 * i))
            d.point([(cx, cy)], fill=WHITE)


def canada(d, w, h):
    d.rectangle([0, 0, w, h], fill=WHITE)
    bw = round(w * 0.27)
    d.rectangle([0, 0, bw, h], fill=RED)
    d.rectangle([w - bw, 0, w, h], fill=RED)
    m = round(min(w, h) * 0.28)
    d.polygon([(w // 2, h // 2 - m), (w // 2 + m // 2, h // 2), (w // 2, h // 2 + m),
               (w // 2 - m // 2, h // 2)], fill=RED)


def australia(d, w, h):
    d.rectangle([0, 0, w, h], fill=(32, 52, 120, 255))
    union_jack(d, round(w * 0.46), round(h * 0.46))
    for cx, cy in ((0.72, 0.28), (0.84, 0.5), (0.62, 0.66), (0.76, 0.84)):
        x, y = round(w * cx), round(h * cy)
        d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=WHITE)


def turkey(d, w, h):
    d.rectangle([0, 0, w, h], fill=(180, 40, 45, 255))
    r = round(min(w, h) * 0.26)
    cx, cy = round(w * 0.4), h // 2
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=WHITE)
    d.ellipse([cx - r + max(2, r // 2), cy - r, cx + r + max(2, r // 2), cy + r],
              fill=(180, 40, 45, 255))
    sx = round(w * 0.68)
    d.ellipse([sx - 2, cy - 2, sx + 2, cy + 2], fill=WHITE)


def india(d, w, h):
    third = h // 3
    d.rectangle([0, 0, w, third], fill=(240, 150, 60, 255))
    d.rectangle([0, third, w, 2 * third], fill=WHITE)
    d.rectangle([0, 2 * third, w, h], fill=(60, 130, 70, 255))
    r = max(3, round(h * 0.13))
    cx, cy = w // 2, h // 2
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(40, 60, 150, 255), width=1)


def croatia(d, w, h):
    hbands([(200, 40, 50, 255), WHITE, (40, 60, 150, 255)])(d, w, h)
    sx, sy = w // 2, round(h * 0.4)
    s = max(3, round(h * 0.15))
    d.rectangle([sx - s, sy - s, sx + s, sy + s], fill=WHITE, outline=(200, 40, 50, 255))
    for i in range(2):
        for j in range(2):
            if (i + j) % 2 == 0:
                d.rectangle([sx - s + i * s, sy - s + j * s, sx + i * s, sy + j * s],
                            fill=(200, 40, 50, 255))


def switzerland(d, w, h):
    d.rectangle([0, 0, w, h], fill=(200, 40, 40, 255))
    t = max(2, round(h * 0.2))
    L = round(h * 0.46)
    d.rectangle([w // 2 - t // 2, h // 2 - L // 2, w // 2 + t // 2, h // 2 + L // 2], fill=WHITE)
    d.rectangle([w // 2 - L // 2, h // 2 - t // 2, w // 2 + L // 2, h // 2 + t // 2], fill=WHITE)


def portugal(d, w, h):
    d.rectangle([0, 0, w, h], fill=(190, 40, 50, 255))
    d.rectangle([0, 0, round(w * 0.4), h], fill=(40, 110, 60, 255))
    cx, cy = round(w * 0.4), h // 2
    r = max(2, round(h * 0.13))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(240, 200, 60, 255))


def mexico(d, w, h):
    vbands([(40, 110, 60, 255), WHITE, (190, 40, 50, 255)])(d, w, h)
    cx, cy = w // 2, h // 2
    r = max(2, round(h * 0.1))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(150, 110, 60, 255))


FLAGS = {
    # Einheitspakt
    "de": hbands([BLACK, WHITE, REICH_RED]),
    "pl": hoist_stripe(hbands([WHITE, (200, 40, 50, 255)]), 6),
    "nl": hoist_stripe(hbands([(170, 40, 50, 255), WHITE, (40, 70, 140, 255)]), 6),
    "be": hoist_stripe(vbands([BLACK, (240, 200, 60, 255), (190, 40, 50, 255)]), 6),
    "no": hoist_stripe(nordic_cross((170, 40, 50, 255), (40, 60, 130, 255)), 6),
    "dk": nordic_cross((190, 40, 50, 255), WHITE),
    "fr": vbands([(40, 60, 130, 255), WHITE, (190, 40, 50, 255)]),
    "fl": nordic_cross(WHITE, (40, 70, 150, 255)),
    "ro": vbands([(30, 60, 150, 255), (240, 200, 60, 255), (190, 40, 50, 255)]),
    "hu": hbands([(190, 40, 50, 255), WHITE, (40, 120, 70, 255)]),
    "bg": hbands([WHITE, (40, 120, 70, 255), (190, 40, 50, 255)]),
    "ja": disc(WHITE, (200, 40, 45, 255)),
    "cn": hbands([(200, 40, 50, 255), (240, 200, 60, 255), (40, 70, 150, 255), WHITE, BLACK]),
    "tw": hbands([(240, 200, 60, 255), (200, 40, 50, 255), (40, 70, 150, 255), WHITE, BLACK]),
    "nk": korea,

    # Triumvirate
    "it": vbands([(40, 130, 70, 255), WHITE, (190, 40, 50, 255)]),
    "es": hbands([(190, 40, 50, 255), (240, 200, 60, 255), (190, 40, 50, 255)]),
    "pt": portugal,
    "tr": turkey,
    "gr": greece,
    "yu": croatia,

    # OFN
    "am": usa,
    "gb": union_jack,
    "ca": canada,
    "au": australia,
    "mx": mexico,

    # neutral
    "se": nordic_cross((40, 70, 150, 255), (240, 200, 60, 255)),
    "ch": switzerland,
    "in": india,
    "ru": hbands([WHITE, (40, 70, 150, 255), (190, 40, 50, 255)]),
}


def from_wiki(country):
    """The flag picture downloaded from the TNO wiki, or None (then the drawn design is used)."""
    path = os.path.join(SOURCE, f"{country}.png")
    if not os.path.exists(path):
        return None
    src = Image.open(path).convert("RGBA")
    _, _, right, bottom = CLOTH
    left, top = CLOTH[0], CLOTH[1]
    cw, ch = right - left, bottom - top

    def paint(d, w, h):
        d._image.paste(src.resize((w, h), Image.LANCZOS), (0, 0))
    return paint


def main():
    os.makedirs(OUT, exist_ok=True)
    drafted = []
    for country, painter in FLAGS.items():
        wiki = from_wiki(country)
        if wiki is None:
            drafted.append(country)
        flag(wiki or painter).save(os.path.join(OUT, f"flag_{country}.png"))
    print(f"wrote {len(FLAGS)} flags to {OUT}")
    print("no picture on the wiki (drawn stand-in):", ", ".join(sorted(drafted)))


if __name__ == "__main__":
    main()
