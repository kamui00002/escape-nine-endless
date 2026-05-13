# オンボーディング v1.1 動的チュートリアル — 設計書 ⭐️

作成日: 2026-05-14
ステータス: **設計フェーズ (実装未着手)**
対象リリース: v1.1 (Sprint 2 後半 or Sprint 3)
依存元: Sprint 1 で実装済の `TutorialOverlayView.swift` (静的 3 ページ版)

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

| KPI | 現状 (推定) | 目標 (v1.1 反映後 2 週間) |
|---|---|---|
| **1 階離脱率** | 50%+ | **30% 以下** |
| **Day 1 リテンション** | 不明 (Sprint 1 で計測開始) | +10pp |
| **チュートリアル完了率** | 不明 | 80%+ |
| **チュートリアル後 → 1 階クリア率** | - | 90%+ |

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
  1. 上部に文字: 「あと 1 マスで影に飲まれる」
  2. 心拍音 (60 BPM) を AudioManager で開始
  3. 軽い振動 (UIImpactFeedbackGenerator.light) を毎ビート
  4. プレイヤーを 1 回動かす → 敵から離れる
  5. 「ヒヤリとした。これがこのゲームだ。」 → Step 4 へ

実装ポイント:
- 心拍音: 低音ピアノ単音 (松本さん指摘の 16 回オーディオ「沈黙より一歩先」階層 1-3 と整合)
- 振動: `UIImpactFeedbackGenerator(style: .light).impactOccurred()` を毎ビートで
- 「ヒヤリ」演出: 画面を一瞬赤くフラッシュ (0.2 秒) + 危険圏を強調
- このステップは "脳に焼きつく瞬間" なので 5-10 回テストプレイで体験の強さを調整
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

### 実装変更点

- `Models/Floor.swift`: `Floor.prologue` (Floor 0) を追加
- `Services/StageManager.swift`: チュートリアル直後のフロー分岐
- `ViewModels/GameViewModel.swift`: 初回限定シードバイアスロジック

---

## 5. 心拍音 + 振動 (会議 第 16 回 / Step 3 連動)

### サウンド

```
ファイル: heartbeat_low.wav (60 BPM、低音ピアノ単音)
配置: Resources/Sounds/SFX/
再生: AudioManager.shared.playSFX(.heartbeat, loop: true)
停止: Step 3 終了時に AudioManager.shared.stopSFX(.heartbeat)
```

### 振動

```swift
// Step 3 開始時に毎ビートで:
let generator = UIImpactFeedbackGenerator(style: .light)
generator.prepare()
// BeatEngine.currentBeat の publisher にサブスクライブして毎ビート発火
beatEngine.$currentBeat
    .sink { _ in generator.impactOccurred() }
    .store(in: &cancellables)
```

### 注意点

- アクセシビリティ: `UIAccessibility.isReduceMotionEnabled` が true なら振動を無効化 (第 18 回 Lisa の指摘)
- 設定画面: 「触覚フィードバック ON/OFF」スイッチが既に存在なら尊重、なければ新規追加検討

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
// 旧 Sprint 1 静的チュートリアルは通ったが、v1.1 プレイアブルはまだの場合 → v1.1 を表示する
if !hasSeenTutorialV1_1 {
    showOnboardingTutorial = true
}
```

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

実装着手は以下の 3 条件を満たしてから:

- [ ] Sprint 1 が TestFlight に提出済 (Phase 6 完了)
- [ ] Sprint 1 KPI ベースライン取得済 (`docs/aso/sprint-1-baseline.md`)
- [ ] Sprint 2 機能 (デイリー / 抜かれ通知 / シェア拡張) の優先度判断完了

3 条件が揃ったら `feature/onboarding-v1.1` ブランチで実装着手。
