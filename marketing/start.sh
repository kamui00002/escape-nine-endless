#!/bin/bash
# ☁️ start.sh — Escape Nine 動画・スクショ作成セッション開始スクリプト

set -uo pipefail

MARKETING_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$MARKETING_DIR/.." && pwd)"

cd "$PROJECT_ROOT" || exit 1

echo "═══════════════════════════════════════"
echo "⚡  Escape Nine: Endless 動画+スクショ作成"
echo "═══════════════════════════════════════"
echo ""

# Git 状態
echo "📋 Git 状態"
BRANCH=$(git branch --show-current)
echo "  Branch: $BRANCH"
if [ "$BRANCH" = "main" ]; then
    echo "  ⚠️  main にいます。作業ブランチに切り替えてください:"
    echo "      git checkout marketing/2026-05-19-promo"
fi
echo ""

# シミュレータ状態
echo "📱 シミュレータ"
BOOTED=$(xcrun simctl list devices booted 2>&1 | grep -c Booted || true)
if [ "$BOOTED" -gt 0 ]; then
    echo "  ✅ Booted simulator あり"
    xcrun simctl list devices booted 2>&1 | grep Booted | head -1
else
    echo "  ⚠️  シミュレータ未起動。Xcode で Run してください"
fi
echo ""

# ツール
echo "🛠  必要ツール"
for tool in ffmpeg xcrun; do
    if command -v $tool >/dev/null 2>&1; then
        echo "  ✅ $tool"
    else
        echo "  ❌ $tool が見つかりません"
    fi
done
echo ""

# 既存 BGM
echo "🎵 既存 BGM (流用候補)"
BGM_DIR="$PROJECT_ROOT/EscapeNine-endless-/EscapeNine-endless-/Sounds/BGM"
if [ -d "$BGM_DIR" ]; then
    ls "$BGM_DIR"/*.mp3 2>/dev/null | head -3 | sed 's|^|  |'
fi
echo ""

# ファイル状態
echo "📁 marketing/ 配下"
ls "$MARKETING_DIR"/{copy.md,make-video.sh,make-screenshots.sh,promo-video/README.md} 2>/dev/null | sed 's|^|  ✅ |'
echo ""

cat <<EOF
═══════════════════════════════════════
🚀 次のステップ (順番に実行)

  cd $MARKETING_DIR
  cat copy.md                      # コピー全部確認

  # === 動画作成 ===
  ./make-video.sh record           # 15 秒録画 (シミュでゲームプレイ)
  ./make-video.sh edit             # Claude 編集依頼
  cp $BGM_DIR/bgm_battle.mp3 promo-video/bgm/escape_bgm.mp3  # BGM 流用
  ./make-video.sh convert          # 4 アスペクト比生成

  # === スクショ作成 ===
  ./make-screenshots.sh capture-all  # 6 シーン順次撮影
                                       (home, gameplay_low, gameplay_high,
                                        result, leaderboard, settings)
  ./make-screenshots.sh gen          # Claude に generator 作成依頼を表示

⏱  目安: 20 分
🆘  詰まったら Discord に「escape 詰まった」と送信

═══════════════════════════════════════
EOF
