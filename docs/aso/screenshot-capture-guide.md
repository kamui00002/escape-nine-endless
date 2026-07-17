# Unity 版 App Store スクショ 撮影 & 生成ガイド ⭐️

Unity HD-2D 版に合わせた App Store スクリーンショット（5枚）を作り直すための turnkey 手順。
キャプション典拠: `docs/aso/sprint-1-improvements.md §3`（会議録 第8回 Emily / 第12回 結論）。訴求軸＝**挑発フック**（二人称＋数字）。

> **なぜ作り直すか**: 現ストアのスクショは Swift 版（2026-05-26）で、音ゲー路線の旧コピー。Unity は HD-2D で見た目が別物 → そのままだと審査 2.3.3（実物と不一致）リスク。今回 (a) Unity 実機の絵に差し替え (b) 新 ASO「9マス脱出ローグライク」路線のコピーに刷新する。

---

## 生成パイプライン

`marketing/screenshot-gen/`（Next.js）が、`public/screenshots/ja/*.png`（Unity 実機の生スクショ）に**ブランド配色のキャプション帯**を合成して 5 枚を書き出す。

- レイアウト: 上 ~58% に実機ゲーム画面 / 下 ~42% に Deep Brown キャプション帯
- 配色: 背景 `#2A1F18` / 金 `#D4A659` / 血赤 `#B33A3A` / 骨白 `#F2EAD8` / 警告黄 `#FFC72C`
- キャプションは `src/app/page.tsx` に実装済（未撮影の画像はプレースホルダ表示）

---

## Step 1: Unity 実機で 5 画面を撮る

Unity 版アプリを実機（or シミュレータ）で表示し、以下 5 画面を撮影。**保存先とファイル名を厳守**（page.tsx が参照）:

| # | 撮る画面 | ファイル名（`public/screenshots/ja/` に置く） | 撮影のコツ |
|---|---|---|---|
| 1 | **高難度プレイ中**（高階層・敵が近い・霧/消失マスが見える緊張状態） | `gameplay_high.png` | 影が迫る一番ヒリつく瞬間。HUD に階層/BPM が見えると尚良し |
| 2 | **Game Over 画面**（巨大リトライボタンが写る） | `gameover.png` | 「発射台型」演出。RETRY が大きく写る構図 |
| 3 | **世界ランキング画面** | `leaderboard.png` | 自分の順位＋上位が並ぶリスト |
| 4 | **デイリーチャレンジ画面** | `daily.png` | 今日のテーマ/条件が見える画面 |
| 5 | **序盤の 9 マス盤面**（クリーンな通常プレイ） | `gameplay_low.png` | 3×3 が明快に見える。指タップの瞬間だと尚良し |

### 撮影方法（どちらか）

- **実機**: 画面を目的の状態にして `xcrun devicectl device screenshots ...`、または端末の電源+音量で撮って AirDrop → リネーム。
- **シミュレータ**: `xcrun simctl io booted screenshot <name>.png`（Unity をシミュレータで動かす場合）。
- 撮った 5 枚を `marketing/screenshot-gen/public/screenshots/ja/` に上記名で保存。

> 解像度は実機縦なら何でも可（合成側で上 58% にクロップ）。iPhone の縦スクショ推奨。

---

## Step 2: キャプションを合成して書き出す

```bash
cd marketing/screenshot-gen
npm install        # 初回のみ
npm run dev        # http://localhost:3000
```

ブラウザで開き、右上 **Size** で書き出しサイズを選び **Export all** を押す → 5 枚 PNG がダウンロードされる。

App Store が要求する主要サイズ:
- **6.9"（1320×2868）** — iPhone 16 Pro Max 系（必須）
- **6.5"（1284×2778）** — 旧 Plus/Pro Max（あると安心）
- iPad 13" は別途（現状 iPhone 優先。iPad 版を出すなら追加）

---

## キャプション文言（4 言語・ASC コピペ用）

> スクショ内に焼き込むのは基本 **ja**。ローカライズ地域を厳密にやるなら各言語版も生成（page.tsx のテキストを差し替えて Export）。

### 1. 挑発フック（`gameplay_high.png`）

| 言語 | メインキャッチ | サブ |
|---|---|---|
| ja | あなたは何階まで行ける？ | 現状の最高到達: 7階 |
| en-US | How High Can You Climb? | Current Best: Floor 7 |
| ko-KR | 당신은 몇 층까지? | 최고 기록: 7층 |
| zh-TW | 你能逃到第幾層？ | 最高紀錄: 第7層 |

### 2. Game Over の誘惑（`gameover.png`）

| 言語 | メインキャッチ | サブ |
|---|---|---|
| ja | もう一回？ | 99% の人が「もう一回」を押した |
| en-US | One More? | 99% of players tap RETRY |
| ko-KR | 한 번 더? | 99%가 다시 도전합니다 |
| zh-TW | 再來一次？ | 99% 的玩家選擇了「重試」 |

### 3. 世界と競え（`leaderboard.png`）

| 言語 | メインキャッチ | サブ |
|---|---|---|
| ja | 世界と競え | 今週のトップ: 12階 |
| en-US | Compete Worldwide | Top this week: Floor 12 |
| ko-KR | 세계와 겨뤄라 | 이번 주 1위: 12층 |
| zh-TW | 挑戰全球玩家 | 本週最高: 第12層 |

### 4. 毎日新しい挑戦（`daily.png`）

| 言語 | メインキャッチ | サブ |
|---|---|---|
| ja | 毎日、新しい挑戦 | 今日のテーマ: 影が 2 倍速 |
| en-US | Daily Challenge | Today: Shadow x2 Speed |
| ko-KR | 매일 새로운 도전 | 오늘의 테마: 그림자 2배속 |
| zh-TW | 每日新挑戰 | 今日主題: 影子 2 倍速 |

### 5. 指 1 本で十分（`gameplay_low.png`）

| 言語 | メインキャッチ | サブ |
|---|---|---|
| ja | 指 1 本で十分 | タップして移動するだけ |
| en-US | Just One Finger | Tap to move. That's it. |
| ko-KR | 손가락 하나면 충분 | 탭하여 이동하기만 |
| zh-TW | 只用一根手指 | 點擊移動，就這麼簡単 |

> ⚠️ ASC 側の説明文とは別物（スクショ内キャプションは審査なしで差し替え可能な画像扱い）。ただし**絵文字・装飾記号は使わない**（`feedback_appstore_safe_chars` 準拠）。

---

## 注意（審査）

- **2.3.3**: スクショは Unity 実機の実画面から作る（この手順どおりなら OK）。
- **2.3.4 / R6**: App **プレビュー動画**枠に Seedance 生成動画は**使わない**（実機素録画のみ）。Seedance は広告キャンペーン用。
- 数字（7階/12階/2倍速 等）は演出値。実プレイと矛盾しない範囲で調整可。

## 関連
- ASO 戦略元: `docs/aso/sprint-1-improvements.md §3`
- メタデータ: `docs/appstore-metadata.md`
- 審査対策: `docs/review-readiness-unity.md`（R6/R8）
