# UI インベントリ: Home / Game 画面 (HD-2D リデザイン前の棚卸し)

調査日: 2026-07-06
対象: `unity/EscapeNine/Assets/Scripts/Runtime/UI/Screens/HomeScreen.cs`, `GameScreen.cs`,
`Runtime/UI/UIFactory.cs`, `Runtime/UI/UITheme.cs`, `Runtime/Stage/*`, `TutorialScreen.cs`

目的: 「立体感がない (のっぺり)」「マスの余白に余裕がない」というオーナー不満の原因を実コード行番号で特定し、
次工程 (HD-2D ジオラマ風リデザイン設計) の当て所を洗い出す。**実装はしていない。**

前提のずれ (先に明記): タスク指示にあった `Utilities/ResponsiveLayout.cs` は Unity プロジェクトには
**存在しない**。`grep -rl ResponsiveLayout unity/EscapeNine/Assets/Scripts/` の一致は `UIFactory.cs` と
`HomeScreen.cs` のコメント中で Swift 正本 (`docs/DEVELOPMENT_SWIFT.md` 系) を指す言及のみ。Unity 側で
「比率ベース・固定px禁止」を担っているのは `UIFactory.Place()` 1 関数であり、iPad 分岐も存在しない
(`CanvasScaler` の Scale With Screen Size で吸収する設計。`UIFactory.cs:6-11` のコメント参照)。

---

## 1. Home 画面 要素インベントリ

`HomeScreen.cs` の `BuildUI()` (55-89行) が呼ぶ各 `Build*Section` を掲載順に列挙。位置は `UIFactory.Place(rt, cx, cy, w, h)` の親比率 (cy は下から上、0=画面下端)。

| 要素名 | 種別 | 位置 (cx, cy, w, h) | 色 | 立体感 (影/グラデ/角丸) |
|---|---|---|---|---|
| Background | ColorRect (全面) | 0.5, 0.5, 1, 1 (71-72行) | `UITheme.Background` #2c1810 単色 | なし |
| SafeArea | Panel (透明コンテナ) | 親いっぱい (75-76行) | - | - |
| TitleLabel "ESCAPE NINE" | Label | 0.5, 0.905, 0.94, 0.06 (136-138行) | `UITheme.Available` #ffd700 | なし。コメント (134行) 「グラデーション / グロー / シマー / バウンス演出は Phase 4 (juice) 送り」と明記 |
| SubtitleLabel "Endless Dungeon" | Label | 0.5, 0.862, 0.8, 0.03 (140-142行) | TextColor@0.8α | なし |
| CharacterImage (選択キャラ) | SpriteImage | 0.5, 0.79, 0.30, 0.075 (150-152行) | スプライトそのまま | 接地影・台座なし。raycastTarget=false (151行) |
| CharacterNameLabel | Label | 0.5, 0.744, 0.5, 0.025 (154-155行) | TextColor | なし |
| PlayButton "冒険を始める" (primary) | TextButton | 0.5, 0.685, 0.72, 0.06 (169-173行) | bg=`Main` #f4a460 / fg=`Background` | なし。コメント (168行) 「glow/pulse は Phase 4 送り」 |
| DailyChallengeButton | TextButton + 内部ラベル2行 + NewBadge | 0.5, 0.617, 0.72, 0.05 (236-263行) | bg=`BackgroundSecondary`#3d2817 / fg=`GoldText`。NewBadge=`Color.red` | フラットな赤い矩形バッジのみ、影なし |
| CharacterButton (secondary) | TextButton | 0.5, 0.550, 0.72, 0.040 (181-182行) | bg=`BackgroundSecondary` / fg=`TextColor` | なし。以下6ボタンすべて同一トーン (視覚的な階層区別なし) |
| RankingButton | TextButton | 0.5, 0.506, 0.72, 0.040 (183-184行, gap=0.044) | 同上 | なし |
| ShopButton | TextButton | 0.5, 0.462, 0.72, 0.040 (185-186行) | 同上 | なし |
| AchievementButton | TextButton | 0.5, 0.418, 0.72, 0.040 (187-188行) | 同上 | なし |
| HowToButton | TextButton | 0.5, 0.374, 0.72, 0.040 (189-190行) | 同上 | なし |
| SettingsButton | TextButton | 0.5, 0.330, 0.72, 0.040 (191-192行) | 同上 | なし |
| RelicVaultButton | TextButton | 0.5, 0.286, 0.72, 0.040 (196-197行) | 同上 | なし |
| FloorCaption/FloorNumberLabel (最高到達階層) | Label ×1〜2 | Release: 0.5,0.238,0.6,0.025 + 0.5,0.19,0.6,0.05 (297-304行) / DEBUG: 0.5,0.235,0.8,0.04 1行 (292-294行) | `Available` gold Bold | なし。コメント (301行)「AnimatedNumber (カウントアップ演出) → 静的表示に簡略化。演出は Phase 4 送り」 |
| Toast | Panel | 0.5, 0.155, 0.7, 0.04 (312-313行) | bg=black@0.75α / fg=`GoldText` | 角丸・影なし |
| DangerZone (DEBUG専用) | Panel + WarnBar + 3ステッパー行 + 3トグルボタン | 0.5, 0.115, 0.92, 0.20 (393-460行) | bg=`BackgroundSecondary`@0.9α、上端に `Warning` 色の細バー (WarnBar, 399-400行) | **この WarnBar だけが本画面唯一の「縁取り/アクセント」演出** (単色矩形の重ね張りで疑似枠を作る手法) |

**セカンダリボタン7個の詰め込み実測**: `gap=0.044f` (166行) に対し `h=0.040f` (165行) — ボタン間の実質すき間は `0.044-0.040=0.004` (ボタン高さの約10%)。コメント (163-164行)「セカンダリボタンが 6→7 個になったため、行高/行間をさらに詰めて最高到達階層セクションとの重なりを避ける」と明記されており、Home画面は意図的に窮屈化してきた経緯がある。

---

## 2. Game 画面 要素インベントリ

`GameScreen.cs` の `BuildUI()` (212-255行)。HUD → 盤面 → ボトム → オーバーレイ (Swift の ZStack と同じ「後発が手前」順)。

### HUD

| 要素名 | 位置 (cx, cy, w, h) | 色 | 備考 |
|---|---|---|---|
| Background | 0.5,0.5,1,1 (228-229行) | `Background` 単色 | Home と同一 |
| BackButton "< 戻る" | 0.16, 0.968, 0.26, 0.036 (261-263行) | bg=`BackgroundSecondary` / fg=`TextColor` | |
| PauseButton "一時停止" | 0.82, 0.968, 0.30, 0.036 (265-267行) | 同上 | |
| RelicCount ラベル | 0.5, 0.968, 0.30, 0.036 (272-274行) | `GoldText` | Back/Pause の間 [0.29,0.67] に収める |
| BPMInfoWidget (別クラス) | 0.5, 0.906, 0.92, 0.062 (281-282行) | (未調査、別ファイル `BPMInfoWidget`) | |
| BeatIndicatorWidget (別クラス) | 0.5, 0.795, 0.70, 0.145 (284-285行) | (別ファイル) | |
| ComboRow (Grade/Combo/Multiplier) | 0.5, 0.708, 0.92, 0.026 (288-303行) | Success/TextColor/Success | 既定 SetActive(false) |
| TurnCaption / TurnValue | cy=0.676 (309-314行) | TextColor@0.7α / `Available` | MiddleRight/MiddleLeft で cx を振り分け (306-308行コメント: 2026-07-04 重なり監査で検出・修正済み) |
| SkillInfoRow (SkillName/SkillCount) | cy=0.648 (317-327行) | 同上 | 斜め移動キャラでは非表示 |

### 盤面 (3D World → RawImage)

| 要素名 | 位置 (cx, cy, w, h) | 備考 |
|---|---|---|
| BoardAnchor (RawImage, 盤面表示領域) | 0.5, 0.425, 0.94, 0.36 (351-352行) | **旧 uGUI 盤面と同じ配置比率を踏襲** (348-350行コメント)。`StageRenderView` がカメラ→RenderTexture→この RawImage を結線 (BuildWorldBoard 346-385行) |

BoardAnchor の上下の空き: 上は SkillInfoRow (cy=0.648) との間、下は SkillButton (cy=0.168) / SpecialRule (cy=0.106) との間。HUD 側は盤面エリアに直接重ならない設計。

### ボトム

| 要素名 | 位置 (cx, cy, w, h) | 色 |
|---|---|---|
| SkillButton | 0.5, 0.168, 0.72, 0.055 (391-393行) | bg=`Available` gold / fg=white。既定非表示 |
| SpecialRule ラベル行 | 0.5, 0.106, 0.86, 0.036 (399-402行) | bg=`BackgroundSecondary` / fg=`Warning`。既定非表示 |

### オーバーレイ (兄弟生成順 = 描画順、後が手前)

| 要素名 | 位置概要 | 色・装飾 |
|---|---|---|
| FloorClearOverlay | 全画面bg@0.95α (412-437行) | FloorLabel(110pt gold) cy=0.62 / "クリア！"(64pt GoldText) cy=0.545 / **Divider** (Available@0.4α の極細ColorRect, h=0.0025, cy=0.505, 424-425行) / NextLabel cy=0.465 / StartButton(Main bg) cy=0.375 |
| RouteChoiceOverlay (Phase 5c、Swift正本になし) | Title cy=0.80 (457-459行) | SafeCard(Success色) cy=0.585 / AbyssCard(Warning色) cy=0.375。**カード = Frame(単色ColorRect,全面) + Inner(BackgroundSecondary Panel, 0.965×0.90 inset)** (473-499行) → 唯一の「縁取りカード」表現。角丸・影なし |
| RelicDraftOverlay (Phase 5a、Swift正本になし) | Title cy=0.82 (511-571行) | 最大4枚の縦積みカード。同じ Frame+Inner パターン (543-554行)。フレーム色=レアリティ色 (537-541行コメント: 本来は Bloom 発光にしたいが uGUI は URP Bloom 対象外なので枠の明度と太さだけで代替、と明記) |
| SkillResetToast | cy=0.80, w=0.56,h=0.045 (574-594行) | bg=`BackgroundSecondary`。上下に `Success` 色の細ライン (LineTop/LineBottom, 583-586行) = 疑似ボーダー |
| PregameOverlay | ReadyLabel cy=0.72 / FloorLabel(96pt) cy=0.65 / AI選択4ボタン cy=0.49 (597-644行) | bg=Background@0.97α |
| PausedOverlay | Title cy=0.62 / Resume(Main) cy=0.50 / Quit(BackgroundSecondary) cy=0.42 (647-668行) | bg=Background@0.95α |
| CountdownOverlay | CountLabel(280pt!) cy=0.55 (671-683行) | bg=black@0.7 固定色 (UITheme経由でない直書き) |
| GameOverOverlay | Icon"！"(140pt) cy=0.60 / Text(90pt) cy=0.50 (686-702行) | bg=black@0.7 or Warning@0.4 (捕獲時、986-989行で動的着色) |
| BossOverlay | Title(100pt red) cy=0.58 / FloorLabel cy=0.50 (705-718行) | bg=red@0.15 固定色 |

---

## 3. 「立体感が無い」原因の特定

### 3.1 根本原因: `UIFactory.cs` が影・グラデ・角丸を一切生成しない

- `Panel()` (32-41行): `Image` を追加するが `img.color = bg.Value` のみ (37-38行)。sprite 未指定 = Unity 既定の 1x1 白テクスチャ → **完全な直角フラット矩形**。
- `ColorRect()` (150-157行): 同様に `img.color = color` (154行) だけ、`raycastTarget = false` (155行) 固定。影・グラデ設定なし。
- `TextButton()` (105-130行): 背景 `Image`(110-111行、flat color) + `Button`(113-119行、`Selectable.Transition.ColorTint` = 押下時に色を暗くするだけで z 方向の浮き上がりや影の変化なし) + `Label`(126-127行)。
- `SpriteImage()` (137-144行): `preserveAspect=true` のみ。接地影・グロー等の付随演出なし。
- `UITheme.cs` (18-63行): 15色すべて `Hex()` によるフラット単色定数。グラデーション定義・エレベーション別トーン (surface/surface-variant 等) は存在せず、実質 `Background` / `BackgroundSecondary` の**2階調**だけで全パネルを塗り分けている。

→ Home/Game 全体で60箇所以上ある `UIFactory.Place()` 呼び出しの対象 (Panel/ColorRect/TextButton) は**すべて直角・単色・無影**の板が Background の上にただ重なっているだけ、というのが「のっぺり」の技術的実体。

### 3.2 唯一の「疑似立体」手法: Frame + Inset Inner のカード

`GameScreen.cs` の `BuildRouteCard()` (473-499行) と `BuildRelicDraftOverlay()` のカードループ (530-568行)、`TutorialScreen.cs` の `AddCardBorder()` (272-279行) がやっている手法:
- 外側に単色 `ColorRect`(アクセント色) を全面配置
- 内側にわずかに小さい (0.965×0.90 など) `Panel`(BackgroundSecondary) を重ねる
- 結果、外周にアクセント色の細い縁だけが見える「縁取り風」表現になる

これは影でもグラデでもなく**色面の重ね張り**であり、立体感としては最小限。`GameScreen.cs:537-541` のコメントは「本来はレアリティ別の発光 (Bloom 閾値超えのエミッシブ) にしたいが、uGUI (Canvas Overlay) は URP Bloom (カメラのポストプロセス) の対象外なので枠の明度と太さで代替するに留めた」と**設計側も限界を自覚している**。

### 3.3 対照: BoardStage (3D盤面) は既に HD-2D 化済み

- `TileView.cs`: URP Lit マテリアル + `MaterialPropertyBlock` で色替え (156-158行, 290-295行)。単なる `Image.color` ではなく実ライティングを受けるサーフェス。
- `StageLights.cs`: ゾーン別 Directional Light (61-68行) + 霧専用ポイントライトがプレイヤーの接地座標に追従 (106-111行、`BoardStage.Update()` から毎フレーム呼ばれる)。
- `StagePostFx.cs`: URP `Volume` で Bloom/Vignette/ColorAdjustments を実行時生成 (69-99行)。
- `CameraRig.cs`: 圧迫ズーム (FOV加算)・衝突インパルス (position加算)・階層クリア時のオービット演出 (114-143行) を後乗せするカメラワーク。
- `StageRenderView.cs`: このカメラ映像を RenderTexture → `RawImage` (BoardAnchor) として HUD に埋め込んでいる (66-90行)。

**結論: 盤面は既に立体・演出付き。「平面/のっぺり」なのは盤面を取り囲む uGUI の HUD・オーバーレイ・Home 画面全体**であり、盤面側の技術 (ライト・Bloom・カメラワーク) は一切 uGUI 側に共有されていない。

### 3.4 HD-2D 化で"効く"介入ポイント (優先度順の当たり)

1. **UIFactory への影/縁取りオプション追加**: `Panel`/`TextButton` に `bool shadow = false` 引数を足し、内部でオフセットした黒半透明の `ColorRect` をもう1枚背後に生成する (3.2 の Frame+Inner パターンを一般化するだけで済み、新規シェーダー不要)。
2. **角丸**: `Image.type = Image.Type.Sliced` + 9-slice のシンプルな丸角スプライト1枚を `Resources` に追加し、`Panel`/`ColorRect`/`TextButton` のデフォルト sprite として差し込む。UIFactory 1箇所の変更で全画面に波及する。
3. **ボタンのグラデ (上ハイライト/下シェード)**: 2枚の半透明白/黒 `ColorRect` を上下半分に重ねるだけで疑似グラデが作れる (シェーダー不要、既存の重ね張り手法の延長)。
4. **Home 背景のパララックス**: 現状 `Background` は単色 `ColorRect` 1枚 (HomeScreen.cs:71-72)。BoardStage が持つ「ゾーンテーマ」の思想を輸入し、Home にも多層背景 (奥/中/手前レイヤーを別スクロール速度で) を足す余地が大きい。
5. **CharacterImage の接地影**: `HomeScreen.cs:150-152` のスプライトの真下に薄い楕円形 `ColorRect`(半透明黒) を1枚足すだけで「浮いている」印象を「立っている」に変えられる。
6. **オーバーレイ背景のポストFX**: `FloorClearOverlay`/`PregameOverlay`/`PausedOverlay` 等はすべて `Background@0.95α` の単色フラットパネル。盤面側の `StagePostFx` (Bloom/Vignette) の一部設定をオーバーレイ背景色計算にも波及させる (例: Vignette相当の中心-周辺グラデをオーバーレイの ColorRect に追加) と統一感が出る。

---

## 4. 「マスの余白が無い」原因の特定

### 4.1 Game 盤面 (実プレイの3D盤面) — BoardStage

- `BoardStage.cs:35` `public const float TileSpacing = 1.1f;` (タイル中心間隔、World単位)
- `TileView.cs:67` `public const float Footprint = 0.94f;` (タイル1枚の一辺長。コメント66行「中心間隔と同じにすると隙間なく並ぶ」)
- **実ギャップ = 1.1 − 0.94 = 0.16 World単位 ≈ spacing比で14.5%、footprint比で17%**。既に隙間ゼロではなく意図的な余白が入っている。
- カメラ側にも余白係数がある: `StageCameraDirector.cs:46` `private const float MarginFactor = 1.15f;` — フレーミング時に盤面半径 (`BoardStage.BoardHalfExtent`) へこの係数を掛けて画角を決める (97行 `float halfExtent = BoardStage.BoardHalfExtent * MarginFactor;`) ため、盤面の周囲にもカメラレベルで約15%の余白が確保されている。
- **この盤面は「詰まっている」候補として弱い**。当てるなら `Footprint` を 0.94→0.85〜0.88 程度に下げてギャップ比を上げる、または `MarginFactor` を 1.15→1.25 程度に上げてカメラの引きを増やす、の2択。

### 4.2 Tutorial 盤面 (ミニ図解グリッド) — TutorialScreen

- `TutorialScreen.cs:43` `private const float CellSpacingRatio = 0.02f;` (Swift版 `cellSpacing 6pt` 相当、コメント明記)
- `TutorialScreen.cs:44` `private const float CellSizeRatio = (1f - CellSpacingRatio * 2f) / 3f;` → `(1 - 0.04) / 3 = 0.32`
- **ギャップ/セル比 = 0.02 / 0.32 ≈ 6.25%** — Game盤面 (§4.1, 約17%) の**半分以下**の余白しかない。これが「マスの余白に余裕がない」と感じる主因である可能性が高い。
- さらに `TutorialScreen.cs:45` `private const float SpriteInCellRatio = 0.78f;` — ただでさえ狭いセルの78%をキャラスプライトが占有するため、セル境界とスプライトの間の余白はほぼ無い。
- 配置計算 (`BuildCell()`, 421-424行):
  ```csharp
  float cx = CellSizeRatio * 0.5f + col * (CellSizeRatio + CellSpacingRatio);
  float cy = 1f - (CellSizeRatio * 0.5f + rowFromTop * (CellSizeRatio + CellSpacingRatio));
  ```
  `CellSpacingRatio` と `CellSizeRatio` の2定数だけがギャップ幅を決めており、他のコードを触らず定数変更だけで余白量を調整できる (`CellSizeRatio` は `CellSpacingRatio` から自動導出されるため、スペーシングを上げれば自動的にセルは縮む)。

### 4.3 当て所まとめ

| 画面 | ファイル:行 | 現状値 | ギャップ比 | 調整レバー |
|---|---|---|---|---|
| Game (3D盤面) | `BoardStage.cs:35` / `TileView.cs:67` | TileSpacing=1.1 / Footprint=0.94 | ~17% | Footprint↓ or MarginFactor(`StageCameraDirector.cs:46`)↑ |
| Tutorial (ミニ盤面) | `TutorialScreen.cs:43-45` | CellSpacingRatio=0.02 / SpriteInCellRatio=0.78 | ~6.25% | CellSpacingRatio↑ (0.02→0.05程度) が最も効く。副次的に SpriteInCellRatio↓ (0.78→0.7程度) |

**結論**: 「マスの余白が無い」の主犯は Tutorial のミニ盤面 (`TutorialScreen.cs`)。実ゲームの3D盤面 (`BoardStage`) は既に相対的に余裕のある比率で、更に足すなら Footprint かカメラ MarginFactor の微調整で十分。

---

## 5. リデザインの制約メモ

立体感を足す介入は既存の「1つの UIFactory 経由」原則 (`UIFactory.cs` コメント2-4行: SwiftUI の宣言的 View 構築に相当する唯一の生成口) を壊さないことが最優先。具体的には、影/角丸/グラデを Home・Game 個別の画面コードに直接書き足すのではなく、`UIFactory.Panel()` / `UIFactory.TextButton()` / `UIFactory.ColorRect()` のシグネチャにオプション引数 (`shadow`, `rounded` 等のデフォルト `false`) を追加する形で1箇所に閉じ込め、既存呼び出し (HomeScreen/GameScreen/TutorialScreen 等) は無変更のまま新しい画面だけがオプトインできるようにする。位置・サイズは今回もまったく変えずに `UIFactory.Place()` の比率呼び出し規約 (親矩形に対する 0..1、offsetMin/Max常に0、CanvasScaler 1170×2532 で iPad/iPhone を相似形吸収) を継続し、固定 px を一切導入しない。盤面 (`TileSpacing`/`Footprint`) の調整も同様にBoardStage内の定数変更のみで完結させ、`GameScreen.cs` 側の `BuildWorldBoard()` の呼び出し方 (0.94,0.36 の BoardAnchor比率など) は極力据え置く。新規画面/変更箇所には既存の `#Preview` 相当 (Unity側は Play Mode 実機/シミュレータ確認、iPad分岐は CanvasScaler 依存のため個別分岐コード不要) の確認フローを踏襲する。
