# scripts/

EscapeNine の運用支援スクリプト集。

## 一覧

| スクリプト | 用途 | 引数 |
|---|---|---|
| `generate-appicon.sh` | 汎用 AppIcon 生成 (任意背景色) | `<input.png> [<output-dir>]` |
| `generate-appicon-escapenine.sh` | EscapeNine 専用 wrapper (Deep Brown 背景固定) | `<input.png>` |
| `auto-git-push.sh` | (既存) 自動 push スクリプト | - |

---

## generate-appicon.sh

ChatGPT/DALL-E 等で生成した画像から iOS AppIcon を自動生成。

### 機能

1. 余白の **trim** (ChatGPT 生成時の白パディング除去)
2. **1024×1024 square** にパディング (背景色は環境変数で指定)
3. **透明部分を背景色で塗りつぶし** (Apple HIG 準拠、no transparency)
4. **Xcode 16+ Contents.json** 自動生成 (universal + dark + tinted の 3 appearance)

### 必要なもの

```bash
brew install imagemagick
```

### 使い方

```bash
# デフォルト (背景色 #2A1F18 Deep Brown)
./scripts/generate-appicon.sh ~/Downloads/icon-source.png \
  EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/AppIcon.appiconset

# ParkPedia 用 (緑背景)
ICON_BG="#22C55E" ./scripts/generate-appicon.sh ~/Downloads/parkpedia-icon.png \
  /path/to/ParkPedia/Assets.xcassets/AppIcon.appiconset

# 任意の HEX カラーで
ICON_BG="#FF5733" ./scripts/generate-appicon.sh input.png ./output
```

### 出力

```
output/
├── AppIcon.png       (1024x1024、square、塗りつぶし済)
└── Contents.json     (Xcode 16+ 3 appearance 形式)
```

---

## generate-appicon-escapenine.sh

EscapeNine 専用 wrapper。背景色を **Deep Brown #2A1F18** (会議録第14回 田村ブランドガイドの主背景色) で固定し、EscapeNine の AppIcon.appiconset に直接書き出す。

### 使い方

```bash
./scripts/generate-appicon-escapenine.sh ~/Downloads/escape-nine-icon-1024.png
```

→ `EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/AppIcon.appiconset/AppIcon.png` が更新される。

### 続いて

```bash
# Xcode で確認
open EscapeNine-endless-/EscapeNine-endless-.xcodeproj

# Build
xcodebuild -project EscapeNine-endless-/EscapeNine-endless-.xcodeproj \
  -scheme "EscapeNine-endless-" -sdk iphonesimulator build
```

---

## トラブルシューティング

### `magick: command not found`
ImageMagick 未インストール。`brew install imagemagick`

### 出力画像が真っ黒 / 真っ白
`-alpha remove` の挙動で透明部分が `BG_COLOR` で塗りつぶされる。元画像が完全透明 PNG だと全面塗りつぶしになるので、**元画像はある程度色がついた状態**で渡すこと。

### Xcode で AppIcon が反映されない
1. Xcode を一度閉じる
2. `~/Library/Developer/Xcode/DerivedData/` の該当プロジェクトを削除
3. Xcode 再起動 → ⌘B でクリーンビルド

### 角丸が iOS 自動角丸 (~22%) と合わない
このスクリプトは square 1024×1024 を生成。**iOS が自動で角丸にする**ので、入力画像は square (角無し) で OK。事前に角丸付き画像を作る必要なし。

---

## 将来の拡張 (Sprint 3 候補)

- `generate-appicon-3mode.sh`: 通常 / Dark Mode / Tinted Mode 個別書き出し対応
  - 例: 通常版 (黒背景) / Dark 版 (より暗い背景) / Tinted 版 (グレースケール)
- `generate-marketing-screenshots.sh`: App Store スクショ自動生成 (会議録第8回 5 枚)
- `lottie-icon-export.sh`: ロゴモーション (1.2 秒、会議録第14回 木村案) の Lottie 出力

---

## 参考

- [Apple HIG: App Icons](https://developer.apple.com/design/human-interface-guidelines/app-icons)
- 会議録: `2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26名 35ラウンド).md` 第14回
- ASO Sprint 1: `docs/aso/sprint-1-improvements.md` (アイコン A/B テスト案)
