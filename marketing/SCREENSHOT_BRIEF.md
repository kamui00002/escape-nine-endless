# App Store スクショ Generator 仕様書 (SCREENSHOT_BRIEF) ⭐️

`app-store-screenshots` skill に渡す事前確定回答。スキル起動時の Required 質問への回答を全部埋めてあるので、Claude はこのファイルを参照すれば対話なしで generator を組める。

最終更新: 2026-05-19

---

## 📋 スキル Step 1 — Required 質問への回答

### 1. App screenshots (実機キャプチャ PNG)

- **保存先**: `marketing/raw-screenshots/`
- **撮影予定** (`make-screenshots.sh capture-all` で取得):
  - `home.png` — ホーム画面
  - `gameplay_low.png` — Floor 1-10 / BPM 70-90
  - `gameplay_high.png` — Floor 70+ / BPM 170+
  - `result.png` — 階層クリア演出 / リザルト
  - `leaderboard.png` — ランキング / Game Center
  - `settings.png` — 設定 / ストア

### 2. App icon

- **パス**: `EscapeNine-endless-/EscapeNine-endless-/Assets.xcassets/AppIcon.appiconset/AppIcon.png`
- **解像度**: 1024×1024 (App Store 仕様)

### 3. Brand colors

| 用途 | カラー | コード |
|---|---|---|
| アクセント (CTA、強調語、矢印) | アラート赤橙 | `#FF6B35` |
| 背景グラデ Top | 深い宇宙 | `#0F0F1E` |
| 背景グラデ Mid | ダーク紫青 | `#1A1A2E` |
| 背景グラデ Bottom | ダークブルー | `#0F3460` |
| 本文テキスト | オフホワイト | `#F5F5F5` |
| 強調数字 (BPM, 100, 階層数) | ゴールド | `#FFD700` |
| サブテキスト | グレー | `#A0A0B0` |

### 4. Font

- **見出し (Headline)**: `Noto Sans JP Black` + `Bebas Neue` (英数字、緊張感)
- **本文**: `Noto Sans JP Bold`
- **数字 (BPM, 階層数)**: `Bebas Neue` または `Oswald` (細長、スピード感)
- フォールバック: `system-ui, -apple-system, sans-serif`
- Web フォント: Google Fonts から読み込み (`Noto Sans JP`, `Bebas Neue`)

### 5. Feature list (優先順位順)

1. **BPM 70 → 200 加速システム** (#1 最大の差別化)
2. **9 マス × 10 ターン即決思考** (シンプル + 戦略性)
3. **100 階層到達という長期目標** (やり込み)
4. **同時移動の読み合い** (鬼との心理戦)
5. **完全無料 + 広告非表示プラン** (敷居の低さ)

### 6. Number of slides

**5 枚** (App Store は最大 10 だが、5 で「Hero / Mechanics / BPM / Progression / Free」をカバー)

### 7. Style direction

- **キーワード**: 暗い宇宙系、緊張感、スピード感、未来的、アラート色アクセント
- **参考スタイル**: `~/.claude/skills/app-store-screenshots/references/keeplet-style.png` (Keeplet)
- **トーン**: dark / moody、ただし `#FF6B35` の差し色で焦燥感を演出
- **タイポ**: 太字 + 大胆な余白、headline は 80–120pt 級で画面を支配
- **装飾**: 微細なグリッドノイズ、軌跡的なグロー、BPM 数字が背景にうっすら浮かぶ
- **避ける**: 派手なグラデーション、丸み、明るい色 (世界観に合わない)

---

## 📋 Optional 回答 (スキルが聞く可能性のあるもの)

### 8. iPad スクショ

**不要** (今回は iPhone のみ、iPad 用は将来対応)

### 9. UI 要素の浮遊装飾 (cards/widgets PNG)

**なし** (シミュレータ実機キャプチャをそのまま使う、装飾は CSS で生成)

### 10. ローカライズ

**ja のみ** (初版、en は Sprint 2 以降)

### 11. テーマプリセット

**1 種類** (`escape-nine-dark`)。理由: 5 枚で世界観統一が優先、A/B テストは Sprint 2 で別タスク扱い

### 12. 追加要件

- **HIG セーフエリア遵守**: スクショの上下に通知バーやホームインジケータが映り込む場合は隠す
- **ファイル名規則**: `slide_1_hero.png`, `slide_2_mechanics.png`, ... (Apple 仕様の番号順)
- **Apple 必須解像度を全部出力**:
  - 6.9" (iPhone 16 Pro Max): 1320×2868
  - 6.5" (iPhone 11 Pro Max): 1242×2688
  - 5.5" (iPhone 8 Plus): 1242×2208
- **生成 generator は `marketing/screenshot-gen/` に置く**

---

## 🎨 各スライドの確定レイアウト

### Slide 1: Hero (BPM 加速の緊張)

```
┌─────────────────────────────────┐
│  ⚡ Escape Nine                   │ ← 上部 80px
│                                  │
│  ビートに                        │ ← Headline (100pt, white)
│  合わせて逃げろ                  │ ← 2 行目に「逃げろ」#FF6B35
│                                  │
│  9 マスで鬼から逃げ切る           │ ← Subhead (32pt, #A0A0B0)
│  音ゲー × エンドレスチャレンジ    │
│                                  │
│  [iPhone モックアップ中央配置]    │ ← gameplay_low.png をはめ込み
│   - BPM 数字 70 → 90 のアニメ模  │   背景にうっすら "100" の巨大文字
│                                  │
│  🎵 BPM 70 → 200 加速システム    │ ← Feature bullets (24pt, #F5F5F5)
│  🎮 3×3 グリッド即決思考          │
│  🏆 100 階層に挑む長期目標        │
│  🎶 楽曲ごとに難易度変化          │
└─────────────────────────────────┘
```

### Slide 2: Mechanics (ゲーム性)

```
[Headline] 9 マス、10 ターン、即決思考
[Subhead] 鬼とプレイヤーが同時移動 / クリックひとつが命
[Center] gameplay_low.png (グリッド + 鬼 + プレイヤー)
[Bullets]
  🎯 タップ or スワイプの直感操作
  ⚡ 同時移動で読み合い必須
  🧠 数手先まで読む戦略性
  🔄 何度でもリトライ
```

### Slide 3: BPM Acceleration (音ゲー側面)

```
[Headline] 鼓動が加速する
[Subhead] 階層上昇でテンポも上昇 / 集中力の限界に挑む
[Center] gameplay_high.png (BPM 180+ プレイ)
[Bullets]
  📈 階層ごとに BPM 加速
  🎼 曲調変化で緊張感最大
  👂 音楽同期で没入感
  🏃 反応速度を鍛える
[装飾] 波形 / 心電図のような線が背景を横切る
```

### Slide 4: Progression (長期目標)

```
[Headline] 100 階層、その先へ
[Subhead] 日々の練習が階層突破の鍵
[Center] result.png + leaderboard.png (2 枚並べ)
[Bullets]
  🏆 100 階層クリア = 目標達成
  📊 Game Center リーダーボード
  🎁 階層ごとに新要素解放
  📈 自己ベスト更新の快感
[装飾] 「100」の巨大数字を背景に
```

### Slide 5: Free to Try (敷居の低さ)

```
[Headline] 無料で始められる
[Subhead] 基本プレイ無料 / 広告非表示も選べる
[Center] settings.png (ストア / 設定)
[Bullets]
  🆓 完全無料で全機能体験
  ✨ 広告非表示プラン (任意)
  🌐 オフラインで遊べる
  🎵 オリジナル BGM 多数
```

---

## 🚀 generator 起動コマンド (録画完了 + capture-all 完了後)

> `marketing/raw-screenshots/` に 6 枚の実機スクショがあります。
> `marketing/SCREENSHOT_BRIEF.md` を仕様として、`/app-store-screenshots` skill を起動し、
> `marketing/screenshot-gen/` に Next.js generator を構築してください。
> Required / Optional 質問は SCREENSHOT_BRIEF.md にすべて回答済みなので追加質問は不要です。

---

## ✅ 完了チェック

- [ ] `marketing/raw-screenshots/` に 6 枚揃った
- [ ] `marketing/screenshot-gen/` に Next.js プロジェクトが立つ
- [ ] dev サーバーで 5 枚のスライドがブラウザに表示される
- [ ] export 機能で 6.9"/6.5"/5.5" の 3 解像度 × 5 スライド = 15 枚 PNG 生成
- [ ] 出力先: `marketing/screenshot-gen/exports/{6.9|6.5|5.5}/slide_N.png`

---

> ⭐️ Escape Nine プロジェクト固有 / copy.md と完全整合 / `app-store-screenshots` skill 用
