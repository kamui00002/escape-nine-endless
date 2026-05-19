"""Generate subtitle PNGs (transparent BG) for overlay — fantasy style."""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os

OUT_DIR = "/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/marketing/promo-video/02_edited/subtitles"
os.makedirs(OUT_DIR, exist_ok=True)

# Mincho (serif/明朝) for fantasy feel, W6 (bold)
FONT_JP = "/System/Library/Fonts/ヒラギノ明朝 ProN.ttc"

# Brand palette (docs/game-spec.md)
COL_GOLD = (255, 215, 0)         # #FFD700
COL_AMBER = (218, 165, 32)       # #DAA520 (mid stop for gradient)
COL_DARK = (44, 24, 16)          # #2C1810 outline
COL_GLOW = (255, 140, 0)         # outer glow

texts = [
    "ビートに合わせて",
    "9マスで逃げ切れ",
    "加速する鼓動",
    "100階層、その先へ",
    "Escape Nine: Endless",
]

SIZE = 86
OUTLINE = 6
GLOW = 14
PAD = 60
W_CANVAS = 1080


def vertical_gradient_text(text, font, size_top, size_bot):
    """Render text twice and mask-blend to vertical gradient."""
    bbox = font.getbbox(text)
    text_w = bbox[2] - bbox[0]
    text_h = bbox[3] - bbox[1]
    asc, desc = font.getmetrics()
    line_h = asc + desc

    # Render with top color, mask the bottom half with bot color
    img_top = Image.new("RGBA", (text_w + 4, line_h + 4), (0, 0, 0, 0))
    img_bot = Image.new("RGBA", (text_w + 4, line_h + 4), (0, 0, 0, 0))
    ImageDraw.Draw(img_top).text((-bbox[0], 0), text, font=font, fill=size_top + (255,))
    ImageDraw.Draw(img_bot).text((-bbox[0], 0), text, font=font, fill=size_bot + (255,))

    # Gradient mask (white at top fading to black at bottom)
    grad = Image.new("L", img_top.size, 0)
    for y in range(grad.height):
        v = int(255 * (1 - y / grad.height))
        ImageDraw.Draw(grad).line([(0, y), (grad.width, y)], fill=v)

    img_top.putalpha(grad)
    out = Image.alpha_composite(img_bot, img_top)
    return out


for i, text in enumerate(texts, start=1):
    font = ImageFont.truetype(FONT_JP, SIZE)

    bbox = font.getbbox(text)
    text_w = bbox[2] - bbox[0]
    asc, desc = font.getmetrics()
    line_h = asc + desc

    canvas_h = line_h + PAD * 2 + (OUTLINE + GLOW) * 2

    img = Image.new("RGBA", (W_CANVAS, canvas_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    x = (W_CANVAS - text_w) // 2 - bbox[0]
    y = PAD + GLOW

    # 1) Outer glow (orange, blurred)
    glow_layer = Image.new("RGBA", (W_CANVAS, canvas_h), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow_layer)
    for dx in range(-GLOW, GLOW + 1):
        for dy in range(-GLOW, GLOW + 1):
            if dx * dx + dy * dy <= GLOW * GLOW:
                glow_draw.text((x + dx, y + dy), text, font=font, fill=COL_GLOW + (40,))
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(radius=6))
    img = Image.alpha_composite(img, glow_layer)

    # 2) Dark outline (thick, multi-pass)
    outline_layer = Image.new("RGBA", (W_CANVAS, canvas_h), (0, 0, 0, 0))
    outline_draw = ImageDraw.Draw(outline_layer)
    for dx in range(-OUTLINE, OUTLINE + 1):
        for dy in range(-OUTLINE, OUTLINE + 1):
            if dx * dx + dy * dy <= OUTLINE * OUTLINE:
                outline_draw.text((x + dx, y + dy), text, font=font, fill=COL_DARK + (255,))
    img = Image.alpha_composite(img, outline_layer)

    # 3) Gold-to-amber gradient fill
    grad_text = vertical_gradient_text(text, font, COL_GOLD, COL_AMBER)
    img.alpha_composite(grad_text, (x + bbox[0], y))

    path = os.path.join(OUT_DIR, f"sub_{i}.png")
    img.save(path)
    print(f"sub_{i}: {W_CANVAS}x{canvas_h} '{text}'")

print("done")
