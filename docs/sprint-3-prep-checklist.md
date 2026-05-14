# Sprint 3 着手準備チェックリスト ⭐️

作成日: 2026-05-15
ステータス: **準備中 (ベースライン採取後に Sprint 3 正式着手)**
依存: `docs/onboarding-v1.1-design.md` (本実装の設計書)
引き継ぎ元: `docs/sprint-2-status.md` (Sprint 2 完了棚卸し)

---

## 1. Sprint 3 着手判断ゲート (§10 着手判断 再掲)

| ゲート | 状況 | 残作業 |
|---|---|---|
| 1. Sprint 1 が TestFlight に提出済 | ✅ 達成 | v1.0.0 配信中 |
| 2. Sprint 1 KPI ベースライン取得済 | 🟡 雛形 main 済 (PR #25) | **Firebase Console で 7 日分採取 → §4 テーブル記入 PR** |
| 3. Sprint 2 (デイリー/抜かれ通知/シェア拡張) 完了済 | ✅ 達成 | PR #27 マージ済、`docs/sprint-2-status.md` で公式化済 |
| 4. Obsidian Sprint 2 計画書 → 完了報告昇格 | ✅ 達成 | `📂 プロジェクト/EscapeNine/2026-05-15 Sprint 2 完了報告.md` 作成済 |

→ **残り 1 条件 (ベースライン数値採取) で Sprint 3 着手可能**

---

## 2. ゲート 2 完全達成手順 (ユーザー手動タスク、15-20 分)

### 2.1 Firebase Console 操作

1. https://console.firebase.google.com を開く
2. プロジェクト `escapenine-endless` を選択
3. 左メニュー: **分析 → イベント**
4. 期間セレクタで **直近の連続 7 日間** を選択 (TestFlight 内部テスター期間は除外、本番配信期間のみ)
5. 各イベントカウントを `docs/aso/sprint-1-baseline.md` §4「実測値」テーブルに記入:

| イベント | カウント取得方法 |
|---|---|
| `eg_game_started` | イベント一覧から行をクリック |
| `eg_floor_cleared` (floor==1) | パラメータフィルタ `floor=1` |
| `eg_floor_cleared` (全 floor) | パラメータフィルタなし |
| `eg_game_over_shown` | イベント一覧から行をクリック |
| `eg_retry_tapped` | イベント一覧から行をクリック |
| `eg_home_tapped` | イベント一覧から行をクリック |

6. **漏斗 (Funnels)** で `eg_game_started → eg_floor_cleared(floor==1)` を作成 → 1 階離脱率算出
7. **リテンションレポート**で D1 / D7 値を取得
8. §4 テーブル記入後、PR を作成して main マージ

### 2.2 サンプル数下限チェック

- `eg_game_started` の合計が **100 セッション以上** あるか?
  - 100 未満なら計測期間を 14 日間まで延長
  - 14 日でも未達なら相対値ベース KPI に切り替え (§1 参照)

### 2.3 完了後

`docs/aso/sprint-1-baseline.md` §4 テーブルが埋まり main マージ完了 → **ゲート 2 達成、Sprint 3 即着手可能**

---

## 3. Sprint 3 着手時の最初の 3 タスク (本実装スコープ)

ベースライン採取完了直後、以下の順で実装着手:

### Task 1: Constants 追加 (30 分)

`Utilities/Constants.swift` に追加:

```swift
// MARK: - v1.1 オンボーディング定数 (Sprint 3)
struct TutorialConstants {
    static let tutorialClearTurns: Int = 3        // Step 4 用に通常 10 から短縮
    static let prologueClearTurns: Int = 3        // Floor 0 プロローグの必要ターン
    static let prologueSafeMinDistance: Int = 3   // Floor 1 初回限定の安全距離
    static let prologueFloor: Int = 0
}
```

### Task 2: Floor.calculateBPM の clamp 下限を 1 → 0 に変更 (15 分)

`Models/Floor.swift`:

```swift
static func calculateBPM(for floor: Int) -> Double {
    if floor == 0 { return 60 }  // ← 新規: プロローグ
    let clampedFloor = max(0, min(floor, Constants.maxFloors))  // ← 変更: 1 → 0
    // 既存ロジック...
}
```

### Task 3: AnalyticsEvents 拡張 (30 分)

`Services/AnalyticsEvents.swift` に追加:

```swift
enum AnalyticsEvent: String {
    // 既存...
    // v1.1 オンボーディング (Sprint 3)
    case tutorialStarted = "eg_tutorial_started"
    case tutorialStepCompleted = "eg_tutorial_step_completed"
    case tutorialComplete = "eg_tutorial_complete"
}

enum AnalyticsParam {
    // 既存...
    static let stepNumber = "step_number"
    static let skipped = "skipped"
}

struct AnalyticsLogger {
    // 既存...
    static func logTutorialStarted() { log(.tutorialStarted) }

    static func logTutorialStepCompleted(stepNumber: Int, skipped: Bool) {
        log(.tutorialStepCompleted, parameters: [
            AnalyticsParam.stepNumber: stepNumber,
            AnalyticsParam.skipped: skipped
        ])
    }

    static func logTutorialComplete(elapsedSeconds: Double) {
        log(.tutorialComplete, parameters: [
            AnalyticsParam.elapsedSeconds: elapsedSeconds
        ])
    }
}
```

これら 3 つは独立した PR としてマージ可能。本格実装 (OnboardingTutorialView 等) はその後の PR で。

---

## 4. Sprint 3 全体タスクツリー (§7 工数分解 再掲)

### 新規 SwiftUI View (2.5 日)

| ファイル | 役割 |
|---|---|
| `Views/Onboarding/OnboardingTutorialView.swift` | 4 ステップ統括親 View |
| `Views/Onboarding/TutorialHighlightView.swift` | 光るリング (タップ可能マス示唆) |
| `Views/Onboarding/DangerZoneView.swift` | 敵隣接 8 マスの赤いオーバーレイ |
| `Views/Onboarding/TutorialStepInstructionView.swift` | 上部の説明文表示 |

### 既存 View 修正 (2.5 日)

| ファイル | 修正 |
|---|---|
| `Views/Home/HomeView.swift` | `showTutorial` → `showOnboardingTutorialV1_1` 切替 + migration |
| `Views/Game/GameView.swift` | チュートリアルモードフラグ + 敵 hidden / ターン短縮 |
| `ViewModels/GameViewModel.swift` | `startPrologueFloor()` メソッド + シードバイアス |

### サウンド・触覚 (2 日)

| 項目 | 内容 |
|---|---|
| heartbeat_low.wav 制作 | Suno AI or 既存音源 (60 BPM 低音ピアノ単音、約 4 秒で 4 拍) |
| AudioManager 新 API | `playLoopingSFX(_:)` / `stopLoopingSFX(_:)` |
| 振動連動 | `UIImpactFeedbackGenerator` + BeatEngine 衝突回避 (`suspend()` パターン) |
| Reduce Motion 対応 | `UIAccessibility.isReduceMotionEnabled` で 3 層独立 opt-out |

### テスト (2 日)

| 項目 | 内容 |
|---|---|
| ステップごとのプレイテスト | シミュレータ + 実機で各 Step 動作確認 |
| 既存ユーザー migration | Sprint 1 通過済ユーザーが v1.1 を通るか |
| Reduce Motion / VoiceOver | アクセシビリティ要件確認 |

### 合計工数

**実装 5-6 日 + テスト 2 日 = 計 7-8 日**

---

## 5. Sprint 3 ブランチ運用案

```
main (Sprint 2 マージ済)
  ↓
feature/onboarding-v1.1 (Sprint 3 統合ブランチ、または直接 main へ PR 連発)
  ├── 小 PR 1: Constants + Floor + AnalyticsEvents 追加 (本書 §3 のタスク)
  ├── 小 PR 2: OnboardingTutorialView + 子 View 群
  ├── 小 PR 3: GameViewModel.startPrologueFloor()
  ├── 小 PR 4: heartbeat 音源 + AudioManager loop API
  ├── 小 PR 5: HomeView migration (hasSeenTutorialV1_1)
  └── 小 PR 6: アクセシビリティ + テスト
```

Sprint 2 と同じく **worktree + 単発 PR ベース**を推奨 (統合ブランチ不要)。

---

## 6. リスクと対策 (v1.1 設計書 §8 再掲)

| リスク | 対策 |
|---|---|
| チュートリアルが長すぎて離脱 | 全 4 ステップで合計 40-60 秒以内 |
| ヒヤリ演出が怖すぎる | Step 3 心拍音 5 秒以内、フラッシュ 0.2 秒、強度ユーザーテストで調整 |
| 既存ユーザーが「また通らされる」と不満 | v1.1 リリースノートで「世界観オープニング追加」訴求 |
| 工数競合 | 単独 PR で機能を分割、Sprint 4 へ余剰タスクを送る |
| Reduce Motion 無効化忘れ | 第 18 回 Lisa 必須対応 8 項目、自動テスト追加 |

---

## 7. 関連ドキュメント

- v1.1 オンボーディング設計書: `docs/onboarding-v1.1-design.md`
- Sprint 2 棚卸し: `docs/sprint-2-status.md`
- Sprint 1 KPI ベースライン雛形: `docs/aso/sprint-1-baseline.md`
- Sprint 2 完了報告 (Obsidian): `📂 プロジェクト/EscapeNine/2026-05-15 Sprint 2 完了報告.md`
- ビジュアルモックアップ (生成済、tmp): `/tmp/onboarding-v1.1-mockup-20260514-2315.html`

---

## 8. 本書の更新ルール

- ゲート 2 達成 (ベースライン採取完了) で §1 を ✅ に更新
- Sprint 3 着手時に本書の §3 タスクから着手、完了したものに ✅
- Sprint 3 完了時に本書を `docs/sprint-3-status.md` に rename or 「完了報告」を新規作成
