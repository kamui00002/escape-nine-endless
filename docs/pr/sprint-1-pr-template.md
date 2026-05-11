## 概要

26 名の専門家会議 (35 ラウンド) で 5 票獲得した最優先決議
**「Game Over 画面を離脱口から発射台に変える」** を中心に、
Sprint 1「緊急止血」フェーズ (Week 1-2) の基盤を整備しました。

ブランチ: `feature/sprint-1-foundation` → `main`

本 PR は Sprint 1 の基盤整備一式 (コード + ドキュメント) をまとめたもので、
Game Over 画面の刷新、Firebase Analytics の計装、ASO 多言語対応、
世界観テキスト、プレスリリース草案、リーガルチェックリストを含みます。

---

## 統計

- **8 atomic commits** (Conventional Commits 形式)
- **3,746 行追加 / 14 行削除** (15 ファイル変更)
- **12 ファイル新規**: コード 2 + ドキュメント 10
- **2 ファイル編集**: `GameViewModel.swift` / `ResultView.swift`

---

## 主要変更 (コード)

### Game Over 画面刷新 (commit `dd82342`)

26 名の専門家会議で **5 票獲得した最優先決議**。
Game Over 画面を「離脱口」から「次の挑戦への発射台」に転換する刷新。

| ファイル | 変更内容 |
|---|---|
| `ViewModels/GameViewModel.swift` | `nearMissDistance` / `elapsedSeconds` プロパティ追加、`chebyshevDistance(from:to:)` ヘルパー追加 |
| `Views/Result/ResultView.swift` | 惜しさメーター / 巨大リトライボタン (高さ 180) / 挑戦時間表示 / 自己ベスト誘発 UI / シェアボタン |
| `Views/Components/ShareSheet.swift` (新規) | `UIActivityViewController` ラッパー + Wordle 風 emoji グリッド生成ロジック |

設計意図:
- **惜しさメーター**: 敗北時に「あと N マスで生存」を可視化、再挑戦の動機を即時生成
- **巨大リトライ**: 「もう一度」を最も押しやすい位置・サイズに、離脱を抑止
- **シェアボタン**: Wordle 風の記号化シェアでウイルス係数を稼ぐ準備
- **自己ベスト誘発**: あと N 秒 / N マスで自己ベスト、を文言化

### Firebase Analytics 計装 (commit `29dba3c`)

| ファイル | 変更内容 |
|---|---|
| `Services/AnalyticsEvents.swift` (新規) | 5 カスタムイベント (`eg_*`) 定数 + `AnalyticsLogger` ラッパー |

計装イベント:
- `eg_game_start` — ゲーム開始 (mode/source パラメータ)
- `eg_game_end` — ゲーム終了 (result/elapsed_seconds/near_miss_distance/score)
- `eg_retry_tap` — リトライタップ (game_over_screen からの起点)
- `eg_share_tap` — シェアタップ (share_text_length パラメータ)
- `eg_tutorial_complete` — チュートリアル完了 (step 番号)

---

## ドキュメント追加

| パス | 行数 | 内容 |
|---|---|---|
| `.kiro/sprint-1-research.md` | 93 | 着手前の既存コード調査 (`/kiro:validate-gap` 結果) |
| `.github/ISSUE_TEMPLATE/sprint-1/` | ~800 | Sprint 1 タスク 5 個の Issue テンプレート (game-over-redesign / one-tap-retry / tutorial-3-screens / aso-quick-wins / firebase-analytics-kpi) |
| `docs/analytics/sprint-1-events.md` | ~200 | KPI 計測設計 + BigQuery クエリサンプル |
| `docs/aso/sprint-1-improvements.md` | 794 | ASO 改善 (4 言語完全対応: 日/英/簡中/繁中) |
| `docs/lore/text-package-v1.md` | 466 | 迷宮図書館世界観テキスト一式 (葛城担当) |
| `docs/pr/press-release-v1-draft.md` | 630 | 火消し開発者ストーリー型プレスリリース (Eva 担当) |
| `docs/legal/checklist-sprint-7.md` | 501 | 商標 / プライバシー / 利用規約 チェックリスト (Naomi 担当、Sprint 7 の事前準備) |

---

## Sprint 1 完了基準 (会議録 DoD)

- [x] Game Over 画面刷新 (5 票最優先決議)
- [x] ワンタップリトライ (実装済 / `@AppStorage` 永続化済)
- [x] チュートリアル 3 画面 (初回のみ、`@AppStorage hasSeenTutorial` ガード)
- [x] ASO クイック改善案 (4 言語、Mac で App Store Connect 反映)
- [x] Firebase Analytics で KPI 計測 (5 イベント計装済)
- [ ] TestFlight に v1.1 提出 ← 別 PR で対応 (Mac で `xcodebuild archive` + `altool`)
- [ ] KPI ダッシュボード稼働確認 ← Firebase コンソール側のタスク

---

## 開発手法

**Discord Bot (Claude Code Channels) で並列 subagent 7-8 体を dispatch**

[Anthropic 公式 Multi-agent coordination patterns](https://claude.com/blog/multi-agent-coordination-patterns) の Orchestrator-Subagent パターンを実演。

各 subagent に専用ファイルパスを割り当てて競合回避し、約 30 分で全タスクを完了。
詳細は Vault `2026-05-09 [作業ログ] EscapeNine Sprint 1 並列開発成果` 参照。

---

## レビュアーへの確認お願い事項

### 1. `pbxproj` への新規 Swift ファイル登録

以下の新規 Swift ファイルは Xcode プロジェクトへの登録が必要です:

- `Services/AnalyticsEvents.swift`
- `Views/Components/ShareSheet.swift`

→ Xcode で **Add Files to "EscapeNine-endless"** が必要 (CI ビルド時に「No such file」で検出されるはず)。

### 2. `GameView.swift` の `ResultView` 呼び出し更新

`ResultView` に新 4 引数を追加しました:

- `elapsedSeconds: Double`
- `nearMissDistance: Int`
- `playerPosition: GridPosition`
- `enemyPosition: GridPosition`

→ default 値で後方互換にしているが、本 PR で呼び出し側 (`GameView.swift`) も更新済 (commit に含まれる)。

### 3. 既存ロジックを壊していないか

以下の挙動が Sprint 1 変更で壊れていないか確認をお願いします:

- `AchievementPopupView` の表示タイミング
- `InterstitialAdPresenter` の表示条件 (Game Over → 広告 → ResultView の順)
- `DefeatReason` 別の表示文言 (timeout / cornered / caught)

### 4. アクセシビリティ

- 巨大リトライボタン (高さ 180) は VoiceOver で正しく読まれるか
- シェアボタンの `accessibilityLabel`
- 「あと 1 マスで生存」表示のスクリーンリーダー読み上げ

### 5. Haptic feedback の強度

- 勝利時: `heavy`
- 敗北時: `medium`
- リトライタップ: `heavy`
- シェアタップ: `light`

→ 過剰でないか、UX 的に違和感ないかご確認ください。

---

## テスト

- 既存 XCTest スイートが未変更で壊れていないことを確認 (CI 通過必須)
- 新規テスト追加 (Sprint 1 関連):
  - `EscapeNine-endless-Tests/Sprint1/GameViewModelSprint1Tests.swift` — `nearMissDistance` / `elapsedSeconds` / `chebyshevDistance` の検証
  - `EscapeNine-endless-Tests/Sprint1/AnalyticsEventsTests.swift` — イベント定数とパラメータ整合性
  - `EscapeNine-endless-Tests/Sprint1/ShareTextBuilderTests.swift` — Wordle 風 emoji グリッド生成

---

## 関連

- Vault: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- Vault: `2026-05-09 [作業ログ] EscapeNine Sprint 1 並列開発成果`
- Issue テンプレート: `.github/ISSUE_TEMPLATE/sprint-1/`
- 会議録 PDF: 元資料は Discord セッションで保存済
- 別ファイル (混同注意): `docs/pr/press-release-v1-draft.md` はプレスリリース草案、本 PR テンプレとは別物

---

## 次の Sprint 候補

- **Sprint 2 (Week 3-4) 「再挑戦の仕組み化」**:
  - デイリーチャレンジ機能拡張 (現状のシード固定 → 日付ベースシード + ランキング)
  - Wordle 風シェア完成 (Sprint 1 で土台、Sprint 2 で実運用)
  - 抜かれ通知 (CloudKit ベースのフレンド比較 PoC)
- ピース収集の素地 (Sprint 1 で表示のみ、機能化は Sprint 2 以降)
- 0 conv 計測修理タスク (別ブランチ、`docs/aso/sprint-1-improvements.md` の計測前提に依存)

---

## ultrareview 推奨

```bash
/ultrareview
```

または GitHub PR レビューで:

- **自動レビュアー**: コーディングガイドライン準拠 (Swift スタイル / SwiftUI パターン / ViewModel ルール)
- **人手レビュアー (Soumatou)**: UX 最終判断 (惜しさメーター文言、シェアテキスト形式、巨大リトライのサイズ感 等)

---

> Generated for `feature/sprint-1-foundation` → `main` PR.
> 発行コマンド: `gh pr create --title "feat(sprint-1): foundation — Game Over redesign + Analytics + ASO + Lore + PR + Legal" --body-file docs/pr/sprint-1-pr-template.md --base main --head feature/sprint-1-foundation`
