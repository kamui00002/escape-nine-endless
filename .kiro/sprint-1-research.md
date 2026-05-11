# Sprint 1 着手前 — 既存コード調査メモ

調査日: 2026-05-09 (Discord Bot セッション)
調査対象: feature/sprint-1-foundation ブランチ着手前の現状把握
元会議録: [Vault] 2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド).md

---

## 🟢 既に実装済 (壊さない、活かす)

### 計測 / SDK 関連
| 項目 | 状態 |
|---|---|
| ATT prompt 実装 | ✅ `EscapeNine_endless_App.swift` line 81-83 で `requestTrackingAuthorization` 呼び出し済 |
| `NSUserTrackingUsageDescription` キー | ✅ `Info.plist` line 78-79 「広告の最適化とアプリ改善のために、トラッキングの許可をお願いします。」 |
| Firebase Analytics 初期化 | ✅ App.swift line 36-45 で `setAnalyticsCollectionEnabled(true)` + Consent Mode |
| Firebase 匿名認証 | ✅ `FirebaseService.shared.signInAnonymously()` |
| Conversion 計測 | ✅ `ConversionService.shared.trackAppOpen()` |
| Firebase iOS SDK | ✅ **12.10.0** (Deep Research 推奨 11.15.0+ を満たす) |
| google-ads-on-device-conversion | ✅ **3.3.0** (推奨 2.3.0+ 満たす) |
| GoogleMobileAds | ✅ 12.14.0 |
| Facebook SDK | ✅ 17.4.0 |
| SKAdNetwork ID | ✅ 16 個 (Google `cstr6suwn9` 含む) |

→ **計測関連は SDK 側ほぼ完璧、Firebase の App Store ID 未登録が真因の可能性 95%**

### Views (既存)
- ✅ `Views/Result/ResultView.swift` — 現在の Game Over 画面
- ✅ `Views/Home/HomeView.swift`
- ✅ `Views/Home/TutorialOverlayView.swift` — チュートリアル既に実装!
- ✅ `Views/DailyChallenge/DailyChallengeView.swift` — デイリーチャレンジ既に実装!
- ✅ `Views/Game/GameView.swift`, `GridBoardView.swift`, `BeatIndicatorView.swift`
- ✅ `Views/Achievement/AchievementPopupView.swift`
- ✅ `Views/Components/GameButton.swift`, `GameCard.swift`, `GameBackground.swift`

### ViewModels / Services (既存)
- ✅ `GameViewModel.swift` — 既に `defeatReason`, `showGameOverOverlay`, `dailyChallengeMode` 実装済
- ✅ `Services/AdMobService.swift`, `FirebaseService.swift`, `ConversionService.swift`
- ✅ `Services/DailyChallengeService.swift`
- ✅ `Services/GameCenterService.swift`, `StoreKitService.swift`, `PurchaseManager.swift`

---

## 🔴 ResultView.swift に**追加すべき要素** (Sprint 1 中核)

会議録の合意「Game Over 画面を **離脱口** から **発射台** に変える」のために追加:

| # | 要素 | 現状 | 追加 |
|---|---|---|---|
| 1 | 「あと 1 マスで生存」 (惜しさメーター) | ❌ なし | 新規追加 (敵から何マス離れて死んだか) |
| 2 | 巨大リトライボタン (画面下 70%) | ⚠️ 普通サイズ | サイズ拡大、底部固定 |
| 3 | 挑戦時間表示 (elapsedSeconds) | ❌ なし | 新規追加 |
| 4 | 自己ベスト誘発演出 | ⚠️ NEW RECORD あり (NewRecordとは別) | 「自己ベスト!」「ベスト: X階」 |
| 5 | シェアボタン (Wordle 風結果記号 + 画像) | ❌ なし | 新規追加 (Sprint 2 完全実装、Sprint 1 で素地のみ) |

**ResultView を改修する戦略を採用** (新規 GameOverView 作成より)
理由: 既存の AchievementPopup / InterstitialAd / DefeatReason 表示を壊さず Sprint 1 要素を追加できる

---

## 📝 GameViewModel に追加が必要なプロパティ

```swift
// MARK: - Sprint 1: Near-miss meter
@Published var nearMissDistance: Int = 0  // 敵から何マス離れて死んだか
private var enemyPositionAtDeath: Int? = nil

// MARK: - Sprint 1: Elapsed time
@Published var elapsedSeconds: Double = 0
private var gameStartTime: Date? = nil

// 既存の handleGameOver() 内で:
// - gameStartTime からの経過時間を計算
// - playerPosition と enemyPosition の距離を計算 (グリッド距離)
```

---

## 🌟 Sprint 1 と整合する既存実装

| 会議の合意 | 既存実装 |
|---|---|
| チュートリアル必須化 | `TutorialOverlayView` 既存 (改善余地あり、初回限定 + 3 画面に整理可) |
| デイリーチャレンジ | `DailyChallengeView` + `DailyChallengeService` 既に存在 (Sprint 2 で機能追加) |
| ASO 改善 | コード変更不要、`docs/aso/` で素材生成 |

---

## 🔗 Vault 関連 (会議の出典)

- [[2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)]]
- [[2026-05-10 [明日朝イチ] EscapeNine 計測修理 即実施シート]]
- [[2026-05-09 [アクション] EscapeNine 0 conv Mac 実施手順 統合版]]
