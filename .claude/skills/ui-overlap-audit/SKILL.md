---
name: ui-overlap-audit
description: Unity 版 UI の重なり・はみ出し監査 ☀️。Editor/OverlapAudit.cs を batchmode で実行し、結果テキストを前回と diff して回帰を検出する。Use when Screens/・UIFactory・Stage 系の UI を変更した後、「重なりチェックして」「UI 監査して」と言われた時。
---

# ui-overlap-audit — UI 重なり監査 ☀️

根拠: UI 重なり/はみ出しは本移植で反復した欠陥（overlap-audit-*.txt 6 世代、ボス拡大率の迷走 9105a5c→53e661d 撤回）。目視の前に機械監査を挟む。

## 実行

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/$(ls /Applications/Unity/Hub/Editor | sort -V | tail -1)/Unity.app/Contents/MacOS/Unity"

"$UNITY_BIN" -batchmode -quit \
  -projectPath ~/EscapeNineUnity \
  -executeMethod EscapeNine.EditorTools.OverlapAudit.RunAndLog \
  -logFile /tmp/overlap-audit-run.log
```

- エントリポイント: `Assets/Editor/OverlapAudit.cs` の `EscapeNine.EditorTools.OverlapAudit.RunAndLog`（MenuItem "EscapeNine/Overlap Audit" と同じ）
- 結果は `~/EscapeNineUnity/` 直下の overlap-audit 系テキストに出力される（`WriteResult` 参照）。ログは `/tmp/overlap-audit-run.log`
- Unity Editor で同プロジェクトを開いていると起動に失敗する

## 判定

1. 出力テキストの指摘件数を確認（0 件が green）
2. **前回結果と diff**: `diff <前回のoverlap-audit.txt> <今回>` — 新規に増えた指摘だけが回帰
3. 指摘があれば該当 Screen/Widget を修正 → 再実行。修正後は `/unity-test`（レイアウト定数の変更が Core テストに波及していないか）
4. 最終確認はスクショ目視（`-executeMethod` でのスクショ生成 or 実機/シミュレータ。iPad 相当の縦横比も確認 — iOS 版の iPad ルールに対応）

## やってはいけない

- 指摘を消すために要素を小さくして視認性を犠牲にする（ボス拡大率の迷走の教訓: 演出都合の拡大は盤面はみ出しと引き換えにしない。1.0 に戻した 53e661d が最終判断）
