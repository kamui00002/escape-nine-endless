#!/usr/bin/env python3
"""
Ad image generator for Escape Nine: Endless Meta campaign.
Requires: pip install google-generativeai pillow
Requires: GEMINI_API_KEY with billing enabled (free tier has 0 image quota)

Usage:
  export GEMINI_API_KEY=your_key_here
  python3 ads/generate-images.py
"""

import os
import sys
import json
import base64
import urllib.request
import urllib.error

API_KEY = os.environ.get("GEMINI_API_KEY", "")
if not API_KEY:
    # Try reading from ~/.claude.json
    try:
        import pathlib
        cfg = json.loads(pathlib.Path.home().joinpath(".claude.json").read_text())
        API_KEY = cfg["mcpServers"]["nano-banana"]["env"].get("GEMINI_API_KEY", "")
    except Exception:
        pass

if not API_KEY:
    print("ERROR: GEMINI_API_KEY not set")
    sys.exit(1)

MODEL = "gemini-2.5-flash-image"

BRIEFS = [
    {
        "concept": 1,
        "filename": "feed-1080x1080-v1.png",
        "out_dir": "ad-assets/meta/concept-1",
        "aspect": "1:1",
        "prompt": (
            "#1A1A2E dark background, #E94560 red accent neon glow, "
            "3x3 pixel art grid game board, glowing red enemy piece pursuing fleeing player piece "
            "across dark grid, abstract glowing BPM indicator light '140', stage '35' glowing, "
            "neon-lit retro digital gaming arena, centered composition 20% margins all sides, "
            "cinematic low-key lighting, high energy intense pixel art style, "
            "no readable text, no UI labels, speed thrill excitement addictive atmosphere, square format"
        ),
    },
    {
        "concept": 2,
        "filename": "stories-1080x1920-v1.png",
        "out_dir": "ad-assets/meta/concept-2",
        "aspect": "9:16",
        "prompt": (
            "#1A1A2E dark background, electric blue and #E94560 red neon glow, "
            "3x3 pixel art grid game board centered composition, "
            "glowing neon countdown visual effect '3 2 1', abstract BPM 180 light pulse, "
            "retro pixel art mixed with modern neon aesthetics, intense dramatic mood, "
            "vertical poster format, middle 70% safe zone composition, "
            "no readable text, no labels, challenge competitive thrill atmosphere"
        ),
    },
    {
        "concept": 3,
        "filename": "feed-1080x1080-v2.png",
        "out_dir": "ad-assets/meta/concept-3",
        "aspect": "1:1",
        "prompt": (
            "#1A1A2E dark background, motion blur speed effect, #E94560 red panic glow, "
            "pixel art player piece barely escaping in 3x3 grid, "
            "red enemy piece closing in from corner, abstract BPM meter at 200 pulsing, "
            "dramatic red and dark color palette, adrenaline-rush visual, "
            "centered composition 20% margins, "
            "no readable text, no labels, panic thrill atmosphere, square format"
        ),
    },
]

BASE_URL = f"https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent?key={API_KEY}"


def generate(brief):
    payload = json.dumps({
        "contents": [{"parts": [{"text": brief["prompt"]}]}],
        "generationConfig": {"responseModalities": ["IMAGE", "TEXT"]},
    }).encode()

    req = urllib.request.Request(BASE_URL, data=payload, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = json.loads(resp.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        print(f"  HTTP {e.code}: {body[:300]}")
        return False

    parts = data.get("candidates", [{}])[0].get("content", {}).get("parts", [])
    for p in parts:
        if "inlineData" in p:
            img_bytes = base64.b64decode(p["inlineData"]["data"])
            out_path = os.path.join(brief["out_dir"], brief["filename"])
            os.makedirs(brief["out_dir"], exist_ok=True)
            with open(out_path, "wb") as f:
                f.write(img_bytes)
            print(f"  Saved: {out_path} ({len(img_bytes):,} bytes)")
            return True
        elif "text" in p:
            print(f"  Model text: {p['text'][:100]}")
    print(f"  No image in response: {json.dumps(data)[:300]}")
    return False


def main():
    print(f"Generating {len(BRIEFS)} images with {MODEL}...")
    print("Billing must be enabled — free tier quota is 0 for image generation.\n")
    results = []
    for b in BRIEFS:
        print(f"Brief {b['concept']}: {b['filename']}")
        ok = generate(b)
        results.append(ok)
        print()

    success = sum(results)
    print(f"Done: {success}/{len(BRIEFS)} succeeded")
    if success == len(BRIEFS):
        write_manifest()


def write_manifest():
    manifest = {
        "generated": "2026-05-01",
        "model": MODEL,
        "assets": [
            {
                "concept": b["concept"],
                "path": os.path.join(b["out_dir"], b["filename"]),
                "dimensions": "1080x1080" if b["aspect"] == "1:1" else "1080x1920",
                "platform": "meta",
            }
            for b in BRIEFS
        ],
    }
    out = "ad-assets/generation-manifest.json"
    with open(out, "w") as f:
        json.dump(manifest, f, indent=2)
    print(f"Manifest: {out}")


if __name__ == "__main__":
    main()
