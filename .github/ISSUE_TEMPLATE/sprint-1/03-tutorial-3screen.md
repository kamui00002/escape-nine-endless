---
name: "Sprint 1 — チュートリアル（初回のみ、3 画面）"
about: "初回起動時に 3 画面のチュートリアルを表示し、初心者の 1 階離脱を抑える"
title: "[Sprint 1] チュートリアル改善（初回 3 画面 + 2 回目以降スキップ）"
labels: ["sprint-1", "feature", "onboarding", "priority-medium"]
assignees: ""
---

## 背景

Sprint 1（緊急止血フェーズ）の重要施策の 1 つ。

会議録より:
- 1 階での離脱率が高いことが KPI 上の懸念点
- 既存の `TutorialOverlayView.swift` は存在するが、表示頻度・内容が不明瞭で初心者を取りこぼしている可能性

新規ユーザーは初回プレイで「ルール（脱出ローグライク 9 マス）」を理解できないと、即離脱する。3 画面のスワイプ式チュートリアルで操作・目的・コツを最低限伝え、定着率を上げる。

元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`

## ゴール / Definition of Done

- [ ] 初回起動時のみ 3 画面のチュートリアルが**強制表示**される
- [ ] スワイプ（または横ボタン）で 3 画面を遷移できる
- [ ] 各画面に「次へ」「スキップ」ボタンがある
- [ ] 最終画面の「ゲームを始める」で本編に遷移
- [ ] 2 回目以降の起動では自動スキップ（`@AppStorage("hasSeenTutorial")` で管理）
- [ ] 設定画面から再表示できる（任意、できれば実装）
- [ ] 各画面はアイコン + 1〜2 行の短文で構成（読み疲れない）
- [ ] iPhone SE 〜 Pro Max でレイアウト崩れなし
- [ ] Dark Mode / Dynamic Type 対応
- [ ] アクセシビリティ: ページインジケータの読み上げ対応

## 各画面の内容（叩き台）

### 画面 1: 「9 マスから脱出せよ」
- ビジュアル: 3×3 グリッドのアイコン
- テキスト: 「9 マスのフロアを攻略し、上の階を目指せ。ローグライク脱出ゲーム。」

### 画面 2: 「タップで移動・敵を倒せ」
- ビジュアル: タップ操作のアニメーション or アイコン
- テキスト: 「マスをタップして移動。敵がいれば自動で戦闘。アイテムでブースト。」

### 画面 3: 「死んでも何度でも挑戦」
- ビジュアル: リトライアイコン
- テキスト: 「Game Over してもすぐ再挑戦できる。記録を伸ばして自己ベストを更新しよう。」

※ 上記は叩き台。実装時にチームで文言を確定する。

## 実装タスク

1. 既存 `TutorialOverlayView.swift` の現状確認・棚卸し
2. 3 画面構成への改修（`TabView(.page)` + `PageTabViewStyle` で実装）
3. 各画面のコンテンツ（アイコン・テキスト）配置
4. 「次へ」「スキップ」「ゲームを始める」ボタンの実装
5. `@AppStorage("hasSeenTutorial")` での初回判定 + 永続化
6. 起動時の表示判定ロジック（`App` または `RootView` で `.fullScreenCover`）
7. （任意）設定画面に「チュートリアルを再表示」ボタン追加
8. ページインジケータ（`PageIndexView` 等）のアクセシビリティ対応
9. Preview 追加（3 画面それぞれ）
10. Firebase Analytics の `tutorial_started` / `tutorial_completed` / `tutorial_skipped` イベント発火（#5 と連動）

## 関連ファイル

- `EscapeNine-endless-/EscapeNine-endless-/Views/Tutorial/TutorialOverlayView.swift`
- `EscapeNine-endless-/EscapeNine-endless-/EscapeNine_endless_App.swift`（起動時表示の判定）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Settings/SettingsView.swift`（任意: 再表示ボタン）

## 想定工数

- ハンズオン実装: 5 時間
- コンテンツ調整（文言・アイコン選定）: 2 時間
- 内部テスト（初回判定の検証含む）: 2 時間
- **合計: 9 時間**

## 完了基準（Sprint 1 完了基準）

- TestFlight に v1.1 を提出可能な状態
- 初回起動 → チュートリアル → ゲーム開始 のフローが破綻なく動作
- `hasSeenTutorial` リセット手順がドキュメント化（QA 用）

## 関連 Issue / Vault

- 親 Sprint Issue: `[Sprint 1 親 Issue へのリンク placeholder]`
- 関連 Issue: #5（Firebase Analytics KPI 計測 - tutorial 関連イベント）
- 元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- HIG 参照: `/ios-application-dev:references:swiftui-design-guidelines`、`/ios-application-dev:references:accessibility`
