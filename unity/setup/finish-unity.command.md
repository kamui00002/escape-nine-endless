---
description: ultracode で Escape Nine の Unity 移行を出荷可能まで完成させる（Fable 5 推奨）
argument-hint: "[開始Phase番号 例: 0]"
---

ultracode で Escape Nine の Unity 移行を「出荷可能」状態まで完成させて。あなたはリードエンジニア兼オーケストレータ。
（引数があればその Phase から再開: $ARGUMENTS。無ければ Phase 0 から）

# ゴール
docs/unity-migration-plan.md に沿い、unity/ の足場から Unity 版を実装しきる。
最終形 = iOS + Android + Steam体験版 の3プラットフォーム / 機能パリティ + ゲームフィール + ローグライク深化。

# 正本（必ず先に読む・グラウンディング）
- プラン: docs/unity-migration-plan.md（Phase 0〜7 とゲート）
- 立ち上げ: unity/setup/RUNBOOK.md（bootstrap / MCP / Phase0）
- 移植の正本: EscapeNine-endless-/EscapeNine-endless-/ 配下の Swift（挙動はこれに一致させる）
- 移植済C#: unity/EscapeNine/Assets/Scripts/（Core は移植済。壊さず拡張）
- バランス典拠: docs/game-spec.md（BPM表・AI階層・特殊ルール）
- フェーズ別プロンプト詳細: unity/setup/ULTRACODE_PROMPT.md

# 進め方（ultracode / ワークフロー）
- Phase 0→7 を「1 Phase = 1 ワークフロー」で順に実行。各 Phase の間で結果を報告し、私の確認を待つ。
- Phase 内の独立モジュールは fan-out（並列エージェント）。
- 移植は必ず: Swiftの正本を読む → C#へ移植 → EditModeテストでSwiftと同一入出力を検証。
  検証は敵対的に（別エージェントが Swift↔C# の差分・欠落を探し、CONFIRMED のみ採用）。
- テスト/ビルドは実際に走らせて結果で判断する:
  ・bootstrap: bash unity/setup/bootstrap.sh
  ・テスト: Unity -runTests -batchmode -projectPath <P> -testPlatform editmode -testResults <xml>
  ・シーン/ビルド: Unity -batchmode -quit -projectPath <P> -executeMethod <Class.Method>
- 各 Phase 完了時に必ず報告: 変更点 / テスト結果(pass/fail数) / 未解決 / 次Phaseの前提。

# 絶対ルール（誠実性）
- 未検証を「完成」と言わない。テスト未実行・ビルド未確認はそう明記。
- 次の「人間ゲート」に到達したら停止して私に確認（勝手に進めない/偽の完了報告をしない）:
  1. Phase 0 リズム精度 GO/NO-GO（iOS実機の体感が必要）
  2. 外部SDKの資格情報（Firebase/AdMob/Facebook/IAP商品ID — 既存値の再利用可否）
  3. 課金・広告の実機/実アカウント検証
  4. App Store / Google Play / Steam への提出
- バランス定数・BPM曲線・AI挙動は Swift と1:1（game-spec.md の表と一致）。
- 既存の移植済C#とテストを壊さない（回帰したら直す）。

# 完了条件（Definition of Done）
- 全 EditMode/PlayMode テスト green
- iOS/Android で 起動→数階層プレイ→GameOver→リトライ が動作
- Steam(PC)ビルドが起動
- 収益化（IAP/広告/ランキング）が結線済み（実アカウント検証は人間ゲート）
- 各 Phase の状態を docs に反映（unity-migration-plan.md 更新）

まず開始 Phase（既定 Phase 0）から: bootstrap を実行 → EditMode全pass確認 → 該当Phaseの成果物を実装/ビルド →
人間ゲートに来たら停止して私に確認を求めて。以降は Phase を1つずつ進める。
