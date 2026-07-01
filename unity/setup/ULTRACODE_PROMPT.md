# Escape Nine — Unity 完成 ultracode プロンプト集 ⭐️

**用途**: Fable 5 到来後、ローカル Mac (Ghostty) の Claude Code で **ultracode（複数エージェント/ワークフロー）**を使い、Unity 版を一気に完成へ進めるためのプロンプト。
**前提**: `unity/setup/RUNBOOK.md` の bootstrap 済み or 同時実行。正本は `docs/unity-migration-plan.md`。

> ⚡️ **クイック起動 `/finish-unity`**: 本リポジトリは `.claude/` を gitignore しているため、コマンド本体は
> `unity/setup/finish-unity.command.md` に追跡してある。ローカルで一度だけ設置スクリプトを実行:
> ```bash
> bash unity/setup/install-slash-command.sh   # → .claude/commands/finish-unity.md を作成
> ```
> 以後 Claude Code で **`/finish-unity`**（引数で開始Phase指定可: `/finish-unity 3`）でマスタープロンプトが発火する。
> ultracode を効かせるため、プロンプトに **`ultracode`** の語を必ず含めること（本文に含めてある）。
> モデルは **Fable 5** を選択（`/model` で切替）。大規模に回すなら先頭に予算指定 `+800k` 等を付ける。

---

## ⚠️ 最初に：ultracode でも自動化できないもの（人間ゲート）

「一気に完成」でも、以下は**性質上ローカルの人間 or 実機/実アカウントが必要**。プロンプトはここで**必ず停止して確認を求める**設計にしてある（勝手に進めない/でっち上げない）:

1. **Phase 0 リズム精度 GO/NO-GO** — iOS 実機の体感評価
2. **外部SDKの資格情報** — Firebase / AdMob / Facebook / IAP 商品ID（既存値の再利用可否）
3. **課金・広告の実機/実アカウント検証** — Sandbox/実機
4. **App Store / Google Play / Steam への提出** — 審査・ストア設定

これらは「未完了」として明示され、あなたの入力待ちで止まる。それ以外は自走する。

---

## 1. マスター・ゴールプロンプト（`/finish-unity` の中身）

```
ultracode で Escape Nine の Unity 移行を「出荷可能」状態まで完成させて。あなたはリードエンジニア兼オーケストレータ。

# ゴール
docs/unity-migration-plan.md に沿い、unity/ の足場から Unity 版を実装しきる。
最終形 = iOS + Android + Steam体験版 の3プラットフォーム / 機能パリティ + ゲームフィール + ローグライク深化。

# 正本（必ず先に読む・グラウンディング）
- プラン: docs/unity-migration-plan.md（Phase 0〜7 とゲート）
- 立ち上げ: unity/setup/RUNBOOK.md（bootstrap / MCP / Phase0）
- 移植の正本: EscapeNine-endless-/EscapeNine-endless-/ 配下の Swift（挙動はこれに一致させる）
- 移植済C#: unity/EscapeNine/Assets/Scripts/（Core は移植済。壊さず拡張）
- バランス典拠: docs/game-spec.md（BPM表・AI階層・特殊ルール）

# 進め方（ultracode / ワークフロー）
- Phase 0→7 を「1 Phase = 1 ワークフロー」で順に実行。私は各 Phase の間で結果を読む。
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
- 上記「人間ゲート」(Phase0 GO/NO-GO・SDK資格情報・課金/広告実検証・ストア提出)に到達したら
  停止して私に確認。勝手に進めず、偽の完了報告をしない。
- バランス定数・BPM曲線・AI挙動は Swift と1:1（game-spec.md の表と一致）。
- 既存の移植済C#とテストを壊さない（回帰したら直す）。

# 完了条件（Definition of Done）
- 全 EditMode/PlayMode テスト green
- iOS/Android で 起動→数階層プレイ→GameOver→リトライ が動作
- Steam(PC)ビルドが起動
- 収益化（IAP/広告/ランキング）が結線済み（実アカウント検証は人間ゲート）
- 各 Phase の状態を docs に反映（unity-migration-plan.md 更新）

まず Phase 0 から: RUNBOOK §2 の bootstrap を実行 → EditMode全pass確認 → Phase0シーンをビルド →
リズム精度検証の準備が整ったら私に GO/NO-GO を求めて。以降は Phase を1つずつ進める。
```

---

## 2. フェーズ別プロンプト（1つずつ回したい場合）

マスターで自走させず、フェーズ単位で握りたいときはこちらを個別に貼る。各ブロック冒頭に `ultracode` を含めてある。

### Phase 0 — リズム同期プロトタイプ（ゲート）
```
ultracode で Phase 0 を実行。unity/setup/RUNBOOK.md §2 の bootstrap.sh を走らせ、
EditModeテスト(FloorTests/GameEngineTests/AIEngineTests)が全passすることを確認。
Phase0SceneBuilder でシーン生成 → iOS実機ビルド手順を用意。
リズム精度の GO/NO-GO は私が実機で判断するので、検証準備が整ったら停止して指示を仰いで。
Conductor(dspTime)の scheduleAheadSeconds / firstBeatOffsetSeconds の調整余地も提示して。
```

### Phase 1 — コアロジック移植の残り
```
ultracode で Phase 1 の残ロジックを Swift 正本から C# へ忠実移植。対象:
- ターン進行/逃げ切り判定（ViewModels/GameViewModel.swift のゲームループ部分）
- デイリーチャレンジのシード生成（Services/DailyChallengeService.swift の LCG・UTC日付）
- Wordle風シェア文字列（Views/Components/ShareSheet.swift の ShareTextBuilder）
- 特殊ルール適用（霧/消失の段階ロジック、Constants.disappearCellStages）
各モジュールを fan-out で移植 → EditModeテストを新規作成し Swift と同一入出力を敵対的に検証。
既存 Core/テストを壊さないこと。完了時に pass/fail 数を報告。
```

### Phase 2 — UI/UX 再構築
```
ultracode で Phase 2。Swift の各 View を Unity UI へ再構築（3×3盤面・Home・Result・Ranking・
Shop・Character・Settings・Tutorial）。移植済 GameEngine/Floor/AIEngine への結線が中心。
iPad/iPhone 比率レイアウト原則（CLAUDE.md）を CanvasScaler+SafeArea で再現。
MCP 経由で Editor にシーン/プレハブを構築。各画面ができたら PlayMode で起動確認して報告。
```

### Phase 3 — 収益化・サービス再統合
```
ultracode で Phase 3。Unity IAP(4商品 wizard/elf/knight/removeads) / 広告(LevelPlay or AdMob Unity) /
Firebase Unity(匿名Auth+Firestore+Analytics) / PostHog(REST, 既存イベント名維持) /
Game Center + Google Play Games を結線。商品ID・Firestoreスキーマ・イベント名は既存を流用。
資格情報が要る箇所は停止して私に確認（勝手にダミー値で進めない）。実アカウント検証も人間ゲート。
```

### Phase 4 — ゲームフィール（juice）
```
ultracode で Phase 4。ヒットストップ/画面シェイク/パーティクル/コンボVFX/BPM反応の背景演出/
キャラアニメ/発射台型GameOver強化 を実装。Reduce Motion 対応も入れる。
短尺動画映えする15秒デモが撮れる状態を目標に、PlayModeで確認して報告。
```

### Phase 5 — ローグライク深化
```
ultracode で Phase 5。レリック/パーク選択・メタ進行(永続アンロック)・分岐ルート・
ボスパターン を実装（docs/unity-migration-plan.md §4 の優先度順）。
バランスは Swift 由来の定数を基準に、ヘッドレスGameLoopシミュレータで多数試行して検証。
```

### Phase 6 — マルチプラットフォーム
```
ultracode で Phase 6。Android(Google Play Games/Play Billing) と Steam(PC)体験版のビルドを通す。
ランキングは Firestore を正、GC/GPG は表示連携に。各プラットフォームで起動確認。
ストア提出は人間ゲートなので、提出直前まで整えて停止。
```

### Phase 7 — リリース/移行
```
ultracode で Phase 7。iOS既存アプリの Unity版アップデート差し替え準備（新規アプリにしない）。
メタデータは docs/appstore-metadata.md を流用。提出チェックリストを生成し、
実提出は私が行うので直前で停止。
```

---

## 3. 運用のコツ

- **予算スケール**: 大規模フェーズは先頭に `+800k` 等の予算指定を付けると、ultracode が探索・検証を厚くする。
- **1フェーズずつ**が安全。マスターで全自走も可能だが、Phase境界で結果を読んでから次へ、が事故が少ない。
- **敵対的検証を信頼**: 「移植できた」ではなく「SwiftとC#の差分をレビューし CONFIRMED ゼロ」を完了基準に。
- **中断/再開**: フェーズ単位で PR を切ると、失敗時の巻き戻しが楽（Sprint運用と同じ流儀）。

---

## 4. 関連
- 親プラン: `../../docs/unity-migration-plan.md`
- 立ち上げ手順: `./RUNBOOK.md`
- 足場の説明: `../README.md`
- スラッシュコマンド本体（追跡）: `./finish-unity.command.md`
- コマンド設置スクリプト: `./install-slash-command.sh`（`.claude/` は gitignore のため）
