"""
Pre-generate all folder icon .ico files so they are bundled with the app.
Run this on any OS (Linux/Mac/Windows) to produce the icons directory.
"""
from PIL import Image, ImageDraw, ImageFilter
import os, math

OUTPUT = os.path.join(os.path.dirname(__file__), "icons")
os.makedirs(OUTPUT, exist_ok=True)

SIZES = [16, 32, 48, 64, 128, 256]

# ── colour palette ────────────────────────────────────────────────────────────
COLORS = {
    "yellow":   "#F5C518",
    "blue":     "#4A90D9",
    "green":    "#27AE60",
    "red":      "#E74C3C",
    "purple":   "#8E44AD",
    "orange":   "#E67E22",
    "pink":     "#FF69B4",
    "teal":     "#1ABC9C",
    "gray":     "#7F8C8D",
    "brown":    "#795548",
    "white":    "#F0F0F0",
    "black":    "#2C2C2C",
}

# ── texture overlays ──────────────────────────────────────────────────────────
TEXTURES = {
    "gradient", "striped", "dots", "carbon", "wood", "metallic",
    "neon_blue", "neon_green", "neon_pink",
}


def hex_to_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))


def darken(rgb, factor=0.65):
    return tuple(int(c * factor) for c in rgb)


def draw_folder_shape(draw, w, h, fill, shadow_color):
    """Draw a clean Windows-style folder on a canvas of size w×h."""
    tab_w  = w * 0.40
    tab_h  = h * 0.12
    body_y = h * 0.18

    # shadow
    draw.rounded_rectangle([2, body_y + 2, w - 2, h - 1], radius=w * 0.06,
                            fill=shadow_color)
    # tab
    draw.polygon([(0, body_y),
                  (tab_w, body_y),
                  (tab_w + w * 0.06, body_y - tab_h),
                  (0, body_y - tab_h)],
                 fill=fill)
    # body
    draw.rounded_rectangle([0, body_y, w, h - 2], radius=w * 0.06, fill=fill)


def folder_image(size, base_color_rgb, overlay_fn=None):
    W = H = size
    img  = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    shadow = darken(base_color_rgb, 0.55)
    draw_folder_shape(draw, W, H, base_color_rgb, shadow)

    if overlay_fn:
        overlay = overlay_fn(W, H, base_color_rgb)
        img = Image.alpha_composite(img, overlay)

    # subtle highlight strip at top of body
    hi = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    hd = ImageDraw.Draw(hi)
    body_y = H * 0.18
    hd.rounded_rectangle([0, body_y, W, body_y + H * 0.18],
                          radius=W * 0.06, fill=(255, 255, 255, 40))
    img = Image.alpha_composite(img, hi)
    return img


# ── overlay factories ─────────────────────────────────────────────────────────

def ov_gradient(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    body_y = int(H * 0.18)
    for y in range(body_y, H):
        alpha = int(60 * (y - body_y) / (H - body_y))
        d.line([(0, y), (W, y)], fill=(0, 0, 0, alpha))
    return ol

def ov_striped(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    for y in range(0, H, max(2, H // 12)):
        d.line([(0, y), (W, y)], fill=(255, 255, 255, 30))
    return ol

def ov_dots(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    spacing = max(4, W // 8)
    r = max(1, W // 24)
    for x in range(spacing, W, spacing):
        for y in range(spacing, H, spacing):
            d.ellipse([x-r, y-r, x+r, y+r], fill=(255, 255, 255, 55))
    return ol

def ov_carbon(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    cell = max(3, W // 10)
    for x in range(0, W, cell * 2):
        for y in range(0, H, cell * 2):
            d.rectangle([x, y, x+cell-1, y+cell-1], fill=(0, 0, 0, 60))
            d.rectangle([x+cell, y+cell, x+2*cell-1, y+2*cell-1], fill=(0, 0, 0, 60))
            d.rectangle([x+cell, y, x+2*cell-1, y+cell-1], fill=(255, 255, 255, 20))
            d.rectangle([x, y+cell, x+cell-1, y+2*cell-1], fill=(255, 255, 255, 20))
    return ol

def ov_wood(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    grain = max(3, H // 10)
    for i, y in enumerate(range(0, H, grain)):
        alpha = 40 if i % 2 == 0 else 10
        d.rectangle([0, y, W, y + grain], fill=(80, 40, 0, alpha))
    return ol

def ov_metallic(W, H, base):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    for i in range(6):
        y = int(H * i / 6)
        alpha = [0, 40, 80, 50, 20, 0][i]
        d.rectangle([0, y, W, y + H // 6], fill=(255, 255, 255, alpha))
    return ol

def _neon(W, H, base, neon_rgb):
    ol = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d  = ImageDraw.Draw(ol)
    body_y = int(H * 0.18)
    # glow border
    thick = max(2, W // 20)
    d.rounded_rectangle([0, body_y, W, H - 2], radius=W * 0.06,
                         outline=(*neon_rgb, 200), width=thick)
    d.rounded_rectangle([thick, body_y + thick, W - thick, H - 2 - thick],
                         radius=W * 0.06, fill=(*neon_rgb, 20))
    return ol

def ov_neon_blue(W, H, base):  return _neon(W, H, base, (0, 200, 255))
def ov_neon_green(W, H, base): return _neon(W, H, base, (0, 255, 100))
def ov_neon_pink(W, H, base):  return _neon(W, H, base, (255, 0, 200))


OVERLAY_MAP = {
    "gradient": ov_gradient,  "striped":  ov_striped,
    "dots":     ov_dots,       "carbon":   ov_carbon,
    "wood":     ov_wood,       "metallic": ov_metallic,
    "neon_blue":ov_neon_blue,  "neon_green":ov_neon_green,
    "neon_pink":ov_neon_pink,
}

# ── generate ──────────────────────────────────────────────────────────────────

def make_ico(name, base_rgb, overlay_fn=None):
    frames = [folder_image(s, base_rgb, overlay_fn) for s in SIZES]
    path = os.path.join(OUTPUT, f"{name}.ico")
    frames[0].save(path, format="ICO",
                   sizes=[(s, s) for s in SIZES],
                   append_images=frames[1:])
    print(f"  ✓ {name}.ico")

print("Generating colour icons …")
for cname, chex in COLORS.items():
    make_ico(cname, hex_to_rgb(chex))

print("Generating texture icons …")
BASE_TAN = hex_to_rgb("#D4A843")
for tname, tfn in OVERLAY_MAP.items():
    make_ico(tname, BASE_TAN, tfn)

# also make colour+gradient combos for the nice previews
print("Generating colour+gradient combos …")
for cname, chex in COLORS.items():
    make_ico(f"{cname}_gradient", hex_to_rgb(chex), ov_gradient)

print(f"\nDone! Icons saved to: {OUTPUT}")
