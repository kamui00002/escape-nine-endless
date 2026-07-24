#!/bin/bash
# ☁️ make-video.sh — そらもよう 動画 1 コマンドビルダー
# 使い方:
#   1. シミュレータでアプリ起動 (Xcode Run)
#   2. ./make-video.sh record  # 録画開始 (15秒、自動停止)
#   3. ./make-video.sh edit    # video-use 編集 (Claude 経由を促す)
#   4. ./make-video.sh convert # 4 アスペクト比に変換
#   5. ./make-video.sh all     # record 以外を一気に

set -uo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
PV="$DIR/promo-video"
APP_NAME="escape"

case "${1:-help}" in
    record)
        echo "📹 シミュレータで録画開始..."
        echo "(Ctrl+C で停止 / 15 秒経過で自動停止)"
        mkdir -p "$PV/01_recordings"
        OUTFILE="$PV/01_recordings/raw_$(date +%Y%m%d-%H%M%S).mp4"
        # 15 秒で自動停止 (perl alarm)
        perl -e 'alarm shift; exec @ARGV' 15 xcrun simctl io booted recordVideo "$OUTFILE" || true
        echo "✅ 録画完了: $OUTFILE"
        ;;

    edit)
        echo "🎬 動画編集は Claude に頼んでください:"
        echo ""
        echo "  Claude に: \"$PV/01_recordings/ にある最新動画を video-use で 15 秒の紹介動画に編集して。"
        echo "  字幕は $DIR/copy.md の日本語版通り。"
        echo "  カラーグレード: 低階層は cool、高階層は warm な緊張感。"
        echo "  出力先: $PV/02_edited/edited.mp4\""
        echo ""
        ;;

    bgm)
        echo "🎵 BGM 生成は Claude に頼んでください:"
        echo ""
        cat "$DIR/copy.md" | sed -n '/^## 🎵/,/^---/p' | head -15
        echo ""
        echo "  生成された .mp3 を $PV/bgm/${APP_NAME}_bgm.mp3 に保存"
        ;;

    convert)
        SRC="$PV/02_edited/edited.mp4"
        BGM="$PV/bgm/${APP_NAME}_bgm.mp3"
        OUT_BASE="$PV/03_final"

        if [ ! -f "$SRC" ]; then
            echo "❌ $SRC が見つかりません。先に edit ステップを完了してください"
            exit 1
        fi

        if [ -f "$BGM" ]; then
            echo "🎵 BGM ミックス中..."
            ffmpeg -y -i "$SRC" -i "$BGM" -c:v copy -c:a aac -shortest "$PV/02_edited/edited_with_bgm.mp4" 2>&1 | tail -3
            WITH_BGM="$PV/02_edited/edited_with_bgm.mp4"
        else
            echo "⚠️ BGM なし、動画のみで進めます (後で BGM を追加可能)"
            WITH_BGM="$SRC"
        fi

        echo "📐 4 アスペクト比に変換中..."

        # 9:16
        ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/9_16/${APP_NAME}_9x16.mp4" 2>&1 | tail -1
        # 1:1
        ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1080:force_original_aspect_ratio=decrease,pad=1080:1080:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/1_1/${APP_NAME}_1x1.mp4" 2>&1 | tail -1
        # 16:9
        ffmpeg -y -i "$WITH_BGM" -vf "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/16_9/${APP_NAME}_16x9.mp4" 2>&1 | tail -1
        # 4:5
        ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1350:force_original_aspect_ratio=decrease,pad=1080:1350:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/4_5/${APP_NAME}_4x5.mp4" 2>&1 | tail -1

        echo ""
        echo "✅ 完成!"
        ls -la "$OUT_BASE"/*/${APP_NAME}_*.mp4 2>/dev/null
        ;;

    all)
        echo "🚀 動画ワークフロー全実行 (record は別途実行してね)"
        $0 edit
        echo ""
        $0 bgm
        echo ""
        echo "↑ 上記を Claude に依頼後、改めて './make-video.sh convert' を実行"
        ;;

    help|*)
        cat <<EOF
☁️ make-video.sh — Escape Nine 動画ビルダー

Usage:
  ./make-video.sh record   # シミュレータで 15 秒録画
  ./make-video.sh edit     # Claude に編集依頼 (説明文表示)
  ./make-video.sh bgm      # BGM 生成プロンプト表示
  ./make-video.sh convert  # 4 アスペクト比に変換 (BGM 追加含む)
  ./make-video.sh all      # edit / bgm / 説明だけ表示
  ./make-video.sh help     # このヘルプ

Files:
  copy.md          # マーケコピー全部
  promo-video/     # 作業ディレクトリ
    01_recordings/ # 生録画
    02_edited/     # 編集後
    03_final/      # 4 アスペクト比
    bgm/           # BGM
EOF
        ;;
esac
