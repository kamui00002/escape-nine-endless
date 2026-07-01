#!/usr/bin/env bash
#
# `/finish-unity` スラッシュコマンドをローカルに設置する。
# このリポジトリは .claude/ を gitignore しているため、コマンド本体は
# unity/setup/finish-unity.command.md に追跡し、本スクリプトでローカルの
# .claude/commands/ にコピーする（gitignore の方針を尊重）。
#
# 使い方 (repo ルートで):
#   bash unity/setup/install-slash-command.sh
# その後 Claude Code で /finish-unity が使える。
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC="$SCRIPT_DIR/finish-unity.command.md"
DEST_DIR="$REPO_ROOT/.claude/commands"
DEST="$DEST_DIR/finish-unity.md"

[[ -f "$SRC" ]] || { echo "[install] ソースが見つかりません: $SRC" >&2; exit 1; }
mkdir -p "$DEST_DIR"
cp "$SRC" "$DEST"
echo "[install] 設置完了: $DEST"
echo "[install] Claude Code で /finish-unity が使えます（モデルは Fable 5 推奨）。"
