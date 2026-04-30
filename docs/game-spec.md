# ゲーム仕様詳細 ⭐️

Escape Nine: Endless のゲームバランス・キャラクター・特殊ルール詳細。

要件定義は `要件定義書_EscapeNine.md` を参照。

---

## キャラクター仕様

| キャラ | スキル | 回数 | 解放条件 |
|--------|--------|------|----------|
| 勇者 | ダッシュ（2マス移動） | 3回 | 初期 |
| 盗賊 | 斜め移動 | 5回 | 階層10クリア |
| 魔法使い | 透明化（衝突時に無敵） | 7回 | 有料¥240 |
| エルフ | 拘束（鬼を2ターン停止） | 4回 | 有料¥240 |

- 10階層ごとにスキル使用回数がリセットされる

---

## ゲームバランス

### BPM曲線（べき乗: BPM = 70 + 130 x (floor/99)^1.4）

| Floor | BPM |
|-------|-----|
| 1     | 70  |
| 25    | ~95 |
| 50    | ~139|
| 75    | ~170|
| 100   | 200 |

### AI階層スケーリング

- Floor 1-15: 自然Easy
- Floor 16-35: 自然Normal
- Floor 36+: 自然Hard
- プレイヤー選択でさらに1段調整

### 特殊ルール

- 階層21-40: 霧マップ（視界制限）
- 階層41-60: マス消失（段階的: 1→2→3→4マス）
- 階層61-100: 霧+消失

### ターンシステム

- 1ターン = カウントダウン3→2→1 → 移動実行
- ゲーム開始時に3秒カウントダウン（3→2→1→GO!）
- 移動しなかった場合 → 時間切れゲームオーバー

---

## カラーパレット（冒険ファンタジー系）

- メイン: `#f4a460`（サンディブラウン）
- アクセント: `#daa520`（ゴールデンロッド）
- 背景: `#2c1810`（ダークブラウン）
- テキスト: `#f5deb3`（ベージュ）
- 金テキスト: `#ffd700`（ゴールド）
- 警告: `#ff6347`（トマトレッド）
- 成功: `#90ee90`（ライトグリーン）

---

## ディレクトリ構造

```
EscapeNine-endless-/EscapeNine-endless-/
├── Models/
│   ├── Character.swift      # キャラクター定義（勇者/盗賊/魔法使い/エルフ）
│   ├── Floor.swift          # 階層・BPM曲線・AI階層スケーリング
│   ├── GameState.swift      # ゲーム状態・AI難易度・敗因・特殊ルール
│   ├── Skill.swift          # スキル定義
│   └── Achievement.swift    # 実績システム
├── Views/
│   ├── Game/                # ゲーム画面
│   │   ├── GameView.swift
│   │   ├── GridBoardView.swift
│   │   ├── GridCellView.swift
│   │   ├── BeatIndicatorView.swift
│   │   └── BPMInfoView.swift
│   ├── Home/
│   │   ├── HomeView.swift
│   │   └── TutorialOverlayView.swift
│   ├── Character/CharacterSelectionView.swift
│   ├── Ranking/RankingView.swift
│   ├── Settings/SettingsView.swift
│   ├── Result/ResultView.swift
│   ├── Achievement/AchievementListView.swift
│   └── Components/          # 共通UIコンポーネント
├── ViewModels/
│   ├── GameViewModel.swift   # ゲームロジック・ターン管理
│   ├── PlayerViewModel.swift # プレイヤーデータ・デバッグ設定
│   └── RankingViewModel.swift
├── Services/
│   ├── AIEngine.swift       # 鬼AI（Easy/Normal/Hard）
│   ├── BeatEngine.swift     # ビート同期・ターンカウントダウン
│   ├── GameEngine.swift     # ゲームエンジン
│   ├── StageManager.swift   # ステージ管理
│   ├── AudioManager.swift   # BGM/効果音統合管理
│   ├── GameCenterService.swift # Game Center連携
│   ├── RankingService.swift # ローカルランキング
│   ├── FirebaseService.swift # Firebase（モック実装）
│   ├── AdMobService.swift   # AdMob広告（モック実装）
│   ├── StoreKitService.swift # StoreKit 2課金
│   └── PurchaseManager.swift # 課金管理
└── Utilities/
    ├── Constants.swift      # 全バランス定数・カラーパレット
    ├── Fonts.swift
    ├── AnimationEffects.swift
    └── ResponsiveLayout.swift
```
