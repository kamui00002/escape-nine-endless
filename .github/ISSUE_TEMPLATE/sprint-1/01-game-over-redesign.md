---
name: "Sprint 1 — Game Over 画面刷新"
about: "Game Over 画面に惜しさメーター・巨大リトライボタン・自己ベスト表示等を追加し離脱を防ぐ"
title: "[Sprint 1] Game Over 画面刷新（惜しさメーター + 巨大リトライ + 自己ベスト表示）"
labels: ["sprint-1", "feature", "ux", "priority-high"]
assignees: ""
---

## 背景

Sprint 1（緊急止血フェーズ）の**最優先タスク**。元会議録での 12 人挙手投票で **5 票**を獲得した最重要施策。

田中の総括（会議録より引用）:
> 「既存ユーザーが帰ってこない一番の理由は『Game Over 後にアプリを閉じている』可能性が圧倒的に高い」

現状の `ResultView.swift` は単純な「敗北」表示で、以下の問題がある:
- 惜しかった感が伝わらず「もう 1 回」のモチベーションが起きない
- リトライ動線が弱い（タップ階層が深い、ボタンが小さい）
- 自己ベスト・進捗の可視化がなく、上達実感が得られない

本 Issue では Game Over 画面を**離脱防止 → 即再挑戦**に最適化する刷新を行う。

元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`

## ゴール / Definition of Done

- [ ] 惜しさメーター（「あと N マスで生存」「ベストスコアまであと M」等）が Game Over 画面に表示される
- [ ] 画面下部 70% を占める巨大リトライボタンが配置される（44pt 以上、HIG 準拠）
- [ ] 挑戦時間（プレイ時間）が表示される
- [ ] 自己ベスト誘発要素（「自己ベスト更新まであと X」等）が表示される
- [ ] シェアボタン（素地のみ、押下時は今は noop でも可）が配置される
- [ ] 既存の AchievementPopup / InterstitialAd / DefeatReason 表示を**壊さない**
- [ ] iPhone SE (375pt) 〜 Pro Max (430pt) の全画面サイズでレイアウト崩れなし
- [ ] Dark Mode / Dynamic Type に対応
- [ ] Preview で Game Over 状態が確認可能

## 実装タスク

1. `ResultView.swift` の構造リファクタ（`@ViewBuilder` で `nearMissMeterView` / `retryButtonView` / `bestRecordHintView` / `shareButtonRowView` をサブビュー化）
2. 惜しさメーター用のロジックを `GameViewModel` または `Result` モデルに追加
   - 「あと N マスで生存」（最終位置から脱出地点までのマンハッタン距離 等）
   - 「ベストスコアまであと M」（自己ベストとの差分）
3. 巨大リトライボタンの実装（画面下部 70%、`.frame(minHeight: 44)`、`.tint(.accentColor)`）
4. 挑戦時間表示（`Date` 差分 → `mm:ss` フォーマット）
5. 自己ベスト誘発カードの実装（`UserDefaults` or `SwiftData` で保存している自己ベストを参照）
6. シェアボタン UI 配置（`ShareLink` の素地、共有テキストは仮文言で OK）
7. AchievementPopup / InterstitialAd 表示順を保持する zIndex / overlay の調整
8. DefeatReason 表示の継続確認（既存ロジックを残す）
9. Preview 追加: 通常 Game Over / 自己ベスト更新 / 惜敗 / 大敗 の 4 パターン
10. アクセシビリティラベル付与（`.accessibilityLabel("もう一度挑戦")` 等）

## 関連ファイル

- `EscapeNine-endless-/EscapeNine-endless-/Views/Result/ResultView.swift`
- `EscapeNine-endless-/EscapeNine-endless-/ViewModels/GameViewModel.swift`
- `EscapeNine-endless-/EscapeNine-endless-/Models/GameResult.swift`（または相当ファイル）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Components/AchievementPopup.swift`（既存の挙動維持）
- `EscapeNine-endless-/EscapeNine-endless-/Views/Components/InterstitialAd*`（既存の挙動維持）

## 想定工数

- ハンズオン実装: 6 時間
- 内部テスト（実機 + Simulator 複数機種）: 2 時間
- レビュー・微調整: 2 時間
- **合計: 10 時間**

## 完了基準（Sprint 1 完了基準）

- TestFlight に v1.1 を提出可能な状態
- Game Over → リトライ率（KPI）の計測準備が整う（#5 と連動）

## 関連 Issue / Vault

- 親 Sprint Issue: `[Sprint 1 親 Issue へのリンク placeholder]`
- 関連 Issue: #2（ワンタップリトライ）、#5（Firebase Analytics KPI 計測）
- 元会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)`
- HIG 参照: `/ios-application-dev:references:swiftui-design-guidelines`、`/ios-application-dev:references:accessibility`
