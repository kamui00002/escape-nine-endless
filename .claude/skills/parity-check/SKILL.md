---
name: parity-check
description: iOS(Swift)↔Unity(C#) のパリティ監査 runbook。バランス定数・イベント名・ロジック対応ペアの差分を機械抽出+敵対的レビューで検出し、unity/PARITY_GAPS.md に反映する。Use when 大きめのゲームロジック変更後、リリース前、「パリティ確認して」「Swift と合ってるか見て」と言われた時。
---

# parity-check — Swift↔C# パリティ監査 ☀️

正本は Swift。対応表は `.claude/rules/swift-csharp-parity.md`、既知の意図的差分は `unity/PARITY_GAPS.md`。
実績: この手順の敵対的検証で CONFIRMED 差分 9 件（830e1a6）・機能矛盾 10 件（8d214d9）を検出。

## Step 1: 定数の機械 diff（Constants.swift ↔ GameConfig.cs）

両ファイルから `識別子 = 数値` を抽出して突合する:

```bash
REPO=$(git rev-parse --show-toplevel)
grep -oE '(let|static let) [a-zA-Z]+ *(: *[A-Za-z]+)? *= *[0-9.]+' \
  "$REPO/EscapeNine-endless-/EscapeNine-endless-/Utilities/Constants.swift" | sort
grep -oE 'const [a-z]+ [A-Za-z]+ *= *[0-9.]+[fd]?' \
  "$REPO/unity/EscapeNine/Assets/Scripts/Core/GameConfig.cs" | sort
```

camelCase↔PascalCase の名前対応を取り、**値の不一致は即 CONFIRMED**。

## Step 2: イベント名 diff

hook を手動実行するのが早い:

```bash
echo '{"tool_input":{"file_path":"'$REPO'/unity/EscapeNine/Assets/Scripts/Runtime/Analytics/AnalyticsService.cs"}}' \
  | CLAUDE_PROJECT_DIR=$REPO python3 "$REPO/.claude/hooks/check_analytics_parity.py"
```

無音なら一致（PARITY_GAPS.md の allowlist 込み）。

## Step 3: ロジックペアの敵対的レビュー

対応表の各ペア（特に GameViewModel.swift ↔ GameSession.cs + GameController.cs）について、**差分を見つけることを目的とした**サブエージェントを並列で出す:

- プロンプト骨子: 「Swift 正本 <path> と C# 移植 <path> を読み、挙動が食い違う箇所を探せ。疑いは CONFIRMED（コード引用つきで再現手順まで言える）と PLAUSIBLE に分類。CONFIRMED のみ報告」
- 重点観点（過去にズレた実績順）: ①拍/ターンのタイミング（ポーズ再開・GO直後・階層遷移）②永続化タイミング（即時 Save）③実績チェックの勝敗両パス ④スキル/レリックの適用順 ⑤タイブレーク順
- **CONFIRMED のみ採用**（validity フィルタの精神。PLAUSIBLE は捨てるか手動確認）

## Step 4: 結果の反映

- 直すもの → 両実装ペア修正 + EditMode テスト追加（コミットに `(Swift+C# 1:1)`）→ `/unity-test`
- 意図的に残すもの → `unity/PARITY_GAPS.md` §A に理由つきで追記
- 未移植と判明したもの → 同 §B に追記
