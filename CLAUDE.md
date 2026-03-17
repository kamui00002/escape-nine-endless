# Escape Nine: Endless - プロジェクト概要

## 基本情報

- **タイトル**: Escape Nine: Endless
- **ジャンル**: 音ゲー x 戦略型エンドレスチャレンジ
- **プラットフォーム**: iOS (SwiftUI, iOS 26.0+)
- **アーキテクチャ**: MVVM + Combine

## コンセプト

9マスの盤面で音楽のビートに合わせて逃げ続けるハイスピードエンドレスチャレンジ。
- 3x3グリッドでプレイヤーと鬼が同時移動
- 10ターン逃げ切りで階層クリア
- BPMが階層ごとに加速（70→200、べき乗曲線）
- 100階層到達が目標

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

## 要件定義書

`docs/要件定義書_EscapeNine.md` に詳細仕様あり

## キャラクター仕様

| キャラ | スキル | 回数 | 解放条件 |
|--------|--------|------|----------|
| 勇者 | ダッシュ（2マス移動） | 3回 | 初期 |
| 盗賊 | 斜め移動 | 5回 | 階層10クリア |
| 魔法使い | 透明化（衝突時に無敵） | 7回 | 有料¥240 |
| エルフ | 拘束（鬼を2ターン停止） | 4回 | 有料¥240 |

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

## 現在の開発状況

### 完了（コード実装済み）
- [x] 基本ゲームロジック（移動・当たり判定・同時移動）
- [x] 3x3グリッドUI + レスポンシブ対応
- [x] キャラクター選択画面 + 4キャラスキル
- [x] AI（Easy/Normal/Hard）+ 階層スケーリング
- [x] BPMべき乗曲線（70→200）
- [x] 階層システム + 特殊ルール（霧/消失/複合）
- [x] ターンカウントダウンシステム（3→2→1 → 移動）
- [x] ゲーム開始カウントダウン（3→2→1→GO!）
- [x] 敗因表示（捕まった/時間切れ）
- [x] メトロノームビートシステム（AVAudioEngine）
- [x] 効果音8種（.wav）
- [x] 設定画面（BGM/効果音音量分離）
- [x] ランキング（ローカル永続化）
- [x] 実績システム（9種）
- [x] チュートリアル6ページ（図解付き）
- [x] ショップ画面
- [x] StoreKit 2（ローカルテスト設定済み）
- [x] Game Center連携（コード実装済み）
- [x] Firebase/AdMob（モック実装・本番切替準備済み）
- [x] デバッグ管理システム（開始階層/BPM/AI/カウントダウン設定）
- [x] 定数一元管理（Constants.swift）

### 外部設定が必要（コードだけでは完了不可）
- [ ] Firebase: GoogleService-Info.plist + SPMパッケージ
- [ ] AdMob: Google Mobile Ads SDK + 本番広告ID
- [ ] App Store Connect: StoreKit実プロダクトID
- [ ] Game Center: リーダーボードID登録
- [ ] BGM音楽ファイル（Suno AI等で生成）

## ビルド・実行

```bash
# シミュレータでビルド
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build
```

## カラーパレット（冒険ファンタジー系）

- メイン: #f4a460（サンディブラウン）
- アクセント: #daa520（ゴールデンロッド）
- 背景: #2c1810（ダークブラウン）
- テキスト: #f5deb3（ベージュ）
- 金テキスト: #ffd700（ゴールド）
- 警告: #ff6347（トマトレッド）
- 成功: #90ee90（ライトグリーン）

## 注意事項

- ビルドターゲット: iOS 26.0
- ドット絵: 64x64ピクセル
- スキル使用回数: キャラ別（勇者3/盗賊5/魔法使い7/エルフ4）
- 10階層ごとにスキルリセット
- SourceKitのfalse positiveエラーあり（ビルドは成功する）
