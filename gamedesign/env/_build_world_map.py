"""Vẽ bản đồ vùng có chú thích từ gamedesign/env/world/world_top.jpg + world_map.txt.

    python gamedesign/env/_build_world_map.py
"""
from PIL import Image, ImageDraw, ImageFont
import os

ROOT = os.path.join(os.path.dirname(__file__), "world")
img = Image.open(os.path.join(ROOT, "world_top.jpg")).convert("RGB")
W, H = img.size
d = ImageDraw.Draw(img, "RGBA")


def font(size):
    for f in ("C:/Windows/Fonts/segoeuib.ttf", "C:/Windows/Fonts/arialbd.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


big, mid = font(38), font(26)
px = lambda u, v: (float(u) * W, (1 - float(v)) * H)

rows = [l.rstrip("\n").split("|") for l in open(os.path.join(ROOT, "world_map.txt"), encoding="utf-8")][1:]
for r in rows:
    if r[0] == "road":
        d.line([px(r[2], r[3]), px(r[4], r[5])], fill=(240, 220, 170, 170), width=4)
river = [px(r[2], r[3]) for r in rows if r[0] == "river"]
d.line(river, fill=(90, 170, 230, 200), width=6)

colors = {"village": (255, 210, 120), "temple": (255, 120, 90), "shrine": (255, 160, 200), "outpost": (190, 200, 255)}
icons = {"village": "◆", "temple": "卍", "shrine": "⛩", "outpost": "▲"}


def label(x, y, text, f, fill):
    for dx, dy in ((-2, 0), (2, 0), (0, -2), (0, 2)):
        d.text((x + dx, y + dy), text, font=f, fill=(0, 0, 0, 220), anchor="mm")
    d.text((x, y), text, font=f, fill=fill, anchor="mm")


for r in rows:
    if r[0] == "region":
        x, y = px(r[2], r[3])
        label(x, y, r[1].upper(), big, (255, 255, 255, 235))
for r in rows:
    if r[0] in colors:
        x, y = px(r[2], r[3])
        d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=colors[r[0]] + (255,), outline=(0, 0, 0, 255), width=3)
        label(x, y + 34, r[1], mid, colors[r[0]] + (255,))

legend = [("Làng", "village"), ("Chùa", "temple"), ("Đền", "shrine"), ("Trạm", "outpost")]
d.rectangle([30, H - 60 - 44 * len(legend), 300, H - 30], fill=(0, 0, 0, 150))
for i, (name, k) in enumerate(legend):
    y = H - 60 - 44 * (len(legend) - 1 - i) - 10
    d.ellipse([50, y - 12, 74, y + 12], fill=colors[k] + (255,), outline=(0, 0, 0, 255), width=3)
    d.text((90, y), name, font=mid, fill=(255, 255, 255, 255), anchor="lm")
d.line([(W - 330, H - 50), (W - 330 + W * 500 / 2048, H - 50)], fill=(255, 255, 255, 255), width=6)
d.text((W - 330, H - 85), "500 m · bắc ↑", font=mid, fill=(255, 255, 255, 255))
img.save(os.path.join(ROOT, "world_map_labeled.jpg"), quality=90)
print("ok", os.path.join(ROOT, "world_map_labeled.jpg"))
