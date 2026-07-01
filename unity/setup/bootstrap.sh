#!/usr/bin/env bash
#
# Escape Nine — Unity 完全自動ブートストラップ
# ------------------------------------------------------------
# ローカル Mac の Claude Code (Ghostty) から実行する想定。
# 手作業のインポート/クリック無しで、以下を一気通貫で実行する:
#   1. Unity Editor バイナリを自動検出
#   2. 空プロジェクトを -createProject で生成 (既存ならスキップ)
#   3. 本 repo の C# 足場 (Assets/) をプロジェクトにコピー
#   4. EditMode テストを batchmode で実行 → 忠実移植を一次検証
#   5. Phase 0 シーンを executeMethod でプログラム生成
#
# MCP のセットアップは RUNBOOK.md §3 を参照 (対話フェーズ用、本スクリプトの範囲外)。
#
# 使い方:
#   bash unity/setup/bootstrap.sh
# 環境変数で上書き可:
#   PROJECT_PATH=~/EscapeNineUnity  UNITY_BIN=/path/to/Unity  bash unity/setup/bootstrap.sh
#
set -euo pipefail

# ---- パス解決 ----
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SCAFFOLD_ASSETS="$REPO_ROOT/unity/EscapeNine/Assets"
PROJECT_PATH="${PROJECT_PATH:-$HOME/EscapeNineUnity}"
RESULTS_XML="${RESULTS_XML:-$PROJECT_PATH/editmode-results.xml}"

log() { printf '\033[1;36m[bootstrap]\033[0m %s\n' "$*"; }
err() { printf '\033[1;31m[bootstrap:ERROR]\033[0m %s\n' "$*" >&2; }

# ---- 1. Unity Editor バイナリ検出 ----
detect_unity() {
  if [[ -n "${UNITY_BIN:-}" && -x "$UNITY_BIN" ]]; then
    echo "$UNITY_BIN"; return 0
  fi
  # Unity Hub 既定インストール先 (macOS)。バージョン降順で最新を採用。
  local hub_dir="/Applications/Unity/Hub/Editor"
  if [[ -d "$hub_dir" ]]; then
    local latest
    latest="$(ls -1 "$hub_dir" 2>/dev/null | sort -Vr | head -n1 || true)"
    if [[ -n "$latest" ]]; then
      local bin="$hub_dir/$latest/Unity.app/Contents/MacOS/Unity"
      [[ -x "$bin" ]] && { echo "$bin"; return 0; }
    fi
  fi
  # 単体インストール先
  local single="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
  [[ -x "$single" ]] && { echo "$single"; return 0; }
  return 1
}

UNITY_BIN="$(detect_unity || true)"
if [[ -z "$UNITY_BIN" ]]; then
  err "Unity Editor が見つかりません。UNITY_BIN=/path/to/Unity を指定して再実行してください。"
  err "例: UNITY_BIN='/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity'"
  exit 1
fi
log "Unity: $UNITY_BIN"
log "Project: $PROJECT_PATH"

run_unity() {
  # -logFile - で標準出力にログを流す
  "$UNITY_BIN" "$@" -logFile - 2>&1
}

# ---- 2. プロジェクト生成 ----
if [[ -d "$PROJECT_PATH/Assets" ]]; then
  log "既存プロジェクトを検出。作成をスキップ: $PROJECT_PATH"
else
  log "空プロジェクトを作成中..."
  CREATE_LOG="$(run_unity -batchmode -quit -createProject "$PROJECT_PATH" || true)"
  echo "$CREATE_LOG" | tail -n 5
  if [[ ! -d "$PROJECT_PATH/Assets" ]]; then
    if echo "$CREATE_LOG" | grep -q "No valid Unity Editor license"; then
      err "Unity ライセンスが未アクティベートです (batchmode はライセンス必須)。修復手順:"
      err "  1. Unity Hub を開いてサインイン"
      err "  2. 設定(歯車) → ライセンス → 『Add license』→ 無料の Personal ライセンスを取得"
      err "  3. 本スクリプトを再実行"
      err "  それでも失敗する場合: Hub から一度 Editor を GUI で起動してから再実行。"
      err "  最終手段: Hub GUI で $PROJECT_PATH に 2D プロジェクトを手動作成 → 再実行 (作成はスキップされ続行します)"
    fi
    err "プロジェクト作成に失敗しました。"
    exit 1
  fi
  log "プロジェクト作成完了"
fi

# ---- 3. 足場コピー ----
log "C# 足場をコピー中..."
mkdir -p "$PROJECT_PATH/Assets"
# rsync があれば差分コピー、無ければ cp -R
if command -v rsync >/dev/null 2>&1; then
  rsync -a "$SCAFFOLD_ASSETS"/ "$PROJECT_PATH/Assets"/
else
  cp -R "$SCAFFOLD_ASSETS"/. "$PROJECT_PATH/Assets"/
fi
log "コピー完了 (.meta は Editor が初回 import 時に自動生成)"

# ---- 4. EditMode テスト ----
log "EditMode テストを実行中 (batchmode)..."
set +e
run_unity -runTests -batchmode -projectPath "$PROJECT_PATH" \
  -testPlatform editmode -testResults "$RESULTS_XML" | tail -n 30
TEST_EXIT=$?
set -e
if [[ -f "$RESULTS_XML" ]]; then
  # NUnit XML の集計 (python3 があれば詳細表示)
  if command -v python3 >/dev/null 2>&1; then
    python3 - "$RESULTS_XML" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
tc = root.get('total') or root.get('testcasecount')
failed = root.get('failed') or '0'
passed = root.get('passed') or '?'
print(f"[bootstrap] EditMode results: total={tc} passed={passed} failed={failed}")
sys.exit(1 if (failed not in ('0', None)) else 0)
PY
    PY_EXIT=$?
    [[ $PY_EXIT -ne 0 ]] && err "テストに失敗があります。$RESULTS_XML を確認してください。"
  else
    log "結果ファイル: $RESULTS_XML (集計は python3 未検出のため省略)"
  fi
else
  err "テスト結果 XML が生成されませんでした (Test Framework 未導入の可能性)。RUNBOOK §トラブルシュート参照。"
fi
log "Unity -runTests exit code: $TEST_EXIT"

# ---- 5. Phase 0 シーン生成 ----
log "Phase 0 シーンをプログラム生成中..."
run_unity -batchmode -quit -projectPath "$PROJECT_PATH" \
  -executeMethod EscapeNine.EditorTools.Phase0SceneBuilder.Build | tail -n 15 || \
  err "シーン生成でエラー。ログを確認してください (Editor コンパイルエラーの可能性)。"

log "完了。次: RUNBOOK.md §3 (MCP設定) → §4 (Phase 0 実機検証)"
log "  Editor を開く: \"$UNITY_BIN\" -projectPath \"$PROJECT_PATH\""
