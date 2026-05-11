---
name: "Sprint 1 — ワンタップリトライ"
about: "Game Over 後の再挑戦を 1 タップで完了できるようにし、離脱を防ぐ"
title: "[Sprint 1] ワンタップリトライ実装（経験者向け OFF オプション付き）"
labels: ["sprint-1", "feature", "ux", "priority-high"]
assignees: ""
---

## 背景

Sprint 1（緊急止血フェーズ）の**最優先タスク**。元会議録での 12 人挙手投票で **5 票**を獲得した「Game Over 画面の刷新 + ワンタップリトライ」のうちの**リトライ動線**側を担う。

田中の総括（会議録より引用）:
> 「既存ユーザーが帰ってこない一番の理由は『Game Over 後にアプリを閉じている』可能性が圧倒的に高い」

現状の課題:
- Game Over 後の再挑戦が 2 タップ以上必要（ResultView → タイトル → New Game）と推測される
- 摩擦が大きく「もう 1 回やる気」を冷ます

本 Issue では Game Over 画面の巨大リトライボタン（#1 と連動）から**即・新規ゲーム開始**できる経路を実装する。経験者は経験者で「Game Over をじっくり眺めて反省したい」ニーズがあるため、設定で OFF にできるオプションも提供する。

元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`

## ゴール / Definition of Done

- [ ] Game Over 画面の巨大リトライボタンを 1 回タップ → 即・新規ゲームが開始される
- [ ] 状態リセット（盤面、ターン、HP、敵、アイテム等）が完全に行われる
- [ ] アニメーションは最小限（暗転 → 即新規盤面、長くて 0.3s）
- [ ] 設定画面で「ワンタップリトライ」を OFF にできるトグルがある（経験者向け）
  - OFF 時は従来挙動（一度結果画面で確認、改めてリトライ）
- [ ] OFF 設定は `UserDefaults` で永続化される
- [ ] InterstitialAd の表示頻度ロジック（広告枠）と矛盾しない
- [ ] アクセシビリティラベルは「もう一度挑戦」等の明確な日本語
- [ ] iPhone SE 〜 Pro Max でレイアウト崩れなし

## 実装タスク

1. `GameViewModel` に `restartGame()` 系メソッドを定義（既存があれば再利用）
   - 盤面・状態の完全リセット
   - 必要に応じて `Task` で非同期初期化
2. `ResultView` の `onPlayAgain()` コールバックを **即実行**ルートに変更（#1 のリトライボタンと連動）
3. 設定画面（`SettingsView` 等）に「ワンタップリトライ」トグルを追加
   - `@AppStorage("isOneTapRetryEnabled")` で永続化、デフォルト `true`
4. ViewModel 側で `isOneTapRetryEnabled` を参照し、OFF 時は従来動線にフォールバック
5. InterstitialAd 表示タイミングとの整合確認
   - 広告表示中はリトライ呼び出しを保留 → 広告閉じ後にリトライ実行
6. アニメーション最適化（不要な遅延削除、最大 0.3s 以内）
7. リトライ実行時に Firebase Analytics の `retry_tapped` イベント発火（#5 と連動）
8. アクセシビリティラベル追加
9. Preview / 実機確認（複数機種）

## 関連ファイル

- `EscapeNine-endless-/EscapeNine-endless-/Views/Result/ResultView.swift`
- `EscapeNine-endless-/EscapeNine-endless-/ViewModels/GameViewModel.swift`
- `EscapeNine-endless-/EscapeNine-endless-/Views/Settings/SettingsView.swift`（または相当ファイル）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Components/InterstitialAd*`（既存挙動維持）

## 想定工数

- ハンズオン実装: 4 時間
- 内部テスト（広告表示時の挙動確認含む）: 2 時間
- 設定画面トグル UI 調整: 1 時間
- **合計: 7 時間**

## 完了基準（Sprint 1 完了基準）

- TestFlight に v1.1 を提出可能な状態
- Game Over → リトライ率（KPI）の計測準備が整う（#5 と連動）
- 設定 OFF でも既存挙動が完全に保たれる（リグレッションなし）

## 関連 Issue / Vault

- 親 Sprint Issue: `[Sprint 1 親 Issue へのリンク placeholder]`
- 関連 Issue: #1（Game Over 画面刷新）、#5（Firebase Analytics KPI 計測）
- 元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- HIG 参照: `/ios-application-dev:references:swiftui-design-guidelines`
