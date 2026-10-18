"""Generate logo card for promo video ending."""
from PIL import Image, ImageDraw, ImageFont
import os

OUT = "/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/marketing/promo-video/02_edited/logo_card.png"
ICON = "/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/marketing/screenshot-gen/public/app-icon.png"

W, H = 1080, 1920
BG = (15, 15, 30)
GOLD = (255, 215, 0)
WHITE = (245, 245, 245)
ACCENT = (255, 107, 53)

img = Image.new("RGB", (W, H), BG)

# Gradient overlay (subtle radial)
overlay = Image.new("RGB", (W, H), BG)
draw_ov = ImageDraw.Draw(overlay)
for r in range(800, 0, -20):
    alpha = max(0, 60 - r // 20)
    color = (15 + alpha // 3, 51 - alpha // 6, 96 - alpha // 4)
    draw_ov.ellipse((W // 2 - r, H // 2 - r * 2, W // 2 + r, H // 2 + r * 2), fill=color)
img = Image.blend(img, overlay, 0.3)

# Icon: round corners
icon = Image.open(ICON).convert("RGB").resize((420, 420), Image.LANCZOS)
mask = Image.new("L", (420, 420), 0)
ImageDraw.Draw(mask).rounded_rectangle((0, 0, 420, 420), radius=90, fill=255)
icon_rgba = icon.convert("RGBA")
icon_rgba.putalpha(mask)
img.paste(icon_rgba, ((W - 420) // 2, 560), icon_rgba)

draw = ImageDraw.Draw(img)

# Fonts (fallback chain)
def load_font(size, bold=True):
    candidates = [
        "/System/Library/Fonts/Supplemental/Arial Black.ttf",
        "/System/Library/Fonts/Helvetica.ttc",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/System/Library/Fonts/SFNS.ttf",
    ]
    for p in candidates:
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                continue
    return ImageFont.load_default()

font_big = load_font(120)
font_small = load_font(48)

def draw_centered(text, y, font, fill):
    bbox = draw.textbbox((0, 0), text, font=font)
    w = bbox[2] - bbox[0]
    draw.text(((W - w) // 2, y), text, font=font, fill=fill)

draw_centered("ESCAPE NINE", 1080, font_big, GOLD)
draw_centered("Endless Dungeon", 1240, font_small, WHITE)

img.save(OUT)
print(f"saved: {OUT}")
print(f"size: {os.path.getsize(OUT)} bytes")
