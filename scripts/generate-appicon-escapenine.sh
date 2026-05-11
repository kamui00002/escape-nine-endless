#!/usr/bin/env bash
# generate-appicon-escapenine.sh
#
# EscapeNine 専用 AppIcon 生成 wrapper
#
# 背景色を Deep Brown #2A1F18 (会議録第14回ブランドガイド) で固定し、
# EscapeNine の AppIcon.appiconset に直接配置。
#
# 使い方:
#   ./scripts/generate-appicon-escapenine.sh <input.png>
#
# 例:
#   ./scripts/generate-appicon-escapenine.sh ~/Downloads/escape-nine-icon-1024.png

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
INPUT="${1:?Usage: $0 <input.png>}"

# EscapeNine の AppIcon.appiconset 場所 (固定)
TARGET_DIR="$REPO_ROOT/EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/AppIcon.appiconset"

if [ ! -d "$TARGET_DIR" ]; then
  echo "❌ Error: AppIcon.appiconset not found at: $TARGET_DIR" >&2
  echo "   Run from EscapeNine repo root, or check repo structure." >&2
  exit 1
fi

# Deep Brown #2A1F18 (会議録第14回 田村ブランドガイド)
ICON_BG="#2A1F18" "$SCRIPT_DIR/generate-appicon.sh" "$INPUT" "$TARGET_DIR"

echo ""
echo "🎮 EscapeNine AppIcon updated."
echo "   Verify: open EscapeNine-endless-/EscapeNine-endless-.xcodeproj"
echo "   Build:  ⌘B"
