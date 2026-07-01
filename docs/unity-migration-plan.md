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

## 8. 関連ドキュメント

- 配信ボトルネックの根拠: `docs/aso/sprint-1-baseline.md` §4（実プレイヤー約2名）
- 現行ゲーム仕様: `docs/game-spec.md` / `docs/要件定義書_EscapeNine.md`
- 現行技術仕様: `docs/DEVELOPMENT_SWIFT.md`
- ASO / ポジショニング（ローグライク）: `docs/appstore-metadata.md`
- 収益化統合: `docs/収益化設定ガイド.md`
