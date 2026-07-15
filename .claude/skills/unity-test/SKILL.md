---
name: unity-test
description: Escape Nine Unity 版のテストを turnkey 実行する ☀️。①dotnet test (Core、Editor不要・高速) ②Unity CLI EditMode (全テスト) ③結果XMLの fail 抽出。Use when Unity の .cs を変更した後、コミット前、「Unityテスト回して」「EditMode 回して」と言われた時。
---

# unity-test — Unity テスト実行 ☀️

## どちらを走らせるか（判断基準）

| 変更範囲 | 実行 |
|---|---|
| `Scripts/Core/` のみ | **Stage 1 だけで十分**（CI `unity-core-tests.yml` と同一） |
| `Scripts/Runtime/`・`Editor/` を含む | Stage 1 → **Stage 2 必須**（`PlayerStateTests` / `RankingStoreTests` は PlayerPrefs 依存で Stage 2 でしか走らない） |
| 台帳・ドキュメントのみ | 実行不要 |

## Stage 1: dotnet test（Core、〜30秒）

```bash
# この Mac では dotnet が PATH に無い (~/.dotnet/dotnet が実体) のでフォールバック付きで呼ぶ
DOTNET=$(command -v dotnet || echo "$HOME/.dotnet/dotnet")
cd "$(git rev-parse --show-toplevel)/unity/verify/Core.Tests" && "$DOTNET" test --nologo
```

- Unity Editor 不要。C# 9 固定コンパイルの検証を兼ねる（C# 10+ 構文はここで落ちる）
- 実測 (2026-07-15): 162/162 green・約 1 秒（初回ビルド除く）
- fail したらこの時点で修正。Stage 2 に進まない

## Stage 2: Unity CLI EditMode テスト（全テスト、数分）

```bash
# Unity バイナリ検出（bootstrap.sh と同じ流儀）
UNITY_BIN="/Applications/Unity/Hub/Editor/$(ls /Applications/Unity/Hub/Editor | sort -V | tail -1)/Unity.app/Contents/MacOS/Unity"
RESULT_XML=/tmp/editmode-results-$(date +%H%M%S).xml

"$UNITY_BIN" -batchmode -runTests \
  -projectPath ~/EscapeNineUnity \
  -testPlatform editmode \
  -testResults "$RESULT_XML" \
  -logFile /tmp/editmode-run.log
echo "exit=$?"
```

注意:
- **Unity Editor で同プロジェクトを開いていると起動に失敗する**（先に閉じてもらう）
- `~/EscapeNineUnity` の .cs が repo と同期済みか先に確認: `diff -rq <repo>/unity/EscapeNine/Assets/Scripts ~/EscapeNineUnity/Assets/Scripts | grep -v '\.meta'`（差分があれば `bash unity/setup/bootstrap.sh` で同期）
- `-runTests` は `-quit` 不要（付けるとテストが走らないことがある）

## Stage 3: 結果 XML の集計

```bash
# XML パーサ不使用 (XXE 回避 + 依存ゼロ)。NUnit3 形式の属性を正規表現で抽出する
python3 - "$RESULT_XML" <<'PY'
import re, sys
xml = open(sys.argv[1], encoding="utf-8", errors="replace").read()
m = re.search(r'<test-run\b[^>]*?\btotal="(\d+)"[^>]*?\bpassed="(\d+)"[^>]*?\bfailed="(\d+)"', xml)
print(f"total={m[1]} passed={m[2]} failed={m[3]}" if m else "test-run サマリが見つからない (ログ /tmp/editmode-run.log を確認)")
for tc in re.finditer(r'<test-case\b[^>]*?\bresult="Failed"[^>]*?>', xml):
    name = re.search(r'\bfullname="([^"]+)"', tc[0])
    print("FAIL:", name[1] if name else tc[0][:200])
PY
```

## 報告ルール

- pass/fail 数を必ず数字で報告（「たぶん通る」「実行していないが問題ないはず」は禁止 — finish-unity.md の誠実性ルール）
- fail 時はテスト名 + メッセージを引用して修正 → 再実行
