# Escape Nine: Endless 紹介・広告動画 制作ガイド ⭐️

## 📁 ディレクトリ構成

```
marketing/promo-video/
├── 01_recordings/  # iOS Simulator 等の生録画 (mp4)
├── 02_edited/      # video-use 等で編集済み中間ファイル
├── 03_final/       # 各アスペクト比の最終物
│   ├── 9_16/       # Meta Reels / TikTok / YouTube Shorts
│   ├── 1_1/        # Meta フィード正方形
│   ├── 16_9/       # YouTube / Web LP
│   └── 4_5/        # Meta フィード推奨
└── bgm/            # Suno 生成 BGM (.mp3)
```

> 既存: `screenshots/appstore/videos/` に App Store プレビュー用 (clip1-3 × iPad/iPhone) があるので、必要なら**参考素材として再利用**可能。

## 🎯 アプリ訴求ポイント (top 3)

1. **BPM 加速の緊張感** — 階層を進むごとに BPM 70 → 200 (べき乗曲線) で加速する焦燥感
2. **3x3 グリッドの即決思考** — 鬼と同時移動、9 マスで 10 ターン逃げ切る瞬間判断
3. **100 階層到達という大目標** — 音楽ゲーム × エンドレスチャレンジの新ジャンル

## 🎬 撮影スクリプト (15秒版)

```
0-2秒  : ゲーム開始 → BPM 70 で余裕の操作 (低階層)
2-6秒  : 階層上昇 → BPM 加速、鬼の手数増加
6-10秒 : 高階層 (BPM 180+) で緊迫した逃げ切り
10-13秒: 階層クリア演出 + スコア表示
13-15秒: タイトルロゴ + "100 階層に挑め"
```

## 🎵 BGM 方向性

- **方針**: **ゲーム内既存 BGM を流用が最適** (Suno で作るより合う)
- 既存 BGM ファイル: `EscapeNine-endless-/EscapeNine-endless-/Sounds/BGM/`
- 動画用には**短縮版** or **クライマックス部分の抜粋**を使う

新規で作る場合の Suno prompt:
```
driving electronic, intense, building tension, retro arcade game,
130-180 bpm gradually accelerating, no vocals, 15 second loop
```

## 📝 字幕案 (日本語版)

```
0-2秒  : "ビートに合わせて"
2-6秒  : "9 マスで逃げ切れ"
6-10秒 : "加速する鼓動"
10-13秒: "100 階層、その先へ"
13-15秒: "Escape Nine: Endless"
```

英語版:
```
0-2秒  : "Move to the Beat"
2-6秒  : "Escape on 9 Tiles"
6-10秒 : "Heart Racing BPM"
10-13秒: "Beyond 100 Floors"
13-15秒: "Escape Nine: Endless"
```

## 🛠️ ワークフロー (帰宅後実行)

### Step 1: シミュレータで撮影 (5-10 分)
```bash
cd ~/Documents/GitHub/escape-nine-endless

# Xcode で Run → シミュレータでプレイ
# 録画コマンド (別ターミナル):
xcrun simctl io booted recordVideo marketing/promo-video/01_recordings/raw_low.mp4
# 低階層 (BPM 70-100) を 5 秒撮影 → Ctrl+C

xcrun simctl io booted recordVideo marketing/promo-video/01_recordings/raw_mid.mp4
# 中階層 (BPM 130-160) を 5 秒撮影 → Ctrl+C

xcrun simctl io booted recordVideo marketing/promo-video/01_recordings/raw_high.mp4
# 高階層 (BPM 180+) を 5 秒撮影 → Ctrl+C
```

### Step 2: video-use で編集 (Claude 経由 / 10 分)
> `marketing/promo-video/01_recordings/` の 3 つの mp4 を 15 秒の広告動画に編集して。
> - 構成: low (0-2s) → mid (2-6s) → high (6-10s) → 階層クリア演出 (10-13s) → ロゴ (13-15s)
> - 字幕: 上記スクリプト通り
> - カラーグレード: 高階層は緊張感ある warm tone、低階層は cool
> - 出力先: `02_edited/edited.mp4`

### Step 3: ゲーム既存 BGM を使う or Suno で BGM 生成

```bash
# 既存 BGM 流用パターン
cp "EscapeNine-endless-/EscapeNine-endless-/Sounds/BGM/bgm_battle.mp3" "marketing/promo-video/bgm/escape_bgm.mp3"
```

### Step 4: BGM ミックス & 4 規格変換

```bash
SRC="02_edited/edited.mp4"
BGM="bgm/escape_bgm.mp3"
OUT_BASE="03_final"

# BGM ミックス (動画の長さに合わせて trim)
ffmpeg -y -i "$SRC" -i "$BGM" -c:v copy -c:a aac -shortest "02_edited/edited_with_bgm.mp4"

WITH_BGM="02_edited/edited_with_bgm.mp4"

# 9:16
ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/9_16/escape_9x16.mp4"

# 1:1
ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1080:force_original_aspect_ratio=decrease,pad=1080:1080:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/1_1/escape_1x1.mp4"

# 16:9
ffmpeg -y -i "$WITH_BGM" -vf "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/16_9/escape_16x9.mp4"

# 4:5
ffmpeg -y -i "$WITH_BGM" -vf "scale=1080:1350:force_original_aspect_ratio=decrease,pad=1080:1350:(ow-iw)/2:(oh-ih)/2:black" -c:a copy "$OUT_BASE/4_5/escape_4x5.mp4"
```

## ✅ 完了チェック

- [ ] 01_recordings/ に 3 種類のゲーム録画 (low / mid / high)
- [ ] 02_edited/ に 15 秒の編集動画
- [ ] bgm/ に BGM
- [ ] 03_final/9_16/ /1_1/ /16_9/ /4_5/ の 4 形式
- [ ] Google Ads / Meta Ads にアップロードして配信開始

## 💡 注意点

- **音楽 (BGM)** は権利問題に注意。**ゲーム内 BGM 流用が最も安全**
- 既に Google Ads で運用中なので、効果測定がスムーズ (CPI/CVR/IPM の前後比較)
- **App Store プレビュー動画** とは別物。これは広告配信用

---

> ⭐️ Escape Nine: Endless プロジェクト固有
