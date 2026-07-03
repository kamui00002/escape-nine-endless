# Phase 4.5 設計文書 — ビジュアル大型アップグレード（HD-2D 化）⭐️

**ステータス: 設計のみ・実装前・オーナーレビュー待ち**
作成: 2026-07-04 / 設計: Fable（オーケストレータ）/ 実装予定: Sonnet エージェント fan-out

> 位置づけ: docs/unity-migration-plan.md の Phase 4（juice）と Phase 5（ローグライク深化）の間に挟む
> 追加フェーズ。オーナー評価「Swift 版の方が見た目が良い」(2026-07-04) への回答であり、
> 「Unity でしかできない画」で Swift 版を超えることが目的。

---

## 0. 一言サマリ

**ゲームロジック（Core）には 1 行も触れずに、盤面を「照明付きの 3D ジオラマ」へ載せ替える。**
フル 3D リメイクはしない。ドット絵キャラはドット絵のまま、Octopath Traveler 型の
HD-2D（2D スプライト × 3D 環境 × ポストプロセス）で「別物に見える」画を作る。

---

## 1. 現状診断 — なぜ Swift 版の方が良く見えるのか

| 観点 | Swift 版 | Unity 版（現状） | 根拠ファイル |
|---|---|---|---|
| テキスト描画 | SwiftUI ネイティブ（SF 系、サブピクセル AA） | legacy `Text` + OS フォント動的解決 | `UIFactory.cs:51` `UITheme.cs:98 ResolveFont()` |
| 画面 | ネイティブのマテリアル/スプリング演出 | フラット矩形 + 単色背景 | `MainSceneBuilder.cs:68 SolidColor` |
| 盤面 | — | uGUI ボタングリッド（照明・被写界深度なし） | `GridBoardWidget.cs` |
| ポストプロセス | なし（不可能） | **なし（Built-in RP のまま未活用）** | `Packages/manifest.json`（URP 不在） |

つまり現状は「Swift の見た目の忠実な複製 − ネイティブの磨き」なので、本家に負けるのは構造的必然。
Phase 4 の juice（FxKit/FxLayer/BeatPulse）は uGUI の枠内での演出であり、描画品質そのものは変えていない。

### 技術的な決定的事実（この設計の前提）

1. **Screen Space Overlay の Canvas にはポストプロセスが一切かからない**（Unity の仕様）。
   現在全 UI が Overlay（`MainSceneBuilder.cs:111`）なので、Bloom を欲しければ盤面を
   ワールド空間（カメラが描画する 3D 空間）へ出すしかない。→ HD-2D 化と目的が一致する。
2. **霧の情報判定は Core に一元化済み**（`GameSession.IsCellVisible` / 消失は `IsCellDisappeared`、
   鬼の表示可否は `GridBoardWidget.cs:156` が Core へ問い合わせ）。見た目をライトに変えても、
   「何が見えるか」の決定権は Core のまま = **情報パリティ（Swift と同じ情報量）を構造的に保証できる**。
3. **ビートの正本は `Conductor.SongPositionBeats`（dspTime 由来）**。演出は全てここから駆動する
   （`Time.time` 駆動は禁止 — 音とズレる）。BeatPulse が既にこの方式。
4. Unity 6 の `com.unity.ugui` 2.0.0 には **TextMeshPro が統合済み**（追加パッケージ不要）。
   ただし同梱フォントに日本語グリフは無い → 日本語フォントの同梱が必要。

---

## 2. ゴール / 非ゴール

### ゴール
- G1: 「Swift 版より見た目が良い」とオーナーが即答できる状態
- G2: Steam ストアページ / プロモ動画で映える画（ゾーンごとに画が変わる）
- G3: リズムゲームとしての手触り不変 — 入力遅延・判定・60fps を一切悪化させない
- G4: Core（`EscapeNine.Core.asmdef`, `noEngineReferences: true`）と EditMode テスト 80 本は無傷

### 非ゴール
- ✗ フル 3D リメイク（9 マスの視認性 = ゲームデザインの命。盤の読みやすさを壊す施策は全部却下）
- ✗ キャラの 3D モデル化（64×64 ドット絵はアイデンティティ。ビルボードのまま）
- ✗ メニュー系画面（Home/Shop/設定等）の 3D 化 — uGUI のまま Tier 1 品質向上のみ
- ✗ ネットワーク/収益化/ゲームルールへの変更

### 絶対制約（誠実性ルールの適用）
- **情報パリティ**: 霧・消失・透明化で「プレイヤーに見える情報」は Swift と 1:1。
  ライトの照り返し・Bloom の滲みで隠すべき鬼の位置が漏れる実装は不合格。
  判定は必ず Core の `IsCellVisible`/`IsCellDisappeared` を経由し、演出は結果に対する化粧に限る。
- **リズム精度**: W5 で Phase 0 ハーネス（`Phase0Harness.cs`）による実機再検証を人間ゲートにする。

---

## 3. アーキテクチャ — 「舞台」と「HUD」の 2 層

```
Main Camera (Perspective, 約35°俯瞰, URP + Volume)
 └─ ワールド空間 =「舞台」
     ├─ BoardStage        … 3D ジオラマ盤面（本設計の主役、新規）
     │    ├─ TileView ×9   … 3D ブロックタイル（消失ルールで崩落演出）
     │    ├─ PawnView ×2   … プレイヤー/鬼 = SpriteRenderer ビルボード（ドット絵のまま）
     │    ├─ StageLights   … ゾーン別ライト + 霧時のプレイヤー追従ライト
     │    └─ StageParticles… ParticleSystem（塵・火の粉・移動バースト）
     └─ ZoneBackdrop      … ゾーン別の背景セット（プリミティブ+グラデ空）

Canvas (Screen Space Overlay, 現行のまま)
 └─ HUD =「額縁」: 階層表示/スキル残数/ビートインジケータ/メニュー全画面
     （ポストプロセス非適用 = 常にシャープ。これは欠点でなく仕様として利用する）
```

- ロジックの流れは現行と完全同型: `GameController` → (`GameSession` を読む) → 描画側 `Render(session, ...)`。
  `BoardStage` は `GridBoardWidget` と同じ描画契約（`Render`/`SnapNextRender`/`ResetFxState`/
  `OnCellTapped`/`OnEnemyTapped`）を実装する = **GameController 側の変更は接続の差し替えのみ**。
- 入力: uGUI Button → **タイルの BoxCollider + Physics.Raycast**（タップ/クリック共通)。
  `KeyboardInput.cs`（デスクトップ）は無変更で動く（イベント契約が同じため）。
- シーンは現行どおり全てコード構築（`MainSceneBuilder` / 実行時生成）。`.unity` に UI を焼かない規約を維持。

---

## 4. 実装 Wave 分割（Sonnet 発注単位）

> 各 Wave の末尾ゲートは私（オーケストレータ）が直接実行: compile → EditMode 80 本 →
> PlayMode オートパイロット・スモーク → スクリーンショット/重なり監査 → macOS ビルド。
> ゲートを通らない限り次 Wave へ進まない。1 Wave = 1 コミット以上。

### Wave 0: URP 移行（前提工事・見た目は変えない）
- `com.unity.render-pipelines.universal` 導入（Unity 6000.3 推奨版に追従。manifest 編集後 `Client.Resolve()` 必須 — 既知の罠）
- URP Asset + Universal Renderer 生成（**2D Renderer ではない** — 3D ライト/メッシュを使うため）、
  Graphics/Quality 設定へ割当。カメラの postProcessing フラグ有効化
- 既存描画の互換確認: uGUI は無影響（Overlay）。カスタムマテリアル無しなので破壊リスクは低い
- **ゲート: 全画面スクリーンショットが移行前と一致（差分許容は色空間誤差のみ）+ テスト green + macOS ビルド成功**
- 規模: 小（Sonnet 1 体 + 私の検証）

### Wave 1: TextMeshPro 化（「Swift に追いつく」の主砲）
- 日本語 TTF を同梱（→ 決定 D3）し、`UITheme` に `TMP_FontAsset`（動的アトラス）解決を追加
  （現行 `ResolveFont()` と同じ「実行時生成・コード完結」パターンで、.asset を repo に増やさない）
- `UIFactory.Label` / `TextButton` を TMP 生成へ差し替え。**戻り値型が `Text`→`TMP_Text` に変わるため
  全画面の参照型を機械的に追随**（`grep -rln "UnityEngine.UI.Text"` で全数洗い出し → 一括変換）
- アウトライン/シャドウは TMP 標準機能で付与（現状の縁取りなし白文字から品質向上）
- 重なり監査スクリプト（OverlapAudit）を TMP の preferredHeight 対応へ更新
- **ゲート: 全 10 画面 + チュートリアル 6 ページの重なり監査 CLEAN + 目視スクリーンショット**
- 規模: 中（機械的だが接触ファイル多数。Sonnet 2 体: 変換 / 監査更新）

### Wave 2: BoardStage — 盤面のワールド空間化（構造の山場）
- 新規: `Runtime/Stage/BoardStage.cs`（描画契約は GridBoardWidget と同一）、`TileView.cs`、
  `PawnView.cs`（ビルボード + point filter スプライト）、`StageInput.cs`（Raycast → OnCellTapped）
- カメラを orthographic → perspective 俯瞰（約 35°）へ。`GameScreen` の盤面領域は透明化し、
  背景はワールド側 `ZoneBackdrop`（この Wave では単色 + 地平グラデのみ）が受け持つ
- Fx の移植: `FlashPlayer`（スプライト色）/ `BurstAtPlayer`（ParticleSystem 化）/
  `Shake`（次 Wave のカメラへ委譲、この Wave では盤ローカル揺れ）/ `ResetFxState`
- 旧 `GridBoardWidget` は**この Wave では削除しない**（切替は GameScreen 内の定数 1 箇所。→ 決定 D4）
- **ゲート: オートパイロットで 5 階層プレイ + 情報パリティ検査
  （霧で鬼非表示 / 消失マス表現 / 透明化表現が Core 判定と一致することをコード検査 + 目視）**
- 規模: 大（Sonnet 3 体 fan-out: BoardStage 本体 / 入力+カメラ / Fx 移植）

### Wave 3: ポストプロセス + カメラワーク（「音に乗ってる画」）
- URP Volume: Bloom / Vignette / Color Adjustments /（デスクトップのみ Depth of Field。→ 決定 D7）
- 新規 `BeatVolumePulse.cs`: `Conductor.SongPositionBeats` の小数部で Bloom 強度・Vignette を脈動
  （**dspTime 駆動固定。Time.time 禁止**。BeatPulse と同じ流儀）
- 新規 `CameraRig.cs`（自作 ~150 行。→ 決定 D2）: 衝突時インパルス / 階層クリアの回り込み /
  高階層ほど寄る圧迫ズーム。`GameController` のイベントから駆動
- `FxKit.HitStop`（≤0.1s）とカメラインパルスの重ね掛けで被弾の手応えを完成させる
- **ゲート: BPM 70 と 200 の両端で脈動が拍に吸着していることを録画で確認 + 実機負荷測定（後述予算内）**
- 規模: 中（Sonnet 2 体: Volume+Pulse / CameraRig）

### Wave 4: ゾーン別テーマ + 霧ライト + パーティクル（「別物」の完成）
- 新規 `ZoneThemes.cs`（コード定義の静的テーブル、ScriptableObject 不使用 — 定数一元管理の規約に合わせる）:
  赤鬼(1-25)=溶岩の暖色 / 青鬼(26-50)=氷洞の寒色 / 骸骨(51-75)=紫闇 / ドラゴン(76-100)=劫火、
  各エントリ = ライト色/強度・環境光・背景色・Bloom ティント・パーティクル種
- 霧ルールの演出替え: 現行「非表示」に加えて、環境光を落としプレイヤータイル上のポイントライトだけ灯す。
  **鬼の表示可否は従来どおり `IsCellVisible` — ライトは見えている物の照らし方を変えるだけ**（情報パリティ§2）
- 消失マス: タイルの崩落アニメ（下降+傾き+暗転）。`DisappearedCells` の判定は Core のまま
- ゾーン別アンビエントパーティクル（火の粉/雪片/霊魂/火の粉大）+ 移動/クリアのバースト強化
- **ゲート: 4 ゾーン各 1 階層をオートパイロットで通し、ゾーン切替（26/51/76 階）でテーマが遷移する録画確認**
- 規模: 中〜大（Sonnet 3 体: テーマ基盤 / 霧+消失演出 / パーティクル）

### Wave 5: 品質ティア + 性能実測 + 人間ゲート
- 品質ティア: Desktop=全部入り / iOS=Bloom 低+DoF なし / Android=設定で段階選択。
  設定画面の既存「演出設定」カードに「高品質演出」トグルを追加（低スペック救済 + アクセシビリティ）
- 実機性能実測（次節の予算に対して）。閾値割れならエフェクト単位で降格
- **人間ゲート: iOS 実機で Phase 0 リズム精度ハーネス再実行 → オーナー体感 GO/NO-GO**
  （URP 化 + perspective 化の後で初めて「音ゲーとして無傷」を宣言できる）
- ドキュメント反映（unity-migration-plan.md へ完了記録）
- 規模: 小〜中（Sonnet 1 体 + 実機は私+オーナー）

---

## 5. 性能予算（G3 の数値化）

| 項目 | 予算 | 測定方法 |
|---|---|---|
| フレームレート | iPhone 実機 60fps 張り付き（音ゲー必須） | Xcode GPU レポート / Unity Profiler |
| 1 フレーム GPU | iOS で ≤ 12ms（余裕 4ms） | 同上 |
| 入力→判定遅延 | Phase 0 実測値から悪化なし | Phase0Harness 再実行 |
| ドローコール | 舞台側 ≤ 60（タイル9+ポーン2+背景+パーティクル） | Frame Debugger |
| Bloom | iOS: Quality=Low, ダウンサンプル 2x | URP Volume 設定 |
| DoF | モバイル禁止（D7）。デスクトップのみ Gaussian | — |

超過時の降格順序（先に切るもの順）: DoF → Chromatic Aberration → パーティクル密度 → Bloom 解像度。
**ビート同期の脈動（BeatVolumePulse）は最後まで残す** — これがこのゲームの画の核だから。

---

## 6. リスク台帳

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R1 | URP 移行での想定外の描画劣化 | W0 で頓挫 | W0 を「見た目を変えない」ゲートで独立させ、崩れたら即 revert（1 コミット単位） |
| R2 | perspective 化で 9 マスの視認性が落ちる | ゲーム性毀損 | 俯瞰角は 30–40° の間で 3 案スクショ比較をオーナー判断に上げる。タイル判別性が最優先 |
| R3 | ライト/Bloom で霧の情報が漏れる | 情報パリティ違反 | §2 絶対制約。W2/W4 ゲートに「霧中の鬼が写らない」スクショ検査を含める |
| R4 | TMP 化で全画面の文字送りが変わり重なり再発 | W1 で崩れ | 既存の重なり監査を TMP 対応させ、全画面 CLEAN を機械判定（目視に頼らない） |
| R5 | リズム精度の劣化（描画負荷増による） | 音ゲー失格 | W5 人間ゲート。判定は Conductor（dspTime）系で描画と独立だが、体感は実機でしか分からない |
| R6 | モバイル GPU 予算超過 | iOS で 60fps 割れ | §5 の降格順序を事前定義。品質ティアを最初から設計に含める |
| R7 | 旧 GridBoardWidget と BoardStage の二重保守が長期化 | 技術負債 | D4: W4 ゲート通過後に旧実装を削除するコミットを必ず入れる（残置禁止） |

---

## 7. 触らないもの（明示）

- `Scripts/Core/` 全ファイル（asmdef の `noEngineReferences: true` が番人）と EditMode テスト
- `Conductor` の判定ロジック（`CheckMoveTiming`/`TimingGradeNow`）
- メニュー系画面のレイアウト値（W1 のフォント差し替えに伴う微調整のみ許可）
- DesktopPillarbox v2 / BuildScripts のウィンドウ設定（今日直したばかりの箇所）
- Firebase/課金/ランキング（Phase 3 の領分）

---

## 8. Sonnet 発注の型（コスト規律）

- 実装・機械的変換・調査は **全て model:'sonnet'**（feedback_subagent_model_sonnet 準拠）。
  Fable は設計（本書）・Wave 間ゲート判定・敵対的検証の裁定のみ
- 1 Wave = 1 ワークフロー。Wave 内の独立モジュールのみ fan-out（W2: 3 体 / W4: 3 体が最大）
- 各実装エージェントへ渡す背景 4 点固定: 目的（本書該当節）/ 描画契約（§3）/ 絶対制約（§2）/ 検証方法（該当ゲート）
- 敵対的検証は「情報パリティ」と「dspTime 駆動」の 2 観点を固定レンズにする（CONFIRMED のみ採用）

概算規模: W0=小, W1=中, W2=大, W3=中, W4=中大, W5=小中。
リミット断絶に備え、各 Wave はコンパイル green でコミット可能な粒度に割ってある（resumeFromRunId 前提）。

---

## 9. オーナー決定事項 — ✅ 2026-07-04 確定

> オーナー回答「よくなるなら全部任せます」により、**全項目を推奨案で確定**（D5 俯瞰角のみ W2 ゲートで
> スクショ 3 案比較の上オーケストレータが選定し、オーナーには事後報告）。
> 追加決定: **キャラクタースプライトの一新も許可**（アート生成は Opus 使用可）。生成候補は旧スプライトと
> 並べてオーナーへ事後提示し、差し替えは git で可逆にする。

| # | 決定 | 選択肢 | 推奨 |
|---|---|---|---|
| D1 | 適用プラットフォーム | 全プラットフォーム+品質ティア / デスクトップ先行 | **全プラットフォーム**（iOS が主戦場。ティアで守る） |
| D2 | カメラ実装 | 自作 CameraRig / Cinemachine 導入 | **自作**（要件が単純・依存増を回避・検証が決定的） |
| D3 | 日本語フォント | DotGothic16（ドット絵の顔） / Noto Sans JP(可読性) / 見出し=Dot+本文=Noto | **DotGothic16 単独**（アイデンティティ優先。可読性が問題になった画面のみ個別対応） |
| D4 | 旧盤面の扱い | W4 通過後に削除 / フラグで恒久併存 | **W4 通過後に削除**（二重保守の禁止、R7） |
| D5 | 俯瞰角 | 30° / 35° / 40°（W2 でスクショ 3 案を提示） | W2 ゲートでオーナーが選ぶ |
| D6 | ゾーン背景アート | プリミティブ+ライトのみで開始 / AI 生成背景画像 / 3D プロップ | **プリミティブ+ライトで開始**（W4 の画を見てから追加投資判断） |
| D7 | モバイル DoF | 全面オフ / iOS のみ低品質 | **全面オフ**（60fps 死守） |
| D8 | 実施順 | **Phase 4.5 → 5（ローグライク）** / 5 → 4.5 | **4.5 → 5**（宣伝素材が先に育つ + Phase 5 の新 UI を最初から新描画で作れる） |

---

## 10. 完了条件（Definition of Done）

- [ ] W0–W5 全ゲート通過（compile / EditMode 80 本 / オートパイロット / 監査 / ビルド）
- [ ] 4 ゾーンで画が明確に変わる録画が撮れている（プロモ素材として使える品質）
- [ ] 霧・消失・透明化の情報パリティ検査に合格（Swift と見える情報が 1:1）
- [ ] iPhone 実機 60fps + Phase 0 リズム精度ハーネス再合格（人間ゲート）
- [ ] 旧 GridBoardWidget 削除済み・重なり監査全画面 CLEAN
- [ ] unity-migration-plan.md へ完了記録
