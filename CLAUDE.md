# Escape Nine: Endless - プロジェクト概要

## 基本情報

- **タイトル**: Escape Nine: Endless
- **ジャンル**: 音ゲー × 戦略型エンドレスチャレンジ
- **プラットフォーム**: iOS (SwiftUI)
- **アーキテクチャ**: MVVM + Combine

## コンセプト

9マスの盤面で音楽のビートに合わせて逃げ続けるハイスピードエンドレスチャレンジ。
- 3×3グリッドでプレイヤーと鬼が同時移動
- 10ターン逃げ切りで階層クリア
- BPMが階層ごとに加速（60→240）
- 100階層到達が目標

## ディレクトリ構造

```
EscapeNine-endless-/EscapeNine-endless-/
├── Models/
│   ├── Character.swift      # キャラクター定義（勇者/盗賊/魔法使い/エルフ）
│   ├── Floor.swift          # 階層・特殊ルール
│   ├── GameState.swift      # ゲーム状態管理
│   └── Skill.swift          # スキル定義
├── Views/
│   ├── Game/                # ゲーム画面
│   │   ├── GameView.swift
│   │   ├── GridBoardView.swift
│   │   ├── GridCellView.swift
│   │   ├── BeatIndicatorView.swift
│   │   └── BPMInfoView.swift
│   ├── Home/HomeView.swift
│   ├── Character/CharacterSelectionView.swift
│   ├── Ranking/RankingView.swift
│   ├── Settings/SettingsView.swift
│   └── Result/ResultView.swift
├── ViewModels/
│   ├── GameViewModel.swift   # ゲームロジック
│   ├── PlayerViewModel.swift # プレイヤーデータ
│   └── RankingViewModel.swift
├── Services/
│   ├── AIEngine.swift       # 鬼AI（Easy/Normal/Hard）
│   ├── BeatEngine.swift     # ビート同期
│   ├── GameEngine.swift     # ゲームエンジン
│   ├── StageManager.swift   # ステージ管理
│   └── RankingService.swift
└── Utilities/
    ├── Constants.swift      # 定数・カラーパレット
    ├── Fonts.swift
    ├── AnimationEffects.swift
    └── ResponsiveLayout.swift
```

## 要件定義書

`docs/要件定義書_EscapeNine.md` に詳細仕様あり

## キャラクター仕様

| キャラ | スキル | 回数 | 解放条件 |
|--------|--------|------|----------|
| 勇者 | ダッシュ（2マス移動） | 5回 | 初期 |
| 盗賊 | 斜め移動 | 5回 | 階層10クリア |
| 魔法使い | 透明化（無敵） | 5回 | 有料¥240 |
| エルフ | 拘束（鬼を1ターン停止） | 5回 | 有料¥240 |

## 特殊ルール

- **霧マップ**（階層21-40）: 周囲が見えない
- **マス消失**（階層41-60）: ランダムで1マス消える
- **霧+消失**（階層61-100）: 両方発動

## 現在の開発状況

### 完了
- [x] 基本ゲームロジック（移動・当たり判定）
- [x] 3×3グリッドUI
- [x] キャラクター選択画面
- [x] スキルシステム実装
- [x] AI（Easy/Normal/Hard）
- [x] 階層システム・特殊ルール
- [x] 設定画面（BGM/効果音音量分離）
- [x] ランキング画面UI
- [x] レスポンシブレイアウト

### 未実装（優先度順）
- [ ] BGM・効果音の実装（Suno AI）
- [ ] ビート同期システムの完成
- [ ] Firebase連携（認証・Firestore）
- [ ] AdMob広告（バナー・インタースティシャル）
- [ ] StoreKit課金（キャラ購入・広告削除）
- [ ] Game Center連携（ランキング）

## ビルド・実行

```bash
# シミュレータでビルド
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build
```

## カラーパレット

- メイン: #667eea（紫青）
- アクセント: #764ba2（濃い紫）
- 背景: #1a1a2e（ダーク）
- テキスト: #eaeaea
- 警告: #f03e3e
- 成功: #37b24d

## 注意事項

- 最小iOS: iOS 14.0（現在はiOS 26.0でビルド）
- ドット絵: 64×64ピクセル
- スキル使用回数: 全キャラ5回/試合
