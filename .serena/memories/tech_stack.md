# 技術スタック

## フレームワーク・言語
- **フレームワーク**: SwiftUI
- **言語**: Swift 5.9+
- **最小iOS**: iOS 14.0
- **現在のビルド環境**: iOS 26.0, Xcode 15.0+
- **アーキテクチャ**: MVVM + Combine

## プロジェクト構造
```
EscapeNine-endless-/
├── EscapeNine-endless-/
│   └── EscapeNine-endless-/
│       ├── Models/          # データモデル (Character, GameState, Floor, Skill)
│       ├── Views/           # SwiftUI Views (Home, Game, Character, Ranking, Settings, Result)
│       ├── ViewModels/      # ビューモデル (GameViewModel, PlayerViewModel, RankingViewModel)
│       ├── Services/        # ビジネスロジック (AIEngine, BeatEngine, GameEngine, StageManager, Firebase, AdMob, StoreKit)
│       ├── Utilities/       # ユーティリティ (Constants, Fonts, AnimationEffects, ResponsiveLayout)
│       └── Assets.xcassets/ # 画像・アセット
```

## 外部サービス・SDK
- **Firebase**:
  - Authentication (認証)
  - Firestore (ランキングデータ)
  - Analytics (分析)
- **Google Mobile Ads SDK**: AdMob広告
- **StoreKit**: アプリ内課金
- **Game Center**: ランキング

## オーディオ
- **フレームワーク**: AVFoundation
- **BGM**: Suno AIで作成予定（BPM変化対応）
- **効果音**: ビート音、移動音、ゲームオーバー音、階層クリア音

## グラフィック
- **スタイル**: ドット絵（ピクセルアート）
- **解像度**: 64×64ピクセル
- **アニメーション**: 最小限（静止画 + スライド移動）

## データ保存
- **ローカル**: UserDefaults / Core Data
  - 到達階層、解放キャラ、選択中のキャラ、AI難易度設定
- **クラウド**: Firebase Firestore
  - ランキングスコア
