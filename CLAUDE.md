# Escape Nine: Endless ⭐️

9マスの盤面で音楽のビートに合わせて逃げ続けるハイスピードエンドレスチャレンジ（iOS）。

## 基本情報

| 項目 | 値 |
|---|---|
| タイトル | Escape Nine: Endless |
| ジャンル | 音ゲー × 戦略型エンドレスチャレンジ |
| プラットフォーム | iOS 26.0+（SwiftUI） |
| アーキテクチャ | MVVM + Combine |

## コンセプト

- 3x3グリッドでプレイヤーと鬼が同時移動
- 10ターン逃げ切りで階層クリア
- BPMが階層ごとに加速（70→200、べき乗曲線）
- 100階層到達が目標

## Tech Stack（要点）

- Swift 5.9+ / SwiftUI / Combine
- AVFoundation（音楽同期が最重要）
- GameKit（Game Center）/ StoreKit 2（課金）
- Firebase（Auth / Firestore / Analytics）/ Google Mobile Ads
- 詳細は @docs/DEVELOPMENT_SWIFT.md を参照

## 関連ドキュメント（@記法で展開）

- 要件定義: @docs/要件定義書_EscapeNine.md
- 開発仕様: @docs/DEVELOPMENT_SWIFT.md
- ゲーム仕様詳細（キャラ・BPM・特殊ルール・ディレクトリ・カラーパレット）: @docs/game-spec.md
- 収益化（Firebase / AdMob / StoreKit / Game Center）: @docs/収益化設定ガイド.md
- SDK残タスク: @docs/SDK修正_残タスク手順書.md
- App Store メタデータ: @docs/appstore-metadata.md
- リリース進捗・残作業チェックリスト: @docs/release-checklist.md
- プライバシーポリシー: @docs/privacy-policy.html

## 🚀 TestFlight Upload (ASC API Key 自動化、2026-05-17 整備)

ASC API Key 経由の自動 upload セットアップ済 ([[2026-05-17 App Store Connect API Key で TestFlight 自動 upload セットアップ]] 参照、Obsidian リファレンス)。

「TestFlight 出して」と指示されたら以下を自動実行:

```bash
make testflight
# 内部実行: ~/.claude/scripts/xcode-testflight-upload.sh EscapeNine-endless-/EscapeNine-endless-.xcodeproj EscapeNine-endless-
```

フロー:
1. `CURRENT_PROJECT_VERSION` を +1
2. `xcodebuild archive` (Release / generic iOS)
3. `xcodebuild -exportArchive` で IPA 生成 (ExportOptions.plist は repo 直下)
4. `xcrun altool --upload-app` で App Store Connect に提出

必要前提 (1 回だけセットアップ済):
- `~/.appstoreconnect/private_keys/AuthKey_{KEY_ID}.p8` (chmod 600)
- macOS Keychain: `secret save ASC_KEY_ID` / `secret save ASC_ISSUER_ID`
- `ExportOptions.plist` (team `B7F79FDM78`, method `app-store-connect`) ← 本 PR で追加

その他コマンド:
- `make build-only` — build bump + archive + IPA だけ (upload しない)
- `make no-bump` — build 番号据え置きで再 archive + upload

## ビルド・実行

```bash
# シミュレータでビルド
xcodebuild -scheme EscapeNine-endless- \
  -destination 'id=5B405E6E-0F9F-4715-A97C-D2E85987CB53' \
  build
```

- ビルドターゲット: iOS 26.0
- SourceKit の false positive エラーが出ることがある（ビルド自体は成功する）

## iPad レイアウトルール（厳守）

### 原則: 比率ベースレイアウト

- **固定ptサイズ禁止** — iPad/iPhone で異なる固定値を使わず、`GeometryProxy` の比率で計算する
- **`ResponsiveLayout` 経由** — サイズ・スペーシング・パディングは `ResponsiveLayout` のメソッドを使う
- **新しいレイアウト値の追加時** — `ResponsiveLayout` にメソッドを追加し、View 内で直接 `isIPad()` 分岐しない

### 確認必須項目

- UI 変更時は必ず `#Preview("iPad")` で iPad 表示を確認する
- 主要 View（GameView, HomeView, ShopView, RankingView, CharacterSelectionView）には iPhone/iPad 両方の `#Preview` が定義済み
- グリッドやボタンが画面からはみ出していないか、要素同士が重なっていないかを確認する

### ResponsiveLayout の使い方

```swift
// Good: ResponsiveLayout メソッド経由
.frame(maxHeight: ResponsiveLayout.gridMaxHeight(for: geometry))
.frame(maxWidth: ResponsiveLayout.gridMaxWidth(for: geometry))
let spacing = ResponsiveLayout.verticalSpacing(for: geometry)

// Bad: View 内で直接分岐
.frame(maxHeight: geometry.size.height * (ResponsiveLayout.isIPad() ? 0.30 : 0.40))
```

## 開発ルール・禁止事項

- バランス定数（BPM・スキル回数・階層スケーリング等）は `Utilities/Constants.swift` に一元管理
- Firestore／AdMob／StoreKit への直接アクセスは `Services/` 配下のサービスクラス経由に限定（View/ViewModel から直接呼ばない）
- ドット絵は 64x64 ピクセルで統一
- スキル使用回数は 10 階層ごとにリセット（勇者3/盗賊5/魔法使い7/エルフ4）
- 新規 View 追加時は `#Preview` を必ず定義（iPhone/iPad 両対応）

## Git 運用

- コンベンショナルコミット（feat:, fix:, docs:, test:, refactor:, chore:）／日本語メッセージ
- main への直接コミットは避ける
