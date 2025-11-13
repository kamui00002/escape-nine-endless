# 🎮 Escape Nine: Endless

音ゲー × 9マス鬼ごっこ エンドレスチャレンジ

## 📖 概要

**Escape Nine: Endless** は、9マスの盤面で音楽のビートに合わせて逃げ続ける、ハイスピードなエンドレスチャレンジゲームです。

### ✨ 特徴

- 🎵 **リズムゲーム要素**: 音楽のビートに合わせて移動
- 🏃 **エンドレスチャレンジ**: 100階層までのスコアアタック
- ⚡ **徐々に加速**: 90階層付近は人間限界の超高速
- 🎯 **シンプル**: スタミナ・コイン・複雑な要素なし
- 🎨 **ドット絵**: 64×64ピクセルのレトロ風ビジュアル

### 🎮 基本ルール

1. 3×3 = 9マスの盤面
2. 音楽のビート(ドン・ドン・ドン)に合わせて移動
3. タイミングから外れたら即アウト
4. 10ターン逃げ切れば次の階層へ
5. 階層が上がるとBPMが加速(60→240)

## 🛠️ 技術スタック

- **フレームワーク**: SwiftUI
- **言語**: Swift 5.9+
- **アーキテクチャ**: MVVM + Combine
- **音楽**: AVFoundation
- **バックエンド**: Firebase
- **広告**: Google Mobile Ads SDK
- **課金**: StoreKit
- **ランキング**: Game Center

## 📁 プロジェクト構造

```
escape-nine-endless/
├── EscapeNine-endless-/
│   ├── EscapeNine-endless-/
│   │   └── (Swift source files)
│   └── EscapeNine-endless-.xcodeproj/
├── docs/
│   ├── 要件定義書_EscapeNine.md      # 詳細な要件定義
│   └── DEVELOPMENT_SWIFT.md          # 開発者向け技術仕様
└── README.md                          # このファイル
```

## 🚀 開発開始

### セットアップ

1. Xcodeでプロジェクトを開く
   ```bash
   open EscapeNine-endless-/EscapeNine-endless-.xcodeproj
   ```

2. シミュレーターまたは実機で実行
   - Xcodeで `⌘ + R` を押すか、実行ボタンをクリック

詳細な開発手順は [DEVELOPMENT_SWIFT.md](./docs/DEVELOPMENT_SWIFT.md) を参照してください。

## 📝 ドキュメント

- [要件定義書](./docs/要件定義書_EscapeNine.md) - ゲームの詳細仕様
- [DEVELOPMENT_SWIFT.md](./docs/DEVELOPMENT_SWIFT.md) - 開発者向け技術仕様

## 🎯 開発ロードマップ

### MVP版 (初回リリース)

- ✅ 要件定義完了
- ⬜ プロジェクトセットアップ
- ⬜ 音楽同期システム実装
- ⬜ 基本ゲームロジック
- ⬜ UI/UX実装
- ⬜ 収益化システム
- ⬜ テスト・調整
- ⬜ App Storeリリース

## 📱 対応プラットフォーム

- **iOS**: 14.0以上
- **将来的**: Android対応予定

## 📄 ライセンス

© 2025 Souatou. All rights reserved.

---

**開発開始日**: 2025-11-13
**開発者**: Souatou