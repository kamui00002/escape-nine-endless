#!/usr/bin/env python3
"""Build a self-contained comparison.html (old vs new sprites, base64 embedded)."""
import base64, io, datetime
from PIL import Image
import to64  # reuse flood_key / despill for high-res transparent previews

OLD_DIR = "/Users/yoshidometoru/Documents/GitHub/escape-nine-endless/unity/EscapeNine/Assets/Resources/Sprites"
NAMES = [
    ("hero", "勇者 / Hero", "青+金"),
    ("thief", "盗賊 / Thief", "緑+茶"),
    ("wizard", "魔法使い / Wizard", "紫+青玉"),
    ("knight", "ナイト / Knight", "銀+金+赤房"),
    ("elf", "エルフ / Elf", "緑+銀髪"),
    ("red_oni", "赤鬼 / Red Oni", "赤+金角"),
    ("blue_oni", "青鬼 / Blue Oni", "青+銀角"),
    ("skeleton", "骸骨 / Skeleton", "白骨+紫"),
    ("dragon", "ドラゴン / Dragon", "赤+金+炎"),
]


def b64(im):
    buf = io.BytesIO()
    im.save(buf, format="PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def thumb(im, box):
    im = im.convert("RGBA")
    bb = im.getbbox()
    if bb:
        im = im.crop(bb)
    s = min(box / im.width, box / im.height)
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)


def board_preview():
    """3x3 board sim: new 64 sprites on brown tiles."""
    tile = 120
    gap = 6
    bg = (44, 24, 16, 255)
    W = tile * 3 + gap * 4
    board = Image.new("RGBA", (W, W), bg)
    tcols = [(64, 40, 26, 255), (54, 32, 20, 255)]
    for i, (n, _, _) in enumerate(NAMES):
        r, c = divmod(i, 3)
        ox = gap + c * (tile + gap)
        oy = gap + r * (tile + gap)
        cell = Image.new("RGBA", (tile, tile), tcols[(r + c) % 2])
        board.alpha_composite(cell, (ox, oy))
        sp = Image.open(f"64/{n}.png").convert("RGBA").resize((104, 104), Image.NEAREST)
        board.alpha_composite(sp, (ox + (tile - 104) // 2, oy + (tile - 104) // 2))
    return board


def new_hires(n):
    """High-res transparent preview from the 1408px raw (apples-to-apples with old)."""
    im = to64.flood_key(Image.open(f"raw/{n}.png"))
    im = to64.despill(im)
    return thumb(im, 220)


rows = ""
for n, label, ident in NAMES:
    old = Image.open(f"{OLD_DIR}/{n}.png")
    old_u = b64(thumb(old, 220))
    new_u = b64(new_hires(n))
    chip = b64(Image.open(f"64/{n}.png"))
    rows += f"""
    <tr>
      <th scope="row"><span class="cname">{label}</span><br><span class="ident">{ident}</span></th>
      <td><img class="pix new" src="{old_u}" alt="old {n}"></td>
      <td><img class="pix new" src="{new_u}" alt="new {n}"></td>
      <td class="chipcell"><img class="chip" src="{chip}" alt="new {n} 64px"><br><span class="cap">64&times;64 実寸(タイル)</span></td>
    </tr>"""

board_b64 = b64(board_preview())
now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")

html = f"""<!doctype html>
<html lang="ja">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Escape Nine スプライト刷新 比較シート</title>
<style>
  :root {{ color-scheme: light dark; }}
  * {{ box-sizing: border-box; }}
  body {{ font-family: -apple-system, BlinkMacSystemFont, "Helvetica Neue", sans-serif;
         max-width: 1080px; margin: 0 auto; padding: 24px; line-height: 1.6;
         background: #14100c; color: #f0e6d8; }}
  h1 {{ color: #ffd700; border-bottom: 2px solid #daa520; padding-bottom: 8px; font-size: 1.5rem; }}
  h2 {{ color: #f4a460; margin-top: 40px; }}
  .meta {{ color: #b8a894; font-size: .9rem; }}
  .pix {{ image-rendering: pixelated; image-rendering: crisp-edges; }}
  table {{ border-collapse: collapse; width: 100%; margin: 16px 0; }}
  th, td {{ border: 1px solid #3a2c20; padding: 10px; text-align: center; vertical-align: middle; }}
  thead th {{ background: #2c1810; color: #ffd700; position: sticky; top: 0; }}
  tbody th {{ background: #201812; text-align: left; width: 130px; }}
  .cname {{ font-weight: 700; color: #f5deb3; }}
  .ident {{ font-size: .78rem; color: #90ee90; }}
  td img.new, td img.pix {{ max-width: 100%; height: auto; }}
  .new {{ background:
      repeating-conic-gradient(#2a2622 0% 25%, #221e1a 0% 50%) 0 / 24px 24px;
      border-radius: 6px; }}
  .chip {{ width: 64px; height: 64px; image-rendering: pixelated;
      background: repeating-conic-gradient(#2a2622 0% 25%, #221e1a 0% 50%) 0 / 16px 16px; }}
  .cap {{ font-size: .72rem; color: #b8a894; }}
  .board {{ display: block; margin: 12px auto; max-width: 100%; height: auto;
      border: 2px solid #daa520; border-radius: 8px; image-rendering: pixelated; }}
  .legend {{ background: #201812; border-left: 4px solid #daa520; padding: 12px 16px;
      border-radius: 4px; font-size: .92rem; }}
  .legend code {{ color: #ffd700; }}
  @media (max-width: 640px) {{ tbody th {{ width: 90px; font-size: .8rem; }} }}
</style>
</head>
<body>
  <h1>Escape Nine スプライト刷新 比較シート</h1>
  <p class="meta">生成日時: {now}　/　9体を単一アンカー(勇者)からの style-transfer で生成し、統一。<br>
  変換: Pillow LANCZOS で 1408px → 64&times;64、マゼンタ(#FF00FF)背景をコーナー flood-fill + 内部ポケット除去でアルファ透過化。</p>

  <div class="legend">
    <strong>統一した5軸:</strong> 頭身 (約3.5頭身のチビ体型) ・ 輪郭線 (均一な1px チャコール) ・
    セルシェード (2〜3階調) ・ ライティング (左上キー光 + 右リム光) ・ パレット (温かい絵本調)。<br>
    <strong>各キャラは識別色を強調:</strong> 表の右端の色メモ参照。
  </div>

  <h2>旧 → 新 比較（各キャラ）</h2>
  <table>
    <caption class="meta" style="text-align:left;margin-bottom:8px">左: 現行スプライト（原寸を縮小・高解像） / 中: 新スプライト（生成原寸1408pxを透過処理し縮小・高解像） / 右: 実際に盤面で使う 64&times;64 実寸</caption>
    <thead>
      <tr><th scope="col">キャラ</th><th scope="col">旧 (現行)</th><th scope="col">新 (刷新案)</th><th scope="col">実寸</th></tr>
    </thead>
    <tbody>{rows}
    </tbody>
  </table>

  <h2>3&times;3 盤面プレビュー（新スプライト）</h2>
  <p class="meta">実際のゲーム盤面（ダークブラウンのタイル）に並べた際の見え方シミュレーション。小サイズでのシルエット可読性を確認。</p>
  <img class="board" src="{board_b64}" alt="3x3 board preview with new sprites">

  <h2>差し替えについて</h2>
  <div class="legend">
    候補ファイル: <code>unity/art-candidates/raw/&lt;name&gt;.png</code> (原寸) と
    <code>unity/art-candidates/64/&lt;name&gt;.png</code> (盤面用 64px 透過)。<br>
    現行の <code>Resources/Sprites/*.png</code> は<strong>未変更</strong>。差し替え採否はオーナー判断。
  </div>
</body>
</html>"""

with open("comparison.html", "w") as f:
    f.write(html)
import os
print("comparison.html written,", round(os.path.getsize("comparison.html") / 1024), "KB")
