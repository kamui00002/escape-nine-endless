# オンボーディング v1.1 動的チュートリアル — 設計書 ⭐️

作成日: 2026-05-14
最終更新: 2026-05-14 (multi-agent-reviewer HIGH 指摘反映)
ステータス: **設計フェーズ (実装未着手)**
対象リリース: **v1.1 = Sprint 3 単独 PR** (旧案「Sprint 2 後半」は工数競合のため取り下げ)
依存元: Sprint 1 で実装済の `TutorialOverlayView.swift` (静的 3 ページ版)

> ⚠️ **Sprint 2 計画書との関係**: Sprint 2 (5/13-5/26) は Feature 1 (デイリーチャレンジ 6-7 日) + Feature 2 (抜かれ通知 8-9 日) + Feature 3 (シェア拡張 6 日) で既に飽和。onboarding v1.1 は工数 7-8 日のため Sprint 3 へ送る。Obsidian 側の Sprint 2 計画書にも同期する必要あり (PR 範囲外、ユーザー手動)。

> **背景**: 2026-05-09 戦略会議で「**1 階を壁から招待状に変える**」「**動的チュートリアル必須化 (v1.1)**」が決定。Sprint 1 では時間制約で静的 3 ページ版のみ先行リリース。本書は会議決定通り「完全プレイアブル 4 ステップ + ヒヤリ演出 + 心拍音 + 振動」の本格版を設計する。

---

## 1. 戦略目的と KPI

### 目的

| 課題 | 解決 |
|---|---|
| 1 階で離脱するユーザーが多い (会議第 1 回 Dr. Chen「Zeigarnik 効果が起きていない」) | プレイアブルチュートリアルで「自分でクリアした」体験を作り、未完了タスクへの執着を生む |
| ルールを文字で説明すると初心者が読まずに離脱 | 「やってみる」を最優先、テキストは最小限 |
| 既存ユーザーが再びチュートリアルを通る不快感 | `hasSeenTutorial` migration を継承、初回ユーザーのみ表示 |

### KPI 目標 (会議結論より)

| KPI | 現状 (推定) | 目標 (v1.1 反映後 2 週間) | ベースライン計測式 |
|---|---|---|---|
| **1 階離脱率** | 50%+ | **30% 以下** | `1 - count(eg_floor_cleared where floor>=1) / count(eg_game_started)` |
| **Day 1 リテンション** | 不明 | +10pp | Firebase Analytics 「リテンション」レポートの D1 値 |
| **チュートリアル完了率** | 不明 | 80%+ | `count(eg_tutorial_complete) / count(eg_tutorial_started)` ※両イベントは v1.1 で新規追加必須 |
| **チュートリアル後 → 1 階クリア率** | - | 90%+ | `count(eg_floor_cleared where floor==1 AND prev_event==eg_tutorial_complete) / count(eg_tutorial_complete)` |

### ベースライン取得手順 (着手前の §10 条件 2 を満たすため)

1. **計測期間**: Sprint 1 TestFlight 提出 + ATC 公開後の **連続 7 日間**
2. **サンプル数下限**: `eg_game_started` の合計 **100 セッション以上** (信頼区間 ±5% を担保)
3. **保存先**: `docs/aso/sprint-1-baseline.md` (現状未作成、本書着手前に雛形作成)
4. **必要な新規イベント (v1.1 実装と同時に追加)**:
   - `eg_tutorial_started` (チュートリアル Step 1 開始時)
   - `eg_tutorial_step_completed` (各 Step 通過時、`step_number` パラメータ付き)
   - `eg_tutorial_complete` (Step 4 完了時)

---

## 2. 現状 (Sprint 1) と本格版のギャップ

### Sprint 1 で実装済 (`TutorialOverlayView.swift`)

```
[現状: 静的 3 ページ]
Page 1: 「影が動き出す」+ figure.run アイコン + 説明文
Page 2: 「1 手で動く」+ hand.tap アイコン + 説明文
Page 3: 「逃げ切れ」+ flag.checkered アイコン + 説明文
→ スワイプで進む、スキップなし、初回のみ表示
```

| 会議決定 | Sprint 1 | 本格版 |
|---|---|---|
| 4 ステップ | 3 ページ | **4 ステップへ拡張** |
| スキップなし | ✅ 既に対応 | 継承 |
| 2 回目以降スキップ | ✅ `hasSeenTutorial` | 継承 + `hasSeenTutorialV1_1` 追加 |
| 完全プレイアブル | ❌ 静的テキスト | **3x3 グリッドで実際に操作** |
| 1 階を「プロローグ」扱い | ❌ 普通の階層 1 | **「Floor 0」プロローグ化、Floor 1 は容易なシード** |
| ヒヤリ演出 | ❌ | **Step 3 で意図的に「あと 1 マスで死ぬ」配置** |
| 心拍音 + 振動 | ❌ | **AudioManager + BeatEngine 連動で実装** |

---

## 3. プレイアブル 4 ステップ — 詳細設計

### 全体構成

```
[起動]
  ↓
オープニング画面 (1.5 秒、自動進行)
  「禁書を盗んだ。影が動き出す。」 ← 第 15 回ナラティブ
  ↓
Step 1: 動きを覚える (操作チュートリアル)
  ↓
Step 2: 影を避ける (敵 AI 認識)
  ↓
Step 3: ヒヤリ体験 (脳に焼きつく瞬間)
  ↓
Step 4: 階層クリアの喜び (達成感)
  ↓
チュートリアル完了 → `hasSeenTutorialV1_1 = true`
  ↓
通常 Floor 1 (容易なシード保証) へ遷移
```

### Step 1: 動きを覚える (約 10-15 秒)

```
盤面: プレイヤーのみ配置 (敵なし)
目的: タップで隣接マス移動を覚える
手順:
  1. プレイヤーが中央 (位置 5) に表示
  2. 上部に文字: 「タップで隣のマスに移動できる」
  3. ハイライト矢印で隣接マスを点滅
  4. 1 回タップで移動 → 「いいぞ。動きをつかんだ」
  5. もう 1 〜 2 回移動させる → Step 2 へ自動進行

実装ポイント:
- 既存 GridBoardView / GameView を流用、敵を nil/hidden に
- ハイライトは新規 component `TutorialHighlightView` (光るリング)
- 4 ステップ全体を統括する `OnboardingTutorialView` (新規)
```

### Step 2: 影を避ける (約 15-20 秒)

```
盤面: プレイヤー (位置 1) + 敵 1 体 (位置 9)
目的: 敵が追ってくることを認識、避ける動きを覚える
手順:
  1. 上部に文字: 「影は毎ターン 1 マス近づく」
  2. 敵の周囲に「危険圏」を表示 (赤い半透明オーバーレイ、隣接 8 マス)
  3. プレイヤーを操作して敵から逃げる
  4. 3-4 ターン耐え抜く → 「影をうまく避けた」 → Step 3 へ

実装ポイント:
- 危険圏オーバーレイは新規 `DangerZoneView` (隣接マスを赤く)
- AI は Easy 固定 (会議: 70% ランダム、30% 追跡)
- 3 ターン経過で自動進行
```

### Step 3: ヒヤリ体験 (約 10-15 秒) ⭐️ 最重要

```
盤面: プレイヤー (位置 5) + 敵 (位置 4) ← 隣接配置で開始
目的: 「あと 1 マスで死ぬ」を脳に焼きつける
手順:
  0. ⚠️ Step 開始前に「次の Step は強い演出 (心拍音・振動・赤フラッシュ) が含まれます。スキップする場合は右上のボタンを押してください」予告 (1.5 秒、Reduce Motion ON or Reduced Audio 設定時は自動スキップ)
  1. 上部に文字: 「あと 1 マスで影に飲まれる」
  2. 心拍音 (60 BPM) を AudioManager で開始
  3. 軽い振動 (UIImpactFeedbackGenerator.light) を毎ビート
  4. プレイヤーを 1 回動かす → 敵から離れる
  5. 「ヒヤリとした。これがこのゲームだ。」 → Step 4 へ

スキップ動線 (必須、multi-agent-reviewer HIGH-2 反映):
- 右上に常時表示のスキップボタン (3 タップ or 長押し 1 秒で発火、誤タップ防止)
- スキップ時は `eg_tutorial_step_completed(step_number: 3, skipped: true)` を発火させて KPI と分離
- スキップ後も Step 4 へ進む (チュートリアル全体は完走扱い)

実装ポイント:
- 心拍音: 低音ピアノ単音 (松本さん指摘の 16 回オーディオ「沈黙より一歩先」階層 1-3 と整合)
- 振動: `UIImpactFeedbackGenerator(style: .light).impactOccurred()` を毎ビートで
- 「ヒヤリ」演出: 画面を一瞬赤くフラッシュ (0.2 秒) + 危険圏を強調
- このステップは "脳に焼きつく瞬間" なので 5-10 回テストプレイで体験の強さを調整
- アクセシビリティ詳細は §5 を参照 (Reduce Motion / Reduced Audio / 色覚多様性への完全 opt-out)
```

### Step 4: 階層クリアの喜び (約 5-10 秒)

```
盤面: プレイヤー (位置 1) + 敵 (位置 9) ← 距離最大で開始
目的: 「10 ターン耐えたらクリア」の達成感
手順:
  1. 上部に文字: 「10 ターン耐えれば階層クリア」
  2. ターンカウンタを大きく表示 (例: 1/3、本来 10 だがチュートリアルは短縮)
  3. プレイヤーを動かして 3 ターン耐える
  4. 階層クリア演出 (会議 第 17 回「数字が力強くスケールアップ + パーティクル」)
  5. 「これが Escape Nine だ。本当の挑戦が始まる」
  6. オープニングへ自然に遷移 → 通常 Floor 1 へ

実装ポイント:
- ターン数はチュートリアル専用に 3 ターンに短縮 (constant: tutorialClearTurns)
- クリア演出は既存 `BounceIn` / `Glow` を流用
- BGM は「沈黙より一歩先」アレンジ (まだ静かめ)
```

---

## 4. 1 階を「プロローグ」化する設計

### 現状の問題

- 既存の Floor 1 はランダム配置 + Easy AI でもプレイヤーによっては運悪く死ぬ
- 「チュートリアル直後に死ぬ」 → 学習体験が壊れる

### 解決策: 「Floor 0」プロローグ + Floor 1 ガード付き

```
チュートリアル完了直後:
  - 通常 Floor 1 の代わりに `Floor 0: プロローグ` を 1 回プレイ
  - Floor 0 は:
    - シード固定 (毎回同じ配置)
    - プレイヤー位置 5 (中央)、敵位置 1 (左上)
    - 敵 AI: 完全ランダム (追跡なし)
    - 必要ターン: 3 ターン (通常 10 ターンから短縮)
  - Floor 0 クリア → 「結界の外。本当の旅が始まる」フレーバー
  - Floor 1 へ遷移
Floor 1 (本格スタート):
  - 既存ロジックだが、`hasSeenTutorialV1_1 == true` の初回 1 回限定で:
    - シードに「安全側バイアス」 (プレイヤーと敵が初期距離 3 以上)
    - 2 回目以降は完全ランダム
```

### 実装変更点 (具体的な enum / 定数 / メソッド)

> ⚠️ **multi-agent-reviewer 指摘反映 (2026-05-14)**: 当初案の `StageManager.startPrologueFloor()` 追加は、**現行 `StageManager` がインスタンスメソッドゼロ・getter のみ** (getBPM / getSpecialRule / getDifficulty / getFloorDescription) なので構造を破壊する。Floor 開始の責務は `GameViewModel.startGame()` 側にあるため、prologue 専用 path も `GameViewModel` に追加する。加えて `Floor.calculateBPM(for:)` は `let clampedFloor = max(1, min(floor, ...))` で **floor=0 を 1 に丸める**ため、clamp の下限を 0 に変更する必要がある。

| ファイル | 追加するもの | 既存実装との接続 |
|---|---|---|
| `Models/Floor.swift` | `static let prologueFloor: Int = 0` + `calculateBPM` の clamp 下限を 1 → 0 に変更 + `if floor == 0 { return 60 }` 分岐 | 現行 `let clampedFloor = max(1, min(floor, Constants.maxFloors))` を `max(0, ...)` に変更。0 以外は既存ロジック維持 |
| `Utilities/Constants.swift` | `static let tutorialClearTurns: Int = 3`, `static let prologueClearTurns: Int = 3`, `static let prologueSafeMinDistance: Int = 3` | 既存は `getMaxTurns(for floor: Int)` 関数で動的計算しているため、本定数は **`getMaxTurns` をバイパスする特殊ケース** であることをコメントで明記 (または `getMaxTurns(for:)` 内で `floor == 0 → prologueClearTurns` 分岐に統合する選択肢もあり) |
| `ViewModels/GameViewModel.swift` | `func startPrologueFloor()` メソッド + `private var isFirstFloor1AfterTutorial: Bool` フラグ | 既存 `startGame()` をチュートリアル後に呼ぶ前段で `startPrologueFloor()` を 1 回挟む。内部で固定シード `(player: 5, enemy: 1)` を直接プロパティセット。`StageManager` には触らない |
| `Services/StageManager.swift` | (**変更なし**) | 既存 4 getter は floor 引数を受け取って値を返すだけなので、`getBPM(for: 0)` 等は `Floor.calculateBPM(for: 0)` が 60 を返せば自然に動く。インスタンスメソッド追加は不要 |
| `Services/AIEngine.swift` | (**呼び出し側変更**) | Floor 0 では `.easy` を渡し、AIEngine 内部の追跡確率はそのまま (`pursueProbability` は AILevel ベース)。AIEngine 自体のコード変更は不要、`GameViewModel.startPrologueFloor` が呼び出し時に `.easy` を選ぶだけ |

---

## 5. 心拍音 + 振動 (会議 第 16 回 / Step 3 連動)

### 前提: BeatEngine と衝突しない設計

⚠️ **重要**: 既存の `BeatEngine.swift` は毎ビートで `UIImpactFeedbackGenerator(.light)` を発火している (BeatEngine.swift:205 付近)。さらに Step 3 中はゲーム進行中の BPM (Floor 0 → 60 BPM) と心拍音 (60 BPM) が並走すると **異周波の重ね合わせで音が濁る**。

これを避けるため、**Step 3 中は BeatEngine を一時停止** し、心拍音と心拍振動を**専用パスで再生**する:

```swift
// Step 3 開始時:
beatEngine.suspend()  // 新規メソッド: timer.invalidate() + tactileEnabled = false
audioManager.playLoopingSFX(.heartbeat)  // 新規メソッド (後述)

// Step 3 終了時:
audioManager.stopLoopingSFX(.heartbeat)
beatEngine.resume()
```

### サウンド (AudioManager に loop API を新設)

現行 `AudioManager` は `playSoundEffect(_:)` 1 ショット再生のみで loop 引数なし。心拍音用に新規 API:

```swift
// AudioManager.swift に追加 (SoundEffect enum に case heartbeat も追加)
extension AudioManager {
    func playLoopingSFX(_ effect: SoundEffect) { /* AVAudioPlayer.numberOfLoops = -1 */ }
    func stopLoopingSFX(_ effect: SoundEffect) { /* player.stop() */ }
}
```

```
ファイル: heartbeat_low.wav (60 BPM、低音ピアノ単音、約 4 秒で 4 拍)
配置: EscapeNine-endless-/EscapeNine-endless-/Sounds/SFX/
SoundEffect enum: `case heartbeat = "heartbeat_low"`
```

### 振動 (BeatEngine 既存振動と二重発火させない)

Step 3 中は BeatEngine を suspend してあるので、振動も心拍音用に専用ループで発火:

```swift
// OnboardingTutorialView の Step 3 で:
private var heartbeatTimer: Timer?

func startStep3Heartbeat() {
    let generator = UIImpactFeedbackGenerator(style: .light)
    generator.prepare()
    heartbeatTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { _ in
        generator.impactOccurred()  // 60 BPM = 1 秒間隔
    }
}

func stopStep3Heartbeat() {
    heartbeatTimer?.invalidate()
    heartbeatTimer = nil
}
```

⚠️ `beatEngine.$currentBeat.sink { ... }` 経由ではなく**独立タイマー**で発火することで、BeatEngine 既存の毎ビート振動と二重発火を回避。

### アクセシビリティ (会議第 18 回 Lisa + multi-agent-reviewer HIGH-1 反映)

Step 3 の三層演出 (心拍音 + 振動 + 赤フラッシュ 0.2 秒) に対して、**すべての層で独立に opt-out 可能**にする (WCAG 2.3.1 / Apple HIG Accessibility / 光感受性てんかん・PTSD ユーザー配慮)。

| 演出層 | opt-out 条件 | 動作 |
|---|---|---|
| **振動** | `UIAccessibility.isReduceMotionEnabled == true` | `heartbeatTimer` を作らない (毎ビート振動なし) |
| **赤フラッシュ (0.2 秒)** | `UIAccessibility.isReduceMotionEnabled == true` | フラッシュ層自体を skip し、危険圏オーバーレイの色強調のみで代替 |
| **心拍音** | `audioManager.isMuted == true` OR 効果音音量 = 0 OR `prefersCrossFadeTransitions == true` | `playLoopingSoundEffect(.heartbeat)` 自体を呼ばない |
| **Step 3 全体** | ユーザーのスキップタップ (3 タップ or 長押し 1 秒) | Step 3 を skip し Step 4 へ。`eg_tutorial_step_completed(step_number: 3, skipped: true)` 送信 |

**事前予告**: Step 3 開始の 1.5 秒前に「次の Step は強い演出が含まれます」と予告 (上記 §3 Step 3 の手順 0 参照)。Reduce Motion ON or 効果音音量 0 のユーザーは予告ステップ自体を skip して直接 Step 4 のキー要素 (移動 → クリア) を体験させる。

### 触覚フィードバック ON/OFF スイッチ (Acceptance Criteria 格上げ)

`SettingsView.swift` の現状確認:

```bash
grep -rn "触覚\|haptic\|Haptic" EscapeNine-endless-/EscapeNine-endless-/Views/Settings/
```

- ✅ 既存 → スイッチを尊重し、ON のみ Step 3 振動を発火
- ❌ 未存在 → **v1.1 と同 Sprint で必ず新規追加** (`@AppStorage("hapticsEnabled")` + SettingsView 行追加、推定 0.3 日)。Sprint 1.x 送りは禁止 (v1.1 で振動が新規増加するため同時投入が必須)。

### VoiceOver / Dynamic Type / 色覚多様性 (Lisa 第 18 回 必須対応 8 項目)

- **Dynamic Type**: チュートリアル全テキストは `.font(.body)` / `.font(.title)` 等のセマンティックフォント使用、固定 pt 禁止
- **VoiceOver**: 各 Step の状態を `accessibilityValue` でナレーション (例: "Step 2 / 4: 影を避けてください")
- **アイコンボタン**: `.accessibilityLabel("スキップ")` を必ず付与
- **色覚多様性**: 危険圏オーバーレイは「赤い色」だけでなく **パターン (斜線) + アイコン (⚠️)** を併用
- **Bold Text**: システム設定 ON で `.fontWeight(.bold)` 反映、太字対応フォント使用
- **Reduce Transparency**: 赤フラッシュは半透明使わず単色背景に切替

---

## 6. 既存 migration との関係

### Sprint 1 で実装済の migration (HomeView.swift:85-89)

```swift
// 旧キー `tutorialCompleted` → 新キー `hasSeenTutorial` の一回限り migration
if !hasSeenTutorial && UserDefaults.standard.bool(forKey: "tutorialCompleted") {
    hasSeenTutorial = true
}
```

### v1.1 で追加する新 migration

```swift
@AppStorage("hasSeenTutorial") private var hasSeenTutorial: Bool = false        // Sprint 1
@AppStorage("hasSeenTutorialV1_1") private var hasSeenTutorialV1_1: Bool = false // v1.1 新規

// onAppear 内:
// Sprint 1 既存 migration (tutorialCompleted → hasSeenTutorial) はそのまま継承
if !hasSeenTutorial && UserDefaults.standard.bool(forKey: "tutorialCompleted") {
    hasSeenTutorial = true
}

// v1.1: hasSeenTutorialV1_1 が false なら表示 (hasSeenTutorial の値は無視)
if !hasSeenTutorialV1_1 {
    showOnboardingTutorial = true
}
```

### キー併存ルール (両キーの真偽値の組み合わせ)

| `hasSeenTutorial` | `hasSeenTutorialV1_1` | 状態 | 挙動 |
|---|---|---|---|
| false | false | 完全新規ユーザー | v1.1 を表示 → 完了時 **両方 true にセット** |
| true | false | Sprint 1 既存ユーザー (v1.1 未通過) | v1.1 を表示 → 完了時 `hasSeenTutorialV1_1` のみ true |
| - | true | v1.1 を通過済み | 表示しない (`hasSeenTutorial` の値は無視) |

> **完了時の更新ルール**:
> - 完全新規 (両方 false) → `hasSeenTutorial = true; hasSeenTutorialV1_1 = true` (Sprint 1 静的版を skip した扱いだが旧版へ戻し対応として両方立てる)
> - Sprint 1 既存 → `hasSeenTutorialV1_1 = true` のみ (Sprint 1 完了履歴は保持)
> - 「将来 v1.2 動的版が出る場合は `hasSeenTutorialV1_2` を追加し、本書と同じパターンで世代管理する」

### 既存ユーザーへの配慮

- Sprint 1 既存ユーザーも v1.1 を 1 回通る (新コンテンツとして)
- スキップ不可だが、4 ステップ計 40-60 秒程度に収める (短縮版より長いが、本質的に「初体験」)
- ヒヤリ演出と「禁書を盗んだ」ナラティブは初見の方が効くので、既存ユーザーにも価値あり

---

## 7. 実装タスク分解

### 新規 SwiftUI View

| ファイル | 役割 | 工数 |
|---|---|---|
| `Views/Onboarding/OnboardingTutorialView.swift` | 4 ステップを統括する親 View | 1 日 |
| `Views/Onboarding/TutorialHighlightView.swift` | 光るリング (タップ可能マスを示す) | 0.5 日 |
| `Views/Onboarding/DangerZoneView.swift` | 敵の隣接 8 マスに赤いオーバーレイ | 0.5 日 |
| `Views/Onboarding/TutorialStepInstructionView.swift` | 上部の説明文表示 (各 Step) | 0.5 日 |

### 既存 View の修正

| ファイル | 修正内容 | 工数 |
|---|---|---|
| `Views/Home/HomeView.swift` | `showTutorial` → `showOnboardingTutorialV1_1` に切替、migration 追加 | 0.5 日 |
| `Views/Game/GameView.swift` | チュートリアルモードのフラグ追加、敵 hidden / ターン短縮 | 1 日 |
| `ViewModels/GameViewModel.swift` | チュートリアル用シードバイアス、Floor 0 ロジック | 1 日 |
| `Services/StageManager.swift` | Floor 0 プロローグ追加 | 0.5 日 |

### サウンド・触覚

| 項目 | 工数 |
|---|---|
| heartbeat_low.wav 制作 (Suno AI or 既存音源) | 0.5 日 |
| AudioManager.swift に heartbeat 追加 | 0.5 日 |
| 振動連動 (UIImpactFeedbackGenerator + BeatEngine subscribe) | 0.5 日 |
| Reduce Motion 対応 | 0.5 日 |

### テスト

| 項目 | 工数 |
|---|---|
| ステップごとのプレイテスト | 1 日 |
| 既存ユーザー migration テスト (Sprint 1 通過済ユーザーが v1.1 を通るか) | 0.5 日 |
| Reduce Motion / VoiceOver 動作確認 | 0.5 日 |

### 合計工数

**実装 5-6 日、テスト 2 日 = 計 7-8 日** (Sprint 2 内で並行作業可)

---

## 8. リスクと対策

| リスク | 対策 |
|---|---|
| チュートリアルが長すぎて離脱 | 全 4 ステップで合計 40-60 秒以内に収める、各ステップ 15 秒上限 |
| ヒヤリ演出が怖すぎる / うるさい | Step 3 の心拍音は 5 秒以内、フラッシュは 0.2 秒、強度ユーザーテストで調整 |
| 既存ユーザーが「また通らされる」と不満 | v1.1 リリースノートで「世界観オープニング追加」と訴求、スキップ不可だが短くする |
| Sprint 2 機能 (デイリー / 抜かれ通知) と工数競合 | v1.1 はオンボーディング単独で別 PR、Sprint 2 後半で着手 |
| Reduce Motion 無効化忘れ | 第 18 回 Lisa の必須対応 8 項目に含めて自動テスト追加 |

---

## 9. 関連ドキュメント

- 会議録: Obsidian `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド)` 第 2 回 (オンボーディング再設計) / 第 3 回 (1 階の壁) / 第 12 回 (結論) / 第 15 回 (世界観) / 第 16 回 (オーディオ) / 第 18 回 (アクセシビリティ)
- Sprint 2 計画: Obsidian `2026-05-11 Escape Nine Sprint 2 計画書 draft`
- Sprint 1 リリースノート: Obsidian `2026-05-11 Escape Nine v1.5.1 リリースノート (Sprint 1)`
- 既存実装: `EscapeNine-endless-/EscapeNine-endless-/Views/Home/TutorialOverlayView.swift` (Sprint 1 静的版)
- 既存 migration: `EscapeNine-endless-/EscapeNine-endless-/Views/Home/HomeView.swift:85-89`
- ゲーム仕様: `docs/game-spec.md` (キャラ・BPM・特殊ルール)

---

## 10. 着手判断

**実装着手は Sprint 3 開始時点**、以下の 4 条件を全て満たしてから (HIGH-3 反映で Sprint 2 後半案は取り下げ):

- [ ] Sprint 1 が TestFlight に提出済 (Phase 6 完了)
- [ ] Sprint 1 KPI ベースライン取得済 (`docs/aso/sprint-1-baseline.md` が main にマージ済み、§1 のベースライン取得手順 4 項目を実施)
- [ ] **Sprint 2 (デイリー / 抜かれ通知 / シェア拡張) 完了済 + Sprint 2 振り返り終了**
- [ ] Obsidian 「Sprint 2 計画書 draft」を「Sprint 2 完了報告」に昇格させ、本書を「Sprint 3 計画書」と同期 (ユーザー手動)

4 条件が揃ったら `feature/onboarding-v1.1` ブランチで実装着手。

### multi-agent-reviewer HIGH 指摘反映ステータス (2026-05-14)

| ID | 指摘 | 反映先 | ステータス |
|---|---|---|---|
| HIGH-1 | 心拍音 + 振動 + 赤フラッシュの opt-out が振動のみ | §3 Step 3 手順 0、§5 アクセシビリティ表 | ✅ 反映 |
| HIGH-2 | Step 3 スキップ動線なし (強制通過のメンタル負荷) | §3 Step 3 スキップ動線 + Analytics 分離 | ✅ 反映 |
| HIGH-3 | Sprint 2 計画書との工数競合 | 冒頭メタ + 本 §10 (Sprint 3 単独 PR へ) | ✅ 反映 |
| HIGH-4 | StageManager 構造誤認 + Floor.calculateBPM の clamp(1, ...) | §4 「実装変更点」表を全面書き換え | ✅ 反映 |
