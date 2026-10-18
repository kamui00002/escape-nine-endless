#!/bin/bash
# ☁️ make-screenshots.sh — Escape Nine App Store スクショ 1 コマンドビルダー
#
# 既存 generator なし → /app-store-screenshots skill を Claude で起動して新規作成
# このスクリプトは撮影と skill 起動の補助に特化

set -uo pipefail

MARKETING_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$MARKETING_DIR/.." && pwd)"
STAGING_DIR="$MARKETING_DIR/raw-screenshots"

# 撮影するシーン
SCENES=("home" "gameplay_low" "gameplay_high" "result" "leaderboard" "settings")

case "${1:-help}" in
    capture)
        SCENE="${2:-}"
        if [ -z "$SCENE" ]; then
            echo "❌ シーン名を指定してください: $0 capture <home|gameplay_low|gameplay_high|result|leaderboard|settings>"
            exit 1
        fi
        mkdir -p "$STAGING_DIR"
        OUTFILE="$STAGING_DIR/${SCENE}.png"
        xcrun simctl io booted screenshot "$OUTFILE"
        echo "✅ 撮影完了: $OUTFILE"
        ;;

    capture-all)
        echo "📸 6 シーンを順番に撮影します (Escape Nine)"
        echo ""
        mkdir -p "$STAGING_DIR"
        for SCENE in "${SCENES[@]}"; do
            echo "🎬 シミュレータで [$SCENE] 画面に遷移してください"
            case "$SCENE" in
                home)              echo "  → タイトル / メイン画面" ;;
                gameplay_low)      echo "  → 低階層プレイ中 (BPM 70-100)" ;;
                gameplay_high)     echo "  → 高階層プレイ中 (BPM 180+) " ;;
                result)            echo "  → 階層クリア / リザルト画面" ;;
                leaderboard)       echo "  → ランキング / Game Center" ;;
                settings)          echo "  → 設定画面 / ストア" ;;
            esac
            read -p "  準備できたら Enter (skip するなら 's' + Enter): " input
            if [ "$input" = "s" ]; then
                echo "  ⏭ skip: $SCENE"
                continue
            fi
            OUTFILE="$STAGING_DIR/${SCENE}.png"
            xcrun simctl io booted screenshot "$OUTFILE"
            echo "  ✅ 撮影: $OUTFILE"
            echo ""
        done
        echo "🎉 全シーン撮影完了"
        echo ""
        echo "次のステップ:"
        echo "  ./make-screenshots.sh gen   # /app-store-screenshots skill で generator 作成"
        ;;

    gen)
        cat <<EOF
🎨 generator を作成するには Claude にこう頼んでください:

「$(pwd)/raw-screenshots/ に撮影済の Escape Nine スクショがあります。
\`/app-store-screenshots\` skill を起動して、Keeplet スタイル
(参考: ~/.claude/skills/app-store-screenshots/references/keeplet-style.png)
の App Store スクショ generator を新規作成してください。

設定:
- App icon: $(pwd)/../../iOS の App Icon を流用
- Brand color: #FF6B35 (accent) + 暗い宇宙系背景
- Headline / Subhead / Feature bullets: $(pwd)/copy.md 参照
- 出力先: $(pwd)/screenshot-gen/
- 5 スライド (copy.md の Slide 1-5)」

Claude が必要な質問をして、Next.js プロジェクトを自動生成します。
EOF
        ;;

    list-screenshots)
        echo "=== 現在の撮影済スクショ ==="
        ls -la "$STAGING_DIR" 2>/dev/null
        ;;

    help|*)
        cat <<EOF
☁️ make-screenshots.sh — Escape Nine App Store スクショビルダー

Usage:
  ./make-screenshots.sh capture <scene>    # 1 シーン撮影
  ./make-screenshots.sh capture-all        # 6 シーンを順番に撮影
  ./make-screenshots.sh gen                # /app-store-screenshots skill 起動指示
  ./make-screenshots.sh list-screenshots   # 撮影済一覧

シーン:
  home / gameplay_low / gameplay_high / result / leaderboard / settings

Files:
  copy.md            # マーケコピー (Slide 1-5 全部)
  raw-screenshots/   # 撮影画像
  screenshot-gen/    # (後で skill が生成する Next.js generator)
EOF
        ;;
esac
