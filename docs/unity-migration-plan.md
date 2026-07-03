# Unity 移行 &「強いゲーム」化 プラン ⭐️

作成日: 2026-07-01
ステータス: **提案 / 意思決定前（Phase 0 プロトタイプ・ゲートで GO/NO-GO 判断）**
背景: PostHog / Firebase の実測で判明した「配信ボトルネック」（公開3ヶ月で実プレイヤー約2名, `docs/aso/sprint-1-baseline.md` §4）を踏まえ、オーナー判断で **Unity 全面移行 + 新機能全振り** の方向に決定。
本書の役割: その決定を**成功させるための現実的な実行計画**と、正直なコスト・リスク・意思決定ゲートを提示する。

**実装状況 (2026-07-01)**: `unity/` に **Phase 0 (Conductor) + Phase 1 (コアロジックほぼ全域) の C# + EditMode テスト**を Swift 正本から忠実移植して配置済み。
移植済ロジック: BPM曲線 / 移動判定 / AI(Easy/Normal/Hard/Boss) / スキル・キャラ / **ターン進行(GameSession: 同時移動・すれ違い・透明化/盾/拘束・階層クリア・スキルリセット)** / デイリーチャレンジ(LCGシード生成) / Wordle風シェア / 実績判定 / コンボ判定。EditMode テスト 8 ファイルで Swift と同一入出力を担保。
ただし **Linux 環境のためコンパイル・実行は未検証**（Unity Editor 必須の作業は `unity/README.md` / `unity/setup/RUNBOOK.md` に人間タスクとして明記）。まず Editor で EditMode テスト green → Phase 0 実機検証 → GO/NO-GO の順で進める。
残りの Phase 1 で未移植: UI 結合層 (GameViewModel の @Published/音声/タイマー/カウントダウン)・永続化 (UserDefaults/Firestore) は Phase 2/3 で Unity 側 API に合わせて実装。

---

## 0. 方針（重要な期待値設定）

- **オーナー決定**: SwiftUI 版を Unity(C#) に作り直し、演出・ローグライク深化で「強いゲーム」にする。
- **本書のスタンス**: 決定を尊重し全力で実行計画を組む。ただし Unity 化の価値を正しく定義する:

> ✅ Unity の本当のペイオフ = **Steam + Android にワンソースで出せること**
> ❌ 「iOS 版の見た目が良くなる」だけなら、失う資産（後述）に見合わない

- **戦略的整合**: 現ASOは「9マス脱出ローグライク」（`docs/appstore-metadata.md`）。ローグライクは **Steam ネイティブなジャンル**で、Steam は App Store と違い"発見される場所"。
  → **Unity化 → ローグライク深化 → Steam(体験版) + Android展開** は「強いゲーム」と「配信」を同時に満たす一本線。これを本プランの背骨にする。

---

## 1. なぜ Unity か（unlock されるもの）

| 得られるもの | 内容 | 重要度 |
|---|---|---|
| クロスプラットフォーム | iOS / Android / **Steam(PC)** をワンソース | ★★★（最大の理由） |
| 圧倒的なゲームフィール | パーティクル / シェーダー / 画面シェイク / ポストプロセス。3×3 グリッドを"映える"体験に | ★★★ |
| ローグライク深化 | レリック・分岐・メタ進行など複雑な機構を組みやすい | ★★★ |
| 音楽リアクティブ演出 | BPM に反応する背景・エフェクト（音ゲーの中毒性の核） | ★★ |
| 短尺動画映え | TikTok/Reels 用の"派手な15秒"が撮りやすい＝集客素材 | ★★ |

**逆に言うと**、上記を使わない移行（＝ただ同じゲームを C# で再現するだけ）は投資に見合わない。移行するなら演出・ローグライク・マルチプラットフォームまで行き切ることが前提。

---

## 2. 移行スコープ — 何を捨て、何を作り直すか

現状 57 Swift ファイル。純粋ゲームロジックは**設計を流用**して C# に移植でき、ネイティブ SDK 群は**作り直し**。

| 現状 (Swift/iOS) | Unity での置き換え | 難易度 | 備考 |
|---|---|---|---|
| `BeatEngine`（AVAudioEngine ビート同期）**最重要** | `AudioSettings.dspTime` ベースの Conductor | ★★★ | リズム精度の要。Phase 0 で先に証明する |
| `GameEngine`/`AIEngine`/`Floor`/`Skill`/`GameState`（純ロジック） | C# へ移植（設計流用可） | ★☆☆〜★★ | ここは AI 支援が最も効く領域 |
| SwiftUI Views(20+) | Unity UI（uGUI または UI Toolkit）＋スプライト | ★★ | 全面再構築。3×3 は自作描画が自然 |
| Combine | C# event / UniTask / R3(UniRx) | ★☆☆ | |
| `StoreKitService`（StoreKit 2） | **Unity IAP** | ★★ | 商品ID・審査は流用可 |
| `AdMobService` + Unity Ads メディエーション | **Google Mobile Ads Unity plugin** or **Unity LevelPlay** | ★★ | LevelPlay なら Unity 純正で統合が楽 |
| `FirebaseService`（Auth/Firestore/Analytics） | **Firebase Unity SDK** | ★★ | Firestore ルール・スキーマ流用可 |
| `GameCenterService` | Apple の Unity Plug-Ins (GameKit) or サードパーティ | ★★★ | Android は Google Play Games に置換 |
| Facebook SDK | **Facebook Unity SDK** | ★☆☆ | |
| PostHog | **公式 Unity SDK 無し** → REST(`/capture`) ラッパー自作 | ★★ | `AnalyticsEvents.swift` のイベント定義を移植 |
| Haptics(`UIImpactFeedbackGenerator`) | iOS haptic plugin / Android Vibration | ★☆☆ | |
| 効果音8種(.wav) / BGM6曲 | そのまま流用（Unity AudioClip） | ★☆☆ | 資産再利用可 |
| ドット絵アセット(64×64) | そのまま流用（Sprite import） | ★☆☆ | 資産再利用可 |

**流用できる資産**: 音源・ドット絵・Firestore スキーマ・商品ID・バランス定数（`Constants.swift` の数値）・ゲーム設計（ロジックの"考え方"）・Analytics イベント名。
**作り直す資産**: 全 UI・全サービス統合・ビート同期エンジン・入力/アニメーション。

---

## 3. 段階的移行計画（ゲート付き）

### Phase 0 — ビート同期プロトタイプ【最重要 / 意思決定ゲート】 ⏱ 1〜2週
Unity 移行で唯一"技術的に落ちる"可能性があるのがリズム精度。**ここを最初に潰す。**
- `AudioSettings.dspTime` を使った Conductor（BGM の DSP 時刻から拍を逆算）
- 3×3 の最小プレイアブル（移動＋タイミング判定＋カウントダウン）
- iOS 実機で **Swift 版と同等かそれ以上のタイミング体感**を確認
- **ゲート判定**: 精度 OK → 本移行 GO / NG → Unity 断念 or ハイブリッド再検討

### Phase 1 — コアゲームロジック移植 ⏱ 2〜3週
- `Floor`(BPM曲線) / `GameEngine`(移動・当たり・同時移動) / `AIEngine`(Easy/Normal/Hard + 階層スケーリング) / `Skill`(5キャラ) / 特殊ルール(霧/消失/複合) を C# 移植
- `Constants.swift` の数値をそのまま `GameConfig`(ScriptableObject) に移す
- **EditMode ユニットテスト**で Swift 版と同じ入出力になることを担保（既存 `EscapeNine-endless-Tests` をC#移植）

### Phase 2 — UI/UX 再構築 ⏱ 2〜3週
- Home / Game / Result / Ranking / Shop / Character / Settings / Tutorial
- `ResponsiveLayout` の比率ベース思想を Unity の CanvasScaler + Safe Area で再現（iPad/iPhone 両対応の原則は維持）

### Phase 3 — 収益化・サービス再統合 ⏱ 2〜3週
- Unity IAP（wizard/elf/knight/removeads の4商品）
- 広告（LevelPlay or AdMob Unity）
- Firebase Unity（匿名Auth + Firestore ランキング + Analytics）
- PostHog REST ラッパー（既存イベント名を維持し前後比較を継続）
- Game Center / Google Play Games（プラットフォーム分岐）

### Phase 4 — ゲームフィール / 演出 "juice" ⏱ 2〜3週（強い差別化）
- ヒットストップ / 画面シェイク / パーティクル / コンボ VFX
- BPM 反応の背景パルス・音楽リアクティブ演出
- キャラのアニメーション（現状は静止画）
- 発射台型 Game Over をパーティクル込みで強化

### Phase 5 — ローグライク深化【新機能の本丸】 ⏱ 3〜4週
§4 の新機能群を実装。ASO の「ローグライク」表記を"本物"にする。

### Phase 6 — マルチプラットフォーム ⏱ 2〜4週
- **Android** ビルド（Google Play Games / Play Billing）
- **Steam 体験版**（ローグライクは Steam の Next Fest 等で発見されやすい）＝配信の実質的な解

### Phase 7 — リリース / 移行運用 ⏱ 1〜2週
- iOS は既存アプリを Unity 版で**アップデート差し替え**（新規アプリにしない＝レビュー/ランキング資産を維持）
- ストアメタデータは `docs/appstore-metadata.md` を流用

**合計の現実的レンジ（個人開発 + AI 支援, 週次でムラあり前提）: フィーチャーパリティまで 2〜4ヶ月、演出＋ローグライク＋マルチ展開まで含めて 4〜6ヶ月。**

---

## 4.「強いゲーム」新機能リスト（優先度付き）

ローグライク深化を軸に。★=インパクト大。

| # | 機能 | 内容 | 優先 | 依存 |
|---|---|---|---|---|
| 1 | **レリック/パーク** | 階層クリアごとに強化を選択（例: ジャスト判定拡大 / スキル+1 / 1回復活）。ラン毎に別ビルド | ★★★ | Unity Phase5 |
| 2 | **ゲームフィール全面強化** | ヒットストップ・シェイク・パーティクル・音楽反応。中毒性の核 | ★★★ | Unity Phase4 |
| 3 | **メタ進行** | ラン跨ぎの永続アンロック（新キャラ/スキン/初期レリック）。継続動機 | ★★★ | Phase5 |
| 4 | **分岐ルート** | 次階層を「安全/高リスク高報酬」から選ぶ。戦略性 | ★★ | Phase5 |
| 5 | **ボスパターン** | 現ボス階(10の倍数)を"動きのパターン"を持つ本物のボスに | ★★ | Phase4-5 |
| 6 | **音楽リアクティブ背景** | BPM で脈動する演出。短尺動画映え=集客素材 | ★★ | Phase4 |
| 7 | **リプレイ/ゴースト** | 自己ベストのゴースト表示・リプレイ共有 | ★ | Phase5+ |
| 8 | **Steam 実績/カード** | Steam ネイティブのリテンション装置 | ★ | Phase6 |

> 補足: #2/#5/#6 の一部は**現 Swift 版にも今すぐ入れられる**（Unity 移行の数ヶ月を待たずに"つなぎの強化"が可能）。移行中もiOS版を凍結せず小改善を続けたい場合の候補。

---

## 5. リスクと対策

| リスク | 深刻度 | 対策 |
|---|---|---|
| **ビート同期がUnityで落ちる** | 高 | Phase 0 で最初に証明。dspTime 必須（Time.time は不可）。NG なら移行中止 |
| **移行中リリースが止まり、その間の集客ゼロ** | 高 | iOS版を凍結せず §4 の小改善を並行投入。Steam 到達を最優先KPIに |
| **数ヶ月かけても"配信"は自動解決しない** | 高 | Unity の目的を「Steam+Android で発見される」に固定。作って終わりにしない |
| Game Center → GPG のプラットフォーム分岐が煩雑 | 中 | ランキングは Firestore を正とし、GC/GPG は"表示連携"に格下げ |
| PostHog Unity SDK 不在 | 中 | REST `/capture` の薄いラッパーを自作（イベント名は現行維持） |
| Firebase Unity のビルドサイズ/依存肥大 | 中 | 使う Product を Auth/Firestore/Analytics に限定 |
| 個人開発の工数（4〜6ヶ月）が枯渇 | 中 | Phase 単位で"出荷可能"に区切る。各Phase後にGO/NO-GO |
| 既存iOSアプリの資産(レビュー/順位)喪失 | 低 | 新規アプリにせず**アップデート差し替え**で移行 |

---

## 6. 意思決定ゲート

```
[今ここ] 本プラン承認
   ↓
Phase 0: ビート同期プロトタイプ (1-2週)  ←★最重要
   ↓
  ゲート判定: リズム精度 OK ?
   ├─ YES → Phase 1〜 本移行へ全力
   └─ NO  → Unity 断念 / ハイブリッド(ゲームロジックのみC#共有) を再検討
```

**「いきなり全部Unityで作り直す」前に、Phase 0 の1〜2週だけ先行投資してリスクを潰す**のが唯一の安全弁。ここだけは省略しないことを強く推奨。

---

## 7. 未確定事項（次アクションで詰める）

- [ ] Unity バージョン（LTS 推奨。2022 LTS or 6 LTS）と入力系（New Input System）
- [ ] UI 方式（uGUI か UI Toolkit か）
- [ ] 広告メディエーション（LevelPlay か AdMob Unity か）
- [ ] Steam 展開の本気度（体験版 → Next Fest 参加の是非）
- [ ] iOS 版を移行中に凍結するか、小改善を並行するか
- [ ] リポジトリ構成（現 repo に `unity/` を同居させるか、別 repo にするか）

---

## 8. Fable 5 開始後の実行計画（2026-07-01 改訂）

### 変わった前提

1. **Fable 5 が利用可能に** → `/finish-unity` + ultracode での実行フェーズへ移行
2. **クレジット従量消費が発生中**（プラン付属枠超過 → extra usage 課金） → **トークン = 実費**。ultracode の無計画な全自走は禁物

### コスト階層（Tier）— 安い順に実行する

| Tier | 手段 | コスト | 対象作業 |
|---|---|---|---|
| **0** | シェルのみ（AI不要） | ほぼ無料 | `bootstrap.sh` 実行 / EditModeテスト / Phase0シーン生成 / iOS実機ビルド |
| **1** | 通常 Claude Code（ultracode なし） | 小 | コンパイルエラー修正 / 小さな結線 / MCP設定 |
| **2** | ultracode + **予算指定必須**（`+200k` 等） | 大 | UI再構築 / ローグライク設計 / Swift↔C# 敵対的検証 |

**ルール**: 機械的な作業に ultracode を使わない。マスター全自走ではなく `/finish-unity <N>` でフェーズ単位。ultracode 起動時は必ず予算指定を付け、`/usage` でクレジット残高を随時確認。

### 改訂フェーズ順（§3 の順を上書き）

```
Phase 0 (Tier 0-1) → Phase 1 検証 (Tier 0) → Phase 2 UI (Tier 2)
→ Phase 4 juice (Tier 2) → Phase 6a Steam体験版 (Tier 1-2) ★前倒し
→ Phase 5 ローグライク (Tier 2) → Phase 3 収益化 (Tier 1) ★後ろへ
→ Phase 6b Android → Phase 7 リリース
```

**順序変更の理由**:
- **Phase 3（収益化再統合）を後ろへ**: 実プレイヤー約2名の現状で IAP/広告の結線は価値が低い。人が入ってから結線しても遅くない。
- **Phase 6a（Steam体験版）を前倒し**: 体験版に IAP/広告は不要（むしろ無い方がよい）ので Phase 3 に依存しない。Steam は"発見される場所"であり、Next Fest 等への露出 = 集客ボトルネックの実質的な解。UI + juice が揃った時点で最短で出せる。
- **Phase 4（juice）は Phase 2 直後**: 短尺動画映えする15秒素材が撮れる状態を早く作る（これも集客素材）。

### ✅ Phase 0 ゲート通過（2026-07-02）

- Unity 6.3 LTS (6000.3.19f1) / EditMode テスト **60/60 green**（Unity 上で確認）
- Phase 0 シーン（Conductor + Phase0Harness + bgm_early）が Editor・**iOS 実機**で動作
- **リズム精度: オーナー実機判定で GO** → 本移行を正式に続行。次は MCP 設定 → Phase 2 (UI) へ

### ✅ Phase 2 完了（2026-07-03） — UI/UX 再構築

- **uGUI + 単一シーン画面切替**で 8 画面を実装（Home/Game/Result/Ranking/Shop/CharacterSelect/Settings/Tutorial）。UI は全てコードで自己構築（`.unity` に UI を焼かない Phase0SceneBuilder 流儀）
- 新規 C#: UI 基盤（UITheme/UIFactory/ScreenRouter/SafeAreaFitter）+ 結線層（App/GameController=GameViewModel移植/PlayerState=PlayerViewModel移植/AudioDirector=AudioManager移植/RankingStore）+ 画面13ファイル + MainSceneBuilder/PixelArtImportSettings
- アセット移送: スプライト9・SFX9・BGM6 を `Assets/Resources/` へ（Point filter/PPU64/SFX=DecompressOnLoad 自動適用）
- **検証済み**: コンパイル green / EditMode テスト **80/80 green**（既存60+新規20）/ PlayMode スモーク＝起動→チュートリアル自動表示→Home→開始カウントダウン→floor1 を5ターン逃げ切りクリア→階層クリア演出→floor2→敗北（敵に捕まった）→Result（NEW RECORD・敗因表示・ニアミス表示）→リトライ、を Editor 上でオートパイロット実証。時間切れ敗北も別ランで実証
- 環境注意: `~/EscapeNineUnity/Packages/manifest.json` に `com.unity.ugui: 2.0.0` が必要（bootstrap 素マニフェストに無い。PackageBootstrap の自動追記スクリプトも同梱）
- 意図的な先送り: IAP/広告/Firebase/GameCenter/ネイティブ共有＝Phase 3、アニメ・パーティクル・scale演出・BGMフェード・カスタムフォント＝Phase 4、デイリーチャレンジ画面＝Phase 2.5、**iOS 実機での見た目・SafeArea 確認は未実施**（次の実機ビルド時に確認）
- **敵対的検証（Swift↔C# 差分レビュー 3レーン）実施済み**: コア数値は完全一致を確認（GameColors 15色 / バランス定数全項目 / BPM曲線 / 鬼階層帯 / Product ID / BGMマッピング / メトロノーム音源パラメータ）。CONFIRMED 差分 9 件は修正済み（ポーズ連打の時間切れ回避穴 / リリースビルドでデバッグ設定有効 / 開始カウントダウン1秒固定化 / GO後0.5s入力ブロック / 衝突死ターンのmove音 / 実績チェック結線+永続化 / Boss難易度選択 / 透明化ONバッジ / v1.1オンボーディング判定+デイリーボタン準備中トースト+DEBUG設定2項目）
- **Phase 2.5 バックログ**（検証で確定した残移植、次スプリントで対応）: ① デイリーチャレンジのフロー統合（開始経路・条件適用・完了記録）と画面 ② 実績 UI（Home ボタン+一覧+Result 解除ポップアップ。判定・永続化は結線済み） ③ デイリーチャレンジ完了状態表示
- **意図的差分として維持**（文書化）: AI難易度の永続化（Swift はセッション毎 Easy リセット→UX 改善として保持）/ 壊れセーブ時に勇者を残す安全化 / SFX 音量 0.7 統一（Swift の 0.8/0.7 二重経路の負債解消）/ Conductor.CheckMoveTiming の判定窓（現行フロー未使用 API。使用時に要再検討）/ FloorClear 中のポーズ不可（Swift は可）

### ✅ Phase 4 完了（2026-07-03） — ゲームフィール (juice)

- **FX 基盤**: FxKit（PunchScale/ShakeRect/Flash/HitStop/SlideIn、コルーチンベース・外部Tweenライブラリなし）/ FxLayer（プール式 UI スプライトバースト。ParticleSystem 不使用＝Overlay Canvas と描画順問題を回避）/ BeatPulse（Conductor 拍位相同期の脈動）
- **Reduce Motion 対応**: PlayerState.ReduceMotionEnabled + 設定画面トグル。全エフェクトが FxKit.MotionEnabled の単一ゲートを通る
- **盤面演出**: 移動ホップ(squash&stretch) / 透明化吸収=紫Flash+バースト / 盾=青Flash / 敗北=HitStop0.08s+盤面シェイク+赤Flash+バースト / GO!演出 / 階層クリア=SlideIn+金バースト / 盤面BeatPulse / コンボ3・5で色エスカレーション / 霧・消失のクロスフェード化
- **Result 発射台**: DEFEAT が上から落下+着地シェイク / スコアカード時差 SlideIn / NEW RECORD 金バーストループ / 巨大リトライの拍パルス / ニアミス赤Flash / BeatIndicator 拍ヒット発光 / BPM 値変更ポップ
- **検証済み**: コンパイル green / EditMode 80/80 / PlayMode 耐久スモーク＝オートパイロットが **floor 1→16 を260秒・例外ゼロ**で走破（BPM変化・ボス階10・スキルリセット・AI自然スケーリング跨ぎ）。スクショ3枚（盤面/クリア演出/Result floor16 NEW RECORD）取得
- 技術判断の記録: カメラシェイク不可（Overlay）→ RectTransform シェイク / timeScale HitStop は dspTime 駆動 Conductor と干渉しない0.1s以下限定 / BGMフェードは未実装のまま（次スプリント候補）
- 既知の軽微リスク（統合レビュー申告）: FxKit 演出中に host の StopAllCoroutines が走ると scale が中間値で固着し得る（発生頻度極小・視覚影響軽微。FloorClear オーバーレイは位置リセットで対処済み）/ 演出検知が「OnTurnResolved が OnStateChanged より先」というイベント順序に依存

### ✅ Phase 6a 技術基盤完了（2026-07-03） — Steam体験版のデスクトップ対応

- **DesktopPillarbox**: 横長ウィンドウで縦画面コンテンツ (1170:2532) を中央カラム拘束 + 左右暗色フィル。モバイルでは無効
- **KeyboardInput**: 盤面 1-9 / QWEASDZXC / テンキー(空間反転) → 移動、F/Shift=スキル、B=拘束、Esc/P=ポーズ、Space/Enter=階層クリア進行・リトライ・プレイ開始
- **BuildScripts.BuildMac()**: メニュー + batchmode 両対応、結果マーカーファイル出力方式
- **検証済み**: EditMode 80/80 / **macOS スタンドアロンビルド成功（errors=0, 186.6MB）→ 実起動確認（ピラーボックス動作・Player.log 例外ゼロ・スクショ取得）**
- 残タスク（人間ゲート/環境）: Steamworks 連携（Steam アカウント/AppID 必要）/ Windows ビルド（Unity Hub で Windows Build Support モジュール追加が必要、現状 mac/iOS/Android のみ）/ Steam ストアページ・体験版設定
- 既知の軽微課題: キーボード移動時に JUST/GOOD 表示バナーが出ない（内部状態は正常）/ デスクトップでチュートリアル説明文がカード枠を僅かにはみ出す

### 前段検証の結果（2026-07-01, リモート環境の .NET 8 で実行済み）

- **Core コンパイル + 全 60 テスト green**（`unity/verify/Core.Tests`, C# 9 固定）→ ゲート①のリスクは大幅低減
- **バランスシミュレーション 15構成×1000ラン**（`unity/verify/BALANCE_REPORT.md`）主要所見:
  完璧タイミングでも100階クリアはほぼ不可能 / 魔法使いが突出（中央値34-66階）/ 盗賊が最弱 /
  Hard AI が魔法使い相手に Normal より弱い創発 → **Phase 5 のバランス設計の一級資料**

### 最初の90分（今日やること — ほぼ Tier 0）

1. `git pull` → `bash unity/setup/bootstrap.sh`（AI不要）
2. EditMode テスト全 pass 確認 = **検証ゲート①**（コンパイルエラーが出たら Tier 1 で修正）
3. `bash unity/setup/install-slash-command.sh` → MCP設定（RUNBOOK §3、Tier 1）
4. Phase 0 シーンを iOS 実機へ → **リズム精度 GO/NO-GO**（人間ゲート）
5. GO なら `/finish-unity 2` を**予算付き**で起動（ここが最初の Tier 2）

---

## 9. 関連ドキュメント

- 配信ボトルネックの根拠: `docs/aso/sprint-1-baseline.md` §4（実プレイヤー約2名）
- 現行ゲーム仕様: `docs/game-spec.md` / `docs/要件定義書_EscapeNine.md`
- 現行技術仕様: `docs/DEVELOPMENT_SWIFT.md`
- ASO / ポジショニング（ローグライク）: `docs/appstore-metadata.md`
- 収益化統合: `docs/収益化設定ガイド.md`
