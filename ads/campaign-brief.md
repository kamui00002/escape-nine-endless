# Campaign Brief: Escape Nine: Endless
**Generated:** 2026-04-30
**Platforms:** Meta (Facebook, Instagram)
**Objective:** App Installs
**Concepts:** 3

## Brand DNA Summary
「Escape Nine: Endless」は「ビートに乗れ。逃げ切れ。」をキャッチコピーに、9マスの盤面で音楽のビートに合わせて逃げ続けるハイスピードなエンドレスチャレンジゲーム。ターゲットは音ゲー・リズムゲーム好きの15-35歳。スタミナなし・コインなし・複雑な要素なし、シンプルなのに中毒性が高いのが特徴。ビジュアルはダークで疾走感のあるドット絵レトロ調。

## Audit Context
No audit data; concepts are generalized.

## Campaign Concepts

### Concept 1: 「やめられない」— 中毒性訴求
**Hypothesis:** 「シンプルだから一瞬で引き込まれる」体験を広告でも再現
**Primary Message:** シンプルなのに、気づいたら30分経ってる
**Tone:** スピード感・興奮・中毒性
**Visual Direction:** ゲームプレイ画面のスクリーンショット。BPMカウンターが高速で上昇。「Stage 47」など高階層の数字が輝く。ダークバックグラウンドに赤いアクセントカラー。
**Target Platforms:** Instagram Feed, Facebook Feed
**CTA:** 無料でプレイ
**Copy Framework:** AIDA（ゲーム画面で注目 → 「シンプルなのに」で興味 → 記録更新の欲求 → DL）

### Concept 2: 「ビートに挑戦」— スキル訴求
**Hypothesis:** 音ゲー勢の「自分のリズム感を試したい」本能にアプローチ
**Primary Message:** あなたは何階層まで逃げ切れる？
**Tone:** 挑発的・競争心・達成感
**Visual Direction:** 高BPMステージのスクリーンショット（BPM 180以上）。カウントダウン「3・2・1」のエフェクト。「YOUR RECORD」テキストオーバーレイ。
**Target Platforms:** Instagram Stories, Instagram Reels
**CTA:** 記録に挑戦
**Copy Framework:** PAS（リズムゲームに退屈した → もっと熱くなれるゲームが欲しい → EscapeNineで解決）

### Concept 3: 「逃げろ」— 緊迫感訴求
**Hypothesis:** ゲームの緊迫感をそのまま広告に持ち込み、体験を予感させる
**Primary Message:** 音が速くなるほど、頭が真っ白になる
**Tone:** 緊張感・疾走感・スリル
**Visual Direction:** 動体ブラーが入ったゲーム画面。鬼コマがプレイヤーに迫る瞬間。「BPM 200」表示。画面全体に緊迫感のある光エフェクト。
**Target Platforms:** Instagram Reels, Facebook Feed
**CTA:** 今すぐ逃げろ
**Copy Framework:** Star-Story-Solution（余裕のBPM100 → 気づいたらBPM180で手が震える → それがEscapeNine）

## Copy Deck

### Concept 1 — Meta Feed
- **Headline (40文字以内):** 「シンプルなのに、やめられない」
- **Primary Text (125文字以内):** 9マス。ビート。それだけ。なのに気づいたら30分。Escape Nine: Endless を無料でプレイ。
- **CTA:** アプリをインストール

### Concept 1 — Instagram Stories (縦型)
- **Headline:** 「ビートに乗れ、逃げ切れ」
- **Primary Text:** シンプルなのに中毒性MAX。今日から記録更新を目指せ。
- **CTA:** スワイプしてDL

### Concept 2 — Meta Feed
- **Headline:** 「何階層まで逃げ切れる？」
- **Primary Text:** BPMが上がるほど、思考が追いつかなくなる。あなたの限界を試せ。音ゲー好き必見。
- **CTA:** 記録に挑戦

### Concept 3 — Instagram Reels / Facebook
- **Headline:** 「音が速くなるほど、頭が真っ白に」
- **Primary Text:** BPM200の世界で逃げ続けろ。シンプルな9マスが、最高の地獄になる。無料プレイ。
- **CTA:** 今すぐ逃げろ

## Image Generation Briefs

### Brief 1: Concept 1 — Instagram/Facebook Feed (1080×1080)
**Prompt:** Dark retro pixel art game screenshot showing 3x3 grid escape game, glowing red enemy piece chasing player on dark background, BPM counter showing 140, stage number 35, dramatic lighting with neon accents, intense gaming atmosphere, minimalist game UI, high energy pixel art style
**Dimensions:** 1080×1080
**Safe zone notes:** 上部に「Escape Nine」タイトル余白、下部CTA余白

### Brief 2: Concept 2 — Instagram Stories (1080×1920)
**Prompt:** Vertical gaming poster, dark background with electric blue and red neon glow, 3x3 grid game board center composition, large "BPM 180" text glowing, countdown "3 2 1" effect, retro pixel art mixed with modern neon aesthetics, intense and dramatic mood, mobile game advertisement style
**Dimensions:** 1080×1920
**Safe zone notes:** 上部300px・下部400px をテキスト・CTA用に確保

### Brief 3: Concept 3 — Facebook Feed (1080×1080)
**Prompt:** High-speed motion blur effect on dark game screen, pixel art character barely escaping in 3x3 grid, red enemy closing in, BPM meter at 200 with pulsing glow, panic and thrill visual atmosphere, dramatic red and dark color palette, adrenaline-rush gaming scene
**Dimensions:** 1080×1080
**Safe zone notes:** 中央にゲーム画面、四隅テキスト余白

## Next Steps
1. `ads/.env` に Meta 認証情報を設定（ユーザー作業）
2. `/ads generate` で Brief の画像を本番生成（banana-claude セットアップ後）
3. App Store URL を `campaign-template.yaml` の `link:` に設定（ユーザー作業）
4. `meta-ads create --config ads/campaign-template.yaml --dry-run` で最終確認
5. `meta-ads create --config ads/campaign-template.yaml` で出稿
