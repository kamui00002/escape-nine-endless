#!/usr/bin/env bash
# generate-appicon.sh
#
# 元画像から iOS AppIcon (Xcode 16+ AppIcon.appiconset) を生成するスクリプト
#
# 機能:
#   1. 余白を trim (ChatGPT/DALL-E 等で生成された画像の白パディングを除去)
#   2. 1024×1024 の square にパディング (背景色は ICON_BG で指定)
#   3. 透明部分を背景色で塗りつぶし (Apple HIG: no transparency)
#   4. 通常 / Dark / Tinted の 3 appearance 対応の Contents.json を生成
#
# 必要なもの:
#   brew install imagemagick
#
# 使い方:
#   ./scripts/generate-appicon.sh <input.png> [<output-dir>]
#
# 環境変数:
#   ICON_BG  背景色 (default: #2A1F18 = Deep Brown、会議録第14回ブランドガイド)
#
# 使用例 (EscapeNine):
#   ./scripts/generate-appicon.sh ~/Downloads/icon-source.png \
#     EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/AppIcon.appiconset
#
# 使用例 (ParkPedia、緑背景):
#   ICON_BG="#22C55E" ./scripts/generate-appicon.sh ~/Downloads/parkpedia-icon.png \
#     /path/to/parkpedia/Assets.xcassets/AppIcon.appiconset
#
# 注意:
#   - Xcode 16+ AppIcon.appiconset 形式 (universal + dark + tinted の 3 appearance)
#   - 現状は同じ画像を 3 appearance に使用、Dark Mode / Tinted Mode 専用版は
#     別途 Sprint 3 で対応 (scripts/generate-appicon-3mode.sh を参照)

set -euo pipefail

INPUT="${1:?Usage: $0 <input.png> [<output-dir>]}"
OUTPUT_DIR="${2:-./AppIcon.appiconset}"
SIZE=1024
BG_COLOR="${ICON_BG:-#2A1F18}"  # Deep Brown by default

# ── 入力検証 ──
if [ ! -f "$INPUT" ]; then
  echo "❌ Error: input file not found: $INPUT" >&2
  exit 1
fi

if ! command -v magick >/dev/null 2>&1; then
  echo "❌ Error: ImageMagick not installed." >&2
  echo "   Install: brew install imagemagick" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

# ── 一時ディレクトリ ──
TMP=$(mktemp -d)
trap "rm -rf $TMP" EXIT

echo "🎨 Generating AppIcon..."
echo "   Input:        $INPUT"
echo "   Output dir:   $OUTPUT_DIR"
echo "   Background:   $BG_COLOR"
echo "   Target size:  ${SIZE}x${SIZE}"
echo ""

# ── 1. trim 余白 → square 化 → 透明部分を塗りつぶし ──
magick "$INPUT" \
  -trim +repage \
  -background "$BG_COLOR" \
  -gravity center \
  -extent "${SIZE}x${SIZE}" \
  -alpha remove -alpha off \
  "$OUTPUT_DIR/AppIcon.png"

# ── 2. Contents.json 生成 (Xcode 16+ 3 appearance 対応) ──
cat > "$OUTPUT_DIR/Contents.json" <<'JSON'
{
  "images" : [
    {
      "filename" : "AppIcon.png",
      "idiom" : "universal",
      "platform" : "ios",
      "size" : "1024x1024"
    },
    {
      "appearances" : [
        {
          "appearance" : "luminosity",
          "value" : "dark"
        }
      ],
      "filename" : "AppIcon.png",
      "idiom" : "universal",
      "platform" : "ios",
      "size" : "1024x1024"
    },
    {
      "appearances" : [
        {
          "appearance" : "luminosity",
          "value" : "tinted"
        }
      ],
      "filename" : "AppIcon.png",
      "idiom" : "universal",
      "platform" : "ios",
      "size" : "1024x1024"
    }
  ],
  "info" : {
    "author" : "xcode",
    "version" : 1
  }
}
JSON

# ── 3. 結果確認 ──
RESULT_SIZE=$(magick identify -format "%wx%h" "$OUTPUT_DIR/AppIcon.png")
RESULT_BYTES=$(stat -f%z "$OUTPUT_DIR/AppIcon.png" 2>/dev/null || stat -c%s "$OUTPUT_DIR/AppIcon.png")
RESULT_KB=$((RESULT_BYTES / 1024))

echo "✅ Done!"
echo "   AppIcon.png:  $OUTPUT_DIR/AppIcon.png ($RESULT_SIZE, ${RESULT_KB}KB)"
echo "   Contents.json: $OUTPUT_DIR/Contents.json"
echo ""
echo "📱 Next steps:"
echo "   1. Xcode で Assets.xcassets を開く → AppIcon プレビュー確認"
echo "   2. ビルド (⌘B) → ホーム画面で確認"
echo "   3. 必要なら Dark Mode / Tinted Mode 個別版を Sprint 3 で対応"
