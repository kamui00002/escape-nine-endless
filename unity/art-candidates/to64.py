#!/usr/bin/env python3
"""Convert a raw magenta-background sprite to a 64x64 transparent PNG.

Pipeline:
  1. Corner flood-fill to remove the magenta background (tolerant, connected-only
     so interior purples/pinks survive).
  2. Halo despill: trim thin magenta-dominant fringe touching transparency.
  3. Crop to content bbox.
  4. Downscale to fit 64x64 (mode = nearest | lanczos), bottom-aligned + centered.
Also writes a 512px upscaled preview (nearest) for visual inspection.
"""
import sys
from collections import deque
from PIL import Image

MAGENTA = (255, 0, 255)


def is_strict_magenta(px):
    """Near-pure magenta only. Safe to remove anywhere (interior pockets):
    fails for purple robes (R low), blue orbs (R low), pink flowers (G high)."""
    r, g, b = px[0], px[1], px[2]
    return r > 205 and b > 205 and g < 100 and abs(r - b) < 50


def is_magenta(px, tol):
    r, g, b = px[0], px[1], px[2]
    # magenta: high R, low G, high B, R~=B. Distinguishes from purple(low R) / pink(high G).
    return (r > 200 and b > 200 and g < 110 and abs(r - b) < 55) or \
           ((r - MAGENTA[0]) ** 2 + (g - MAGENTA[1]) ** 2 + (b - MAGENTA[2]) ** 2) < tol * tol


def flood_key(im, tol=95):
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    visited = bytearray(w * h)
    q = deque()
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    # add whole top/bottom edges as seeds too (character rarely touches corners)
    for x in range(0, w, 4):
        seeds.append((x, 0)); seeds.append((x, h - 1))
    for y in range(0, h, 4):
        seeds.append((0, y)); seeds.append((w - 1, y))
    for sx, sy in seeds:
        if not visited[sy * w + sx] and is_magenta(px[sx, sy], tol):
            visited[sy * w + sx] = 1
            q.append((sx, sy))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not visited[ny * w + nx]:
                if is_magenta(px[nx, ny], tol):
                    visited[ny * w + nx] = 1
                    q.append((nx, ny))
    for i in range(w * h):
        if visited[i]:
            x, y = i % w, i // w
            r, g, b, _ = px[x, y]
            px[x, y] = (r, g, b, 0)
    # second pass: kill enclosed near-pure-magenta pockets flood-fill couldn't reach
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a and is_strict_magenta((r, g, b)):
                px[x, y] = (r, g, b, 0)
    return im


def despill(im, iterations=2):
    px = im.load()
    w, h = im.size
    for _ in range(iterations):
        to_clear = []
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                if a == 0:
                    continue
                # magenta-dominant fringe pixel?
                if r > 170 and b > 170 and g < 130 and abs(r - b) < 60:
                    # touching transparency?
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] == 0:
                            to_clear.append((x, y)); break
        if not to_clear:
            break
        for x, y in to_clear:
            r, g, b, _ = px[x, y]
            px[x, y] = (r, g, b, 0)
    return im


def to_64(im, mode="lanczos"):
    bbox = im.getbbox()
    im = im.crop(bbox)
    w, h = im.size
    scale = min(56 / w, 56 / h)  # leave a 4px margin inside 64
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    resample = Image.NEAREST if mode == "nearest" else Image.LANCZOS
    small = im.resize((nw, nh), resample)
    canvas = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    ox = (64 - nw) // 2
    oy = 64 - nh - 3  # bottom-aligned, 3px baseline margin
    canvas.paste(small, (ox, oy), small)
    return canvas


def main():
    name = sys.argv[1]
    mode = sys.argv[2] if len(sys.argv) > 2 else "lanczos"
    src = f"raw/{name}.png"
    im = Image.open(src)
    im = flood_key(im)
    im = despill(im)
    out = to_64(im, mode)
    out.save(f"64/{name}.png")
    # preview upscaled 8x with nearest for inspection
    out.resize((512, 512), Image.NEAREST).save(f"64/_preview_{name}_{mode}.png")
    # report opaque pixel count
    px = out.load()
    opaque = sum(1 for y in range(64) for x in range(64) if px[x, y][3] > 0)
    print(f"{name} [{mode}] -> 64/{name}.png  opaque={opaque}/4096")


if __name__ == "__main__":
    main()
