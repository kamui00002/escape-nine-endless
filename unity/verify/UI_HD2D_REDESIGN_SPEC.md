# UI HD-2D リデザイン仕様（Home + Game 先行）

方針（オーナー承認済み・2026-07-06）: HD-2D ジオラマの立体感 / Home+Game 先行→確認→横展開 / マス余白改善。
現状（UI_INVENTORY_HOME_GAME.md 要点）: 盤面(BoardStage)は既に HD-2D。**平面なのは uGUI の HUD/Home のみ**。UIFactory の Panel/ColorRect/Button は単色 Image だけ（影・グラデ・角丸なし）。Unity 版に ResponsiveLayout は無く UIFactory.Place(比率)+CanvasScaler。

## 設計原則
- **UIFactory 一元集約は維持**。立体感は UIFactory に「深度付きバリアント」を足す形で入れる（呼び出し側の分岐を増やさない）。
- **外部アセット非依存**: 角丸・影・グラデのスプライトは**実行時 Texture2D 生成**（本プロジェクトの手続き的生成方針＝メトロノーム音等と同じ思想）。生成物はキャッシュして使い回す。
- 比率レイアウト(UIFactory.Place)厳守・固定pt禁止。#Preview は Unity には無いので実機/エディタ目視で確認。
- 盤面の HD-2D 資産（URP Lit・ゾーン光・Bloom）と**質感を揃える**：暖色ライティング感・接地影・奥行き。

## A. UIFactory への深度プリミティブ追加（土台）
1. **RoundedSprite(radius, size)**: 角丸矩形の Sprite を Texture2D で生成（アルファで角を丸める、9-slice border 付き）。キャッシュ。
2. **SoftShadowSprite**: 半透明の柔らかい影（ぼかし円/角丸）Sprite を生成。パネル/ボタンの背後に少しオフセットして敷く＝エレベーション。
3. **VerticalGradientSprite(topColor,bottomColor)**: 縦グラデ Sprite（上明・下暗＝上からのライティング感）。
4. **Card(parent,name)**: Panel の上位版。「影 → 角丸グラデ背景 → 縁ハイライト(上辺1pxの明色)」の3層。既存の GameScreen/TutorialScreen の Frame+Inner カードもこれに寄せる。
5. **TextButton の立体化**: 影付き＋角丸グラデ。押下時に影を縮めて「沈む」フィードバック（Button の transition or onClick 前後で shadow offset を変える）。primary ボタンは暖色アクセント＋強めの影。

## B. Home 画面の HD-2D ジオラマ化
1. **パララックス背景（3層）**: 遠景(暗い洞窟/ダンジョンの奥)・中景(石柱や篝火のシルエット)・近景(手前の縁のヴィネット)。各層を UIFactory.Place で重ね、**端末の傾き(Input.gyro)か緩い自動ドリフト**で層ごとに異なる量だけ動かす（奥ほど小さく動く＝奥行き知覚）。gyro 不可環境は自動ドリフトにフォールバック。背景は手続き生成 or 単色グラデ+装飾で軽量に。
2. **キャラの接地影＋リムライト**: HomeScreen の CharacterImage(0.5,0.79) の足元に柔らかい楕円影を敷く。可能なら軽い発光(暖色)で盤面のゾーン光と統一。
3. **タイトルの奥行き**: ESCAPE NINE ロゴに影/縁取り＋わずかな浮遊アニメ(上下0.5px、拍非同期の緩い sin で可、演出のみ)。
4. **ボタン群の余白改善**: 現状 gap=0.044 / h=0.040(余白10%)。**h を保ちつつ gap を 0.052 程度へ**、または primary/secondary の階層を付けて詰まり感を解消（最高到達階層セクションとの重なりに注意＝インベントリ参照）。全ボタンを Card 化して影で浮かせる。

## C. Game 画面の HUD 立体化（盤面は既にHD-2D）
1. HUD チップ（階層/ターン/スキル/BPM）を Card 化＝影＋角丸で盤面の上に浮く感。
2. オーバーレイ（分岐/ドラフト/カウントダウン/クリア）も Card 化。ドラフトのレリックカードは影＋レアリティ色の縁で"カードらしさ"を強調。
3. 盤面 RawImage と HUD の余白/重なりをインベントリの配置値で微調整（HUD が盤面に食い込んでいないか）。

## D. マス余白の是正
1. **チュートリアル盤面（真犯人）**: TutorialScreen.cs:43-45 `CellSpacingRatio=0.02`(間隔6.25%) → **ゲーム盤面並みの ~0.12〜0.15** へ。`SpriteInCellRatio=0.78` → 0.62〜0.68 でスプライト周りに余白。セルも Card 化で立体に。
2. ゲーム盤面(BoardStage)は間隔17%で妥当＝据え置き。ただし HD-2D の見栄え次第で TileSpacing/カメラ MarginFactor を微調整可（要目視）。

## 実装順・検証
1. A(UIFactory 深度プリミティブ) → B(Home) を先に完成させ、**実機ビルド→スクショ→オーナー確認**。
2. OK 後 C(Game HUD)・D(余白) → 再確認。
3. その後 Shop/Ranking/Settings/遺物庫へ横展開。
4. 各段階で: コンパイル(console-get-logs Error 0) → EditMode 回帰 green → 実機スクショ目視。
5. レリック解放説明画面(バッチ1)も Card/深度プリミティブで見栄えを揃える。

## 未確定（実装中に判断 or オーナー確認）
- パララックスの駆動: gyro か自動ドリフトか（実機で酔わない範囲。まず自動ドリフト微量で安全に）。
- 角丸半径・影の強さ: 実機で"やりすぎない"範囲に調整（AIデザインの3デフォルト回避、上品に）。
