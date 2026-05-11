---
name: "Sprint 1 — Firebase Analytics で KPI 計測開始"
about: "1 階離脱率・Game Over → リトライ率・Day 1 Retention のカスタムイベントを実装し、ダッシュボード稼働"
title: "[Sprint 1] Firebase Analytics KPI 計測実装（1 階離脱率 + リトライ率 + Day 1 Retention）"
labels: ["sprint-1", "analytics", "infrastructure", "priority-high"]
assignees: ""
---

## 背景

Sprint 1（緊急止血フェーズ）の**完了基準**そのものに含まれる必須 Issue。

会議録より:
- Sprint 1 の完了基準は「TestFlight に v1.1 提出 + **KPI ダッシュボード稼働**」
- 現状、定量的な離脱箇所・リトライ動線の効果測定ができていない
- Sprint 1 の他施策（#1 Game Over 刷新 / #2 ワンタップリトライ / #3 チュートリアル）の効果を測るには、**先に計測基盤が動いている必要**がある

Firebase Analytics SDK 自体は既に組み込まれている（`EscapeNine_endless_App.swift` で初期化済み）ため、本 Issue ではカスタムイベントの設計・実装・ダッシュボード構築のみを行う。

元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`

詳細イベント設計は別途作成される `docs/analytics/sprint-1-events.md` を参照（Subagent E が作成）。

## ゴール / Definition of Done

- [ ] 以下のカスタムイベントが実装され、Firebase コンソールで受信確認済み
  - [ ] `floor_started`（パラメータ: `floor_number`）
  - [ ] `floor_cleared`（パラメータ: `floor_number`, `time_elapsed_sec`）
  - [ ] `game_over`（パラメータ: `floor_number`, `defeat_reason`, `time_elapsed_sec`）
  - [ ] `retry_tapped`（パラメータ: `floor_number`, `defeat_reason`）
  - [ ] `tutorial_started` / `tutorial_completed` / `tutorial_skipped`
  - [ ] `share_tapped`（素地のみ、#1 と連動）
- [ ] Firebase Analytics の DebugView で全イベントが受信できる
- [ ] BigQuery エクスポート設定（任意、可能なら有効化）
- [ ] KPI ダッシュボード（Firebase or Looker Studio）が以下を可視化
  - [ ] 1 階離脱率（`floor_started`(1) - `floor_cleared`(1) / `floor_started`(1)）
  - [ ] Game Over → リトライ率（`retry_tapped` / `game_over`）
  - [ ] Day 1 Retention（標準イベント `user_engagement` で計測）
  - [ ] チュートリアル完了率（`tutorial_completed` / `tutorial_started`）
- [ ] イベント命名・パラメータ仕様書（`docs/analytics/sprint-1-events.md`）が完成
- [ ] イベント送信は debug ビルドでも動くが、**個人特定情報を含めない**（`privacy: .private` 相当）

## 実装タスク

1. 別 Subagent E 作成の `docs/analytics/sprint-1-events.md` を確認・レビュー
2. `AnalyticsService.swift`（または相当のラッパー）を新規作成 or 既存を拡張
   - `Analytics.logEvent(_:parameters:)` を直接呼ばず、ラッパー経由にする（差し替え容易）
   - `protocol AnalyticsServiceProtocol` で抽象化
3. 各イベントを発火する箇所にトラッキングコードを挿入
   - `floor_started`: フロア生成完了時（`GameViewModel.startFloor()` 等）
   - `floor_cleared`: フロアクリア判定箇所
   - `game_over`: Game Over 状態遷移箇所
   - `retry_tapped`: ResultView のリトライボタン（#1, #2 と連動）
   - `tutorial_*`: TutorialOverlayView の各ステート遷移（#3 と連動）
4. `time_elapsed_sec` 計測のためのタイマー実装（`Date` 差分）
5. `defeat_reason` の enum 定義 + イベントパラメータ送信
6. Firebase コンソールで DebugView を有効化し、受信確認
7. BigQuery エクスポート設定（プロジェクト設定 → Integrations → BigQuery）
8. ダッシュボード作成
   - Firebase Analytics の標準ダッシュボード活用 or
   - Looker Studio で BigQuery を参照したカスタムダッシュボード
9. プライバシー監査: 個人特定情報（user_id 平文、メールアドレス等）を送信していないことを確認
10. README or `docs/analytics/README.md` にイベント送信のセットアップ手順を記載

## 関連ファイル

- `EscapeNine-endless-/EscapeNine-endless-/EscapeNine_endless_App.swift`（Firebase 初期化済み）
- `EscapeNine-endless-/EscapeNine-endless-/Services/AnalyticsService.swift`（新規 or 既存）
- `EscapeNine-endless-/EscapeNine-endless-/ViewModels/GameViewModel.swift`（イベント発火点）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Result/ResultView.swift`（retry_tapped 発火点、#1, #2 と連動）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Tutorial/TutorialOverlayView.swift`（tutorial_* 発火点、#3 と連動）
- `docs/analytics/sprint-1-events.md`（別 Subagent E が作成、イベント仕様書）

## 想定工数

- イベント設計レビュー: 1 時間
- `AnalyticsService` ラッパー実装: 2 時間
- 各イベント発火箇所の実装: 4 時間
- DebugView での受信確認・修正: 2 時間
- BigQuery エクスポート設定: 1 時間
- ダッシュボード作成: 3 時間
- プライバシー監査・ドキュメント: 1 時間
- **合計: 14 時間**

## 完了基準（Sprint 1 完了基準）

- TestFlight に v1.1 を提出可能な状態
- **KPI ダッシュボード稼働**（Sprint 1 の完了基準そのもの）
- 1 階離脱率・Game Over → リトライ率・Day 1 Retention が**数値で確認できる**
- イベント仕様書がコードと一致している

## 関連 Issue / Vault

- 親 Sprint Issue: `[Sprint 1 親 Issue へのリンク placeholder]`
- 関連 Issue: #1（Game Over 刷新）、#2（ワンタップリトライ）、#3（チュートリアル）、#4（ASO クイック改善）
  - 全ての施策の効果測定基盤として本 Issue が前提
- イベント仕様書: `docs/analytics/sprint-1-events.md`（別 Subagent E 作成）
- 元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- 関連スキル: `firebase-ios-patterns`
