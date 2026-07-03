# Unity Phase 5「ローグライク深化」設計文書 ⭐️

作成日: 2026-07-03 / 改訂: 2026-07-04（Phase 4.5 ビジュアル大型アップグレード決定に伴う練り直し・改訂者: Fable）
ステータス: **設計提案 v2（実装未着手）** — 本書は `docs/unity-migration-plan.md` §4 のPhase 5スコープを具体化する設計書。コード変更は含まない。

**v2 の変更点サマリ**: Phase 4.5（`docs/unity-phase4-5-visual-upgrade-design.md`、HD-2D化）を**先に実施する前提**へ改訂。
Phase 5 の新規 UI は最初から新描画基盤（TMP / ワールド空間 BoardStage / URP ポストプロセス）の上に作り、
レリック・分岐・ボスの各機構に §1.5 の演出統合を追加した。旧 §9-8（消失マスとボス威圧の視覚混同）は §1.5 で解消。
**ゲームメカニクス設計（§2〜§6 の数値・構造・回帰ガード）は v1 から不変** — バランス根拠（BALANCE_REPORT）に変化はないため。

## 0. 参照文書 / グラウンディング

- `docs/unity-migration-plan.md` §4（新機能リスト。レリック/パーク★★★・メタ進行★★★・分岐ルート★★・ボスパターン★★の優先度）
- `unity/verify/BALANCE_REPORT.md`（15構成 × 1000ラン ヘッドレスシミュレーション。**本書の設計判断の一次根拠**）
- `docs/game-spec.md`（現行バランス定数・キャラ5種・BPM曲線・特殊ルール階層帯）
- 既存 Core 実装: `unity/EscapeNine/Assets/Scripts/Core/{GameSession,GameConfig,Floor,Character,Skill,AIEngine,GameEngine,GameEnums,Achievement,DailyChallenge}.cs`
- 既存 Runtime 実装: `unity/EscapeNine/Assets/Scripts/Runtime/{GameController,PlayerState,Conductor}.cs`
- `unity/verify/Sim/Program.cs`（ヘッドレスバランスシミュレータ。バランス検証の再利用基盤）
- `docs/unity-phase4-5-visual-upgrade-design.md`（v2 で追加: 先行フェーズ。BoardStage/ZoneThemes/CameraRig/TMP が本書の演出前提）

### BALANCE_REPORT.md の要点（設計の一次根拠）

| 所見 | 数値 | Phase 5 での扱い |
|---|---|---|
| 完璧タイミングでも100階クリアはほぼ不可能 | 全15構成で勝率 0.0〜0.1% | §8 未決事項へ（ゴール設定はオーナー判断） |
| 魔法使いが圧倒的に強い | 中央値 34〜66階（他キャラ 2〜25階） | §2.2 弱点タグ付きドラフトで対処（魔法使いは強化レリックの重みを下げる） |
| 盗賊が最弱 | 中央値 2〜7階（勇者未満） | §2 レリック一覧で「盗賊救済」タグを最多配分 |
| Hard AI が魔法使いに対し Normal より弱い（創発） | Wizard-Hard 56.8 vs Wizard-Normal 44.2（中央値は66 vs 34） | §2.2・§6 で「AI改変レリックは魔法使い所持時に除外」ルール化。§8 で根本対処（AIEngine修正）は別問題として保留 |

**重要な実装上の制約（Core精読で判明。設計全体を貫く前提）**:
1. `AIEngine.NormalAI`/`HardAI` は**確率要素なし**（`GetMoveTowardsPlayer` は常に最短距離）。「敵の追跡確率を下げる」系レリックは Easy/Boss（`_rng.NextDouble()` を使う）にしか効かない。Normal/Hard には無効。
2. タイミング許容（`GameConfig.TimingTolerance`）は **`Runtime/Conductor.cs` にのみ存在**し、Core (`GameSession`) は関知しない。タイミング系レリックは Runtime 側でも値を受け渡す設計が必要。
3. Unity版の生存判定は「オフビート即死」ではなく「**ターン締切（`TurnCountdown`）に間に合うか**」が中心。ビートタイミングの Just/Good/Miss は**コンボ倍率にのみ影響**し、直接死には繋がらない（Swift由来だが Unity 実装では役割が変わっている点に注意）。
4. ボスは HP を持たない（10の倍数階の「AI難易度ラベル」に過ぎない）。「ボスパターン」は体力フェーズではなく**移動アルゴリズムの切り替え**として設計する必要がある。

---

## 1. 設計原則

1. **Core優先・非破壊**: 新機構は `EscapeNine.Core`（エンジン非依存の純 .NET）に実装し、`GameSession` の公開APIは**追加のみ**（既存メソッドの削除・破壊的シグネチャ変更は禁止）。受け入れ条件: レリック未装備（`Relics = RelicEffects.None`）で走らせたとき、既存 EditMode テスト 80/80 が**無改変で green** であること。
2. **既存バランス定数は不可侵、効果は加算オーバーレイ**: `GameConfig` の数値そのものは変更しない。レリックの効果は新設の `RelicEffects` を経由して判定式に上乗せする「オーバーレイ」として実装する（Swift正本との数値パリティ文書 — Phase 2 の敵対的検証記録 — を壊さないため）。
3. **弱点タグ付きドラフトプールこそが唯一のバランス是正レバー**: レリックは加算型の恩恵である以上、キャラクター非依存の強化レリック（例: 「全キャラ スキル+1」）は魔法使いにも等しく乗り、BALANCE_REPORT が指摘する格差を**むしろ拡大**しうる。したがって「何のレリックを作るか」ではなく「**誰にどのレリックを優先的に見せるか**（ドラフトプールの重み付け）」を主軸のバランス調整手段と位置づける。魔法使いには弱点対抗系・全キャラ強化系レリックの出現重みを意図的に下げる。§6 のシミュレーション計画で「キャラ間ギャップが縮む」ことを **pass/fail 基準として明示的に検証する**（単にレリックを16〜20種並べるだけでは、報告書の所見に表面的に言及しただけで構造的には裏切ることになる）。
4. **ラン長さの目標は実測レンジを基準にする**: BALANCE_REPORT の中央値は 2〜66階（大半のキャラが10階台前半で死亡）。Phase 5 は「弱いキャラ/AI選択の中央値を底上げする」ことを目標とし、「100階踏破を標準的な到達点にする」ことは目標としない（100階の扱いは §8 でオーナー判断とする）。
5. **AI改変系レリックはシミュレーション必須、危険な組み合わせは明示的に除外する**: AI挙動を変えるレリックはキャラクター×選択AIレベルに依存する非自明な効果を持つ（実例: 魔法使いは Hard AI の方が Normal より弱いという創発が実測されている）。AI改変系レリックは出荷前に必ずキャラ×AI全構成のシミュレーションで確認し、有害な組み合わせ（例: 「Hard AIの予測を無効化」レリック × 魔法使い＝改悪）はドラフトプールから**明示的に除外**する（重みを下げるだけでなく、除外ルールとしてコードに残す）。

---

## 1.5 Phase 4.5 基盤との演出統合（v2 で追加）

Phase 4.5 完了後の描画基盤（URP ポストプロセス / ワールド空間 BoardStage / ZoneThemes / CameraRig / TMP）を
Phase 5 の各機構がどう使うか。**機構をメニューで説明するのではなく、舞台の上で見せる**のが方針 —
ローグライクの「選択の重み」は文字情報より先に画で伝わるべきで、これこそ Unity 移行の回収ポイント。

| Phase 5 機構 | Phase 4.5 基盤の使い方 |
|---|---|
| レリックドラフト | レアリティ別発光 = URP Bloom 閾値超えのエミッシブ色で「Legendary が物理的に光る」。カード自体は HUD 層（TMP）、背後の舞台は被写界深度でボケさせ視線をカードへ集める（DoF はデスクトップのみ、Phase 4.5 D7） |
| レリック取得の瞬間 | BoardStage 上のプレイヤーポーンへレアリティ色のライト + パーティクルバースト（StageParticles 流用） |
| 分岐ルート（安全/深淵） | 選択 UI の背後で舞台照明をプレビュー遷移: 深淵側に選択カーソルを合わせると ZoneThemes の照明が赤黒く沈む（「深淵は画で怖い」を選ぶ前に見せる）。確定でカメラが一段寄る（CameraRig） |
| ボスパターン・テレグラフ | ②先読み = 予測先タイルが 1 拍前に青白く明滅（TileView エミッシブ、Conductor 拍同期）/ ③威圧 = 対象タイルが赤熱 + ひび割れ。ボス階進入時は CameraRig の回り込み + ズームで「ボス感」 |
| ボス階の空気 | ボス階は現行 ZoneThemes の**強調モード**（ライト強度↑・Vignette↑・BeatVolumePulse 振幅↑）。テーマ資産の追加なしで済む設計 |
| メタショップ | HUD 層の通常画面（uGUI + TMP）。舞台は使わない（買い物に舞台演出は不要） |

**旧 §9-8（恒久消失マスとボス威圧マスの視覚的混同）はこれで解消**:
恒久消失 = タイルが**崩落して存在ごと消える**（Phase 4.5 W4 実装）/ ボス威圧 = タイルは**存在するが赤熱して踏めない**。
「存在の有無」で区別するため、色だけに頼らず色覚多様性にも頑健。最終的な見た目確認は Phase 5c ゲートでオーナーが行う。

**依存関係と疎結合の担保**: 上記演出は Phase 4.5 の W2（BoardStage）・W3（ポスト+CameraRig）・W4（ZoneThemes/崩落）
完了が前提。ただし機構と演出は `GameController` のイベント境界（`OnRelicDraftOffered` / `OnRouteChoiceOffered` /
`OnBossPatternChanged`）で疎結合にする — 万一 Phase 4.5 が遅延しても、§7 の各スプリントから演出項目だけ落として
機構を先に出荷し、演出を後追いできる構造を保つ。UI 層が BoardStage の内部へ直接触ることは禁止（イベント経由のみ）。

---

## 2. レリック/パークシステム

### 2.1 ドラフトの仕組み

- **タイミング**: 階層クリア時（既存の `IsFloorClearPending` ゲートと同じタイミング）に3択を提示する。ビートクロックは既にこの間停止している（`GameController.Phase.FloorClear`）ため、新規の一時停止プリミティブは不要。
- **頻度 [要検証]**: デフォルト仮説は「毎階層クリア時」。プール非重複が基本のため、弱いキャラ（盗賊、中央値2〜7階）はプールを枯渇させることはまずないが、魔法使い（中央値34〜66階）はプール枯渇の可能性がある → 一部レリックを「スタック可（上限あり）」にすることで緩和する（§2.3 参照）。「毎階層は多すぎる」と実機プレイテストで判明した場合は「6階層目以降・2階層に1回」等へ変更できるよう、頻度は `RelicConfig.DraftInterval` として定数化し、コード変更なしにチューニングできるようにする。
- **キーボード操作必須（Steam体験版対応）**: Phase 6a でデスクトップ/キーボード入力（`KeyboardInput.cs`、1-9 移動・F/Shift スキル・Esc/P ポーズ）が実装済みで、Steam体験版はこの入力系が前提。ドラフトUIも**1/2/3キーで選択できること**を5aスコープの必須要件とする（タッチ専用UIのまま出荷すると体験版ビルドで詰む）。
- **重複ルール**: 原則「1ラン中に同じレリックは1回まで」。一部レリック（霧視界拡大等、環境緩和系）は「スタック可・上限あり」に指定する。
- **プール枯渇時のフォールバック**: 未所持レリックが3種未満になった場合、候補を「まだ上限に達していないスタック可レリック」で埋める。それも尽きた場合は §3 のメタ通貨をボーナス付与する（レリックの代わりに通貨を渡す）。

### 2.2 弱点タグと重み付け設計

弱点タグ: `ThiefRescue`（盗賊救済） / `HardAICounter`（Hard AI対抗） / `LateGame`（終盤対策、階層41+/61+） / `General`（汎用強化） / `Safety`（セーフティネット） / `Score`（スコア/コンボ系）

ドラフト候補の重み = `baseRarityWeight(rarity) × tagMultiplier(tags, character, selectedAI, floor)`

`tagMultiplier` の設計（初期仮説、[要検証]）:

| 条件 | 対象タグ | 倍率 |
|---|---|---|
| character == 盗賊 | ThiefRescue | ×3.0 |
| character == 盗賊以外 | ThiefRescue | ×0.15（完全排除はしない＝フレーバー上のクロスピックは許容） |
| selectedAI == Hard **かつ** character != 魔法使い | HardAICounter | ×2.5（勇者Hard中央値4・盗賊Hard中央値2・エルフHard中央値5・ナイトHard中央値7 — 魔法使い以外は軒並みHardに潰される） |
| character == 魔法使い | HardAICounter | **×0（ドラフト候補から除外）** — Hard vs 魔法使い創発の悪化を防ぐ明示ルール（原則5） |
| floor >= 41（マス消失開始） | LateGame | ×2.0 |
| floor >= 61（霧+消失開始） | LateGame | ×2.5 |
| character == 魔法使い | General | ×0.5（既存優位の増幅を抑える） |
| character in {勇者, エルフ} かつ selectedAI in {Hard, Boss} | Safety | ×1.3（Hard中央値がいずれも一桁台） |

基準レアリティ出現率（初期仮説、[要検証]）: Common 45% / Uncommon 30% / Rare 18% / Epic 6% / Legendary 1%

### 2.3 レリック一覧（18種）

検証区分の凡例:
- **Tier1**: Core (`GameSession`) の状態にのみ作用し、既存ヘッドレスシミュレータ（常に Just 判定前提）で定量検証できる
- **Tier2**: リアルタイムの拍圧力・タイミング判定に作用し、現行シミュレータ（常時完璧タイミング前提）では効果が測定できない。実機/プレイテストでの検証が必要（§6 に詳細）

| # | 名前 | レアリティ | 効果 | 弱点タグ | 検証区分 |
|---|---|---|---|---|---|
| 1 | 影の軽業 | Common | 盗賊の斜め移動発動時、25%の確率でスキル残数を消費しない | ThiefRescue | Tier1 |
| 2 | 残像のヴェール | Rare | 斜め移動を使った直後の1ターン、敵の移動をEasy相当（追跡15%/逃走20%/残りランダム）に強制する | ThiefRescue / HardAICounter | Tier1 |
| 3 | 影分身の型 | Epic | 盗賊のスキル最大使用回数 +3（盗賊専用） | ThiefRescue | Tier1 |
| 4 | 老練の構え | Uncommon | 現在の必要ターン数 -1（最低3） | General / Safety | Tier1 |
| 5 | 不死鳥の残り火 | Epic | 1ラン1回、鬼との衝突による敗北を無効化し継続する | Safety | Tier1 |
| 6 | コンボの守り | Rare | 1ラン1回、Miss判定でもコンボを継続させる | Score | Tier1（効果はコンボ倍率のみ。ヘッドレスsimは常時Just前提のため定量検証は限定的） |
| 7 | 地固めの護符 | Uncommon | マス消失の発生数 -1（最低0、階層41+で有効） | LateGame | Tier1 |
| 8 | 灯火の指輪 | Common（スタック可・上限3） | 霧の視界半径 +1マス（階層21+で有効） | LateGame | Tier1 |
| 9 | 幻惑の粉 | Epic | 選択AIがHardの階層で、敵AIの予測追跡をNormal相当に格下げする | HardAICounter | Tier1・**魔法使いはドラフト対象外**（§2.2） |
| 10 | 護りの起点 | Common | 階層開始時、プレイヤーと敵の初期配置距離を2マス以上保証する | General / Safety | Tier1 |
| 11 | 影の抜け道 | Rare | 1階層につき1回、消失マスへの進入による敗北を無効化する | LateGame / Safety | Tier1 |
| 12 | 二段構えの盾 | Rare | 盾スキルを持たないキャラでも、1ラン1回だけ衝突を無効化する即席の盾を発動する | Safety | Tier1 |
| 13 | 予備の呼吸 | Uncommon | 全キャラのスキル最大使用回数 +1（魔法使いは§2.2でドラフト重み減） | General | Tier1 |
| 14 | 連鎖の証 | Common | コンボ倍率のしきい値を1早める（3→2, 5→4） | Score | Tier1 |
| 15 | 加速の証（リスク型） | Uncommon | 以後のBPM +8%、代わりにコンボ倍率しきい値到達時の倍率 +0.5 | Score / リスク | Tier2（BPM操作は生存難度に影響するため実質プレイテスト頼み） |
| 16 | 刻の猶予 | Rare | 1ターンの締切拍数 +1（Runtime専用、`_turnBeats` 加算） | LateGame | Tier2（現行simはターン締切のリアルタイム圧力をモデル化していない） |
| 17 | 心話の絆 | Rare | 拘束スキルを持たないキャラでも、鬼をタップすると1ラン2回まで1ターン停止させられる | HardAICounter / General | Tier1 |
| 18 | 蒐集家の目 | Legendary | 以後3階層、レリックドラフトの候補数が3→4に増える（パワー自体は増えない） | General | Tier1（ドラフト候補生成数の一時変更） |

> 命名はプレースホルダー。最終コピーは実装時に `frontend-design`/コピー担当が別途詰める前提。
>
> #1（影の軽業）の25%判定は `GameSession` 内の既存 `_rng`（`IRandomSource`）ストリームから引く想定。盗賊の斜め移動消費時にのみ発火するため、`Relics = RelicEffects.None` の回帰テスト経路（§6.4）はこの分岐を一切通らず、既定挙動のバイト単位一致は保たれる。

### 2.4 GameSession への統合点（フック一覧）

新設 `public RelicEffects Relics { get; set; } = RelicEffects.None;` を `GameSession` に追加し、以下の既存箇所が参照する（すべて既存メンバの**内部実装変更**であり、シグネチャは維持）:

| 既存箇所 | 変更内容 |
|---|---|
| `MaxTurns` getter | `GameConfig.GetMaxTurns(CurrentFloor) - Relics.TurnCountReduction`（floor 0 プロローグは対象外、最低3にクランプ） |
| `RemainingSkillUses` | `Skill.MaxUsage + Relics.SkillMaxUsageBonus - SkillUsageCount` |
| `GetNumberOfDisappearingCells` | 算出値から `Relics.DisappearCellReduction` を減算（0未満は0） |
| `IsCellVisible` | 霧のChebyshev半径を `1 + Relics.FogVisibilityRadiusBonus` に |
| `ResolveTurn` の AI 呼び出し | `Relics.NeutralizeHardPrediction` が true かつ実効AIレベルがHardのとき、その1回の呼び出しだけNormalとして `_ai.CalculateNextMove` を呼ぶ（表示上のAIレベルは変えない） |
| `ResolveTurn` の衝突分岐（`isCollision`） | `Relics.ReviveCharges > 0` または `Relics.GenericShieldCharges > 0` を優先チェックしてから、既存の透明化/盾判定に進む |
| `ResolveTurn` の消失マス分岐 | `Relics.DisappearForgivenessPerFloor` の残数があれば、このターンの消失マス進入を無効化する（1階層につき1回） |
| `SelectMove` のコンボリセット | `grade == Miss` かつ `Relics.ComboMissShieldCharges > 0` なら `ComboCount` を維持し、チャージを消費する |
| `ScoreMultiplier` | しきい値定数から `Relics.ComboThresholdReduction` を減算 |

Runtime側（Core非依存を維持するため `GameSession` には持ち込まない）:

| 箇所 | 変更内容 |
|---|---|
| `GameController._turnBeats` | `Relics.TurnCountdownBonus`（レリック#16）を加算 |
| `Conductor.CheckMoveTiming` / `TimingGradeNow` | 呼び出し側（`GameController`）が `Session.Relics` の値を渡せるよう、オプション引数（既定0）を追加。既存呼び出し元は無改修で動作 |
| BPM決定（`Floor.CalculateBPM` の結果を使う箇所） | `GameController` が `Relics.BpmMultiplierBonus`（レリック#15）を乗算してから `Conductor.ChangeBPM` に渡す |

---

## 3. メタ進行

### 3.1 通貨

名称（仮）: **残光（ざんこう）**。1ラン終了時に付与する。

```
残光 = 到達階層 + floor(到達階層 / 5) * 2 + (勝利なら +100) + (デイリーチャレンジ達成なら +20)
```
[要検証・仮の式。ラン頻度と欲しいアンロック速度から逆算してチューニングする]

### 3.2 永続アンロック対象（IAPと競合しない設計）

**大原則**: 残光は「有料キャラクター（魔法使い/エルフ/ナイト、各¥240）の価値を毀損しない」対象にのみ使う。既存の収益化設計（`docs/収益化設定ガイド.md`）と衝突させない。

| 対象 | 単価/条件 | IAPとの関係 |
|---|---|---|
| コスメティック（キャラ配色違い・盤面テーマ） | 残光で購入 | 完全に無関係（パワーに影響しない）。最も安全な"収集"フック |
| レリックプールの拡張 | 該当レリックで到達した最高階層に応じ**無料**で恒久解放、または残光で早期解放 | 全キャラ共通のレリックプールを広げるだけで、特定の有料キャラを無料化しない |
| スターターパーク（1枠のみ） | 残光で解放した Common/Uncommon レリックの中から1つを「ラン開始時装備」として選べる | Rare以上は対象外（パワーの天井を上げすぎない）。ドラフト消費なしで付与される点が通常レリックと異なる |
| 6人目の無料キャラクター（将来枠） | 残光の大量消費でアンロック（新規コンテンツ） | 既存の無料/有料キャラの価値には触れない、新規追加という扱いにする |

**やらないこと**: 残光で魔法使い/エルフ/ナイト本体を購入可能にする、残光で勇者/盗賊を恒久的に魔法使い相当の性能にする、のいずれも禁止（¥240課金モデルの毀損）。

### 3.3 PlayerPrefs スキーマ（`PlayerState.cs` への追加、既存キー方式を踏襲）

既存の `unlockedCharacters` / `purchasedProductIDs` と同じ「CSV文字列シリアライズ + 明示的 `Save()`」方式に合わせる。

| キー | 型 | 内容 |
|---|---|---|
| `metaCurrency` | int | 残光の残高 |
| `unlockedRelicIds` | string (CSV) | ドラフトプールに解放済みのレリックID |
| `unlockedCosmeticIds` | string (CSV) | 解放済みコスメティックID |
| `starterPerkRelicId` | string (nullable) | 現在装備中のスターターパーク |
| `lifetimeRelicsCollected` | int | 累計レリック取得数（将来の実績候補、§8参照） |

---

## 4. 分岐ルート

- 階層クリア後（§2 のレリックドラフトと同じタイミング、レリックドラフトの**前**に提示する）、「安全なルート」と「深淵のルート」の2択を提示する。
  - **安全なルート**: 現行仕様通り（`Floor.GetSpecialRule(CurrentFloor)` / `Floor.GetEffectiveAILevel(...)` をそのまま使う）
  - **深淵のルート（リスク型）**: この1階層限定で、実効AIレベルを1段引き上げる（Easy→Normal→Hard、Hardは据え置き）、または特殊ルールを1段階前倒しする（例: 本来素の階層より1段階厳しい霧/消失を適用）。代わりに、直後のレリックドラフトに **Rare以上を1枠確定で含める** + 残光ボーナスを付与する。
- **頻度 [要検証]**: 初心者への配慮としてFloor 6以降から提示開始（チュートリアル直後の階層は選択肢を増やさない）。
- **デイリーチャレンジとの相互作用**: デイリーチャレンジは既に `ChallengeCondition`（`ForcedAI`/`StartFloor`等）でランの構造を固定しているため、公平性（同じ日替わりシードに挑む全プレイヤーの条件を揃える）を優先し、**デイリーチャレンジ中は分岐ルート選択を無効化**する（常に安全なルート扱い）。レリックドラフトと同じ理由・同じ扱い。
- **アーキテクチャ**: `GameSession.NextFloor()` に `RouteChoice choice = RouteChoice.Safe` を追加（デフォルト値のため既存呼び出し元 `session.NextFloor()` は無改修で動作）。選択結果は「その1階層限定」の一時オーバーライドとして保持し、次の `NextFloor()` 呼び出しで自動的にクリアする。

---

## 5. ボスパターン

現状のボス（10の倍数階）は HP を持たない「AI難易度ラベル」（`AIEngine.BossAI`: 追跡95% / ランダム5%）に過ぎない。ボスを「動きのパターンを持つ本物のボス」にするには、**体力フェーズではなく移動アルゴリズムの切り替え**として設計する。

### 5.1 パターン案（3種）

| パターン | 内容 | 実装根拠 |
|---|---|---|
| ① 追跡（Pursuit） | 既存の `BossAI` をそのまま流用（追跡95%/ランダム5%） | 変更なし、パターンの1つとして再利用 |
| ② 先読み（Foresight） | `HardAI.PredictPlayerMove` を流用し、プレイヤーの回避先を予測してそこへ向かう（決定論的、乱数なし） | 既存ロジックの転用のみ、新規AI実装は不要 |
| ③ 威圧（Intimidation） | ボスに隣接する1マスを1ターンだけ「一時的に進入不可」としてマークする（特殊ルールの恒久消失マスとは別枠の一時セット） | `DisappearedCells` とは独立した `TemporaryBossZone` セットを新設し、既存の特殊ルール判定と混同しないようにする |

### 5.2 ローテーションと難易度ランプ

- ボス階内で **2ターンごとに** ①→②→③→① と固定順で循環する（学習可能性を優先。ランダム循環にしない）。
- Floor 10〜30 のボスは①②のみ循環（2パターン）。Floor 40以降で③が加わる（3パターン）。40という区切りは既存の `GameConfig.AINaturalHardFloor`（=36）の直後に来るボス階（10の倍数）を採用し、既存の難易度ランプ哲学と整合させる。
- UI側は次のパターンを1ターン前にテレグラフ表示する（`GameController.OnBossPatternChanged` イベントを新設）。表現は §1.5 のとおり **BoardStage のタイル発光/赤熱**が主で、HUD 側は補助表示に留める。Phase 4.5 未完了時のフォールバックは Phase 4 の `FxKit`/`BeatPulse` 流用。

### 5.3 統合点

- `AIEngine` に `CalculateBossMove(enemyPosition, playerPosition, pattern, turnIndexInFloor)` のオーバーロードを追加（既存 `CalculateNextMove` は変更しない）。
- `GameSession` に `CurrentBossPattern` プロパティとパターン用のターンカウンタを追加し、`ResolveTurn` のAI呼び出しがボス階のときだけこの新メソッドを経由する。
- Tier1（Core完結、乱数以外はリアルタイム非依存）のため、既存ヘッドレスシミュレータでパターン別の生存率データを取得できる。
- **レリックとの交差点（明示的な仕様決定）**: ボス階では AI 呼び出しが `effective`（`Floor.GetEffectiveAILevel`）ではなく `CalculateBossMove` を経由するため、`effective` を書き換える形のAI改変レリック（#9 幻惑の粉、#2 残像のヴェール）はボス階では**無効（no-op）**になる。#9 は `GetEffectiveAILevel` がボス階で常に `Boss` を返すため自然に対象外になるが、#2（斜め移動後の1ターンEasy強制）はボス階でも発動条件自体は満たしうるため、明示的に「ボスパターンを上書きしない」を仕様として固定する（レリックがボスの読み合いを無力化しないようにするための意図的な決定）。

---

## 6. 実装アーキテクチャ

### 6.1 新規 Core クラス（`EscapeNine.Core`、エンジン非依存を維持）

- `RelicEffects.cs` — 加算オーバーレイのデータ保持クラス。全フィールドの既定値でゼロ効果になる `RelicEffects.None` を提供。
- `RelicDefinition.cs` / `RelicCatalog.cs` — レリックの静的データ（ID/名前/説明/レアリティ/弱点タグ/`RelicEffects` への適用デルタ）。`Character.GetCharacter(type)` と同じ「静的ファクトリ」の作法に合わせる。
- `RelicDraftService.cs` — 純粋ロジック。`IRandomSource` 注入で決定論化（既存の乱数注入パターンを踏襲）。§2.2 の重み付け・重複排除・スタック可否・プール枯渇フォールバックを実装。
- `RouteChoice.cs` — enum + 1階層限定オーバーライドの保持構造。
- `BossPattern.cs` — enum。`AIEngine` の拡張メソッドとセットで追加。
- `MetaProgressionCalculator.cs`（任意） — 残光算出の純関数。`AchievementChecker` と同じ「静的・副作用なし」の作法。

### 6.2 GameSession への変更（§2.4 / §4 / §5 参照、すべて追加的変更）

まとめ: `Relics` プロパティ追加、`NextFloor(RouteChoice)` のデフォルト引数追加、`CurrentBossPattern` 追加。既存の公開メンバの削除・シグネチャ破壊なし。

### 6.3 Runtime への変更

- `GameController`: 新規イベント `OnRelicDraftOffered` / `OnRouteChoiceOffered` / `OnBossPatternChanged`。新規メソッド `ChooseRelic(string relicId)` / `ChooseRoute(RouteChoice)`。新規ゲート `IsRelicDraftPending` / `IsRouteChoicePending`（既存 `IsFloorClearPending` と同じ設計思想）。順序: `FloorCleared` → (該当階層なら) `RouteChoice` → `RelicDraft` → `AdvanceToNextFloor` 解禁。
- `PlayerState`: §3.3 のキー追加（既存キー・既存シリアライズ方式は無変更）。
- `KeyboardInput.cs`: `IsRelicDraftPending` / `IsRouteChoicePending` が true の間のみ、1/2/3キーを選択肢に割り当てる（既存の Space/Enter=階層クリア進行と競合しないよう、ドラフト解決後に Advance が有効化される順序を維持）。
- 新規UI: `RelicDraftScreen` / `RouteChoiceScreen`（5c） / `MetaShopScreen`（5b+）。`ScreenBase` / `UIFactory`（Phase 4.5 W1 以降は TMP 生成）を再利用し、演出は §1.5 の舞台統合（Bloom レアリティ発光・照明プレビュー・タイルテレグラフ）を使う。舞台への指示は `GameController` イベント経由に限定。

### 6.4 EditMode テスト戦略

- `RelicEffectsTests` — 各効果フィールドを個別に設定し、`GameSession` の挙動が期待通り変わることを確認（Tier1レリックのみ対象）。
- `RelicDraftServiceTests` — 固定シードでの決定性、N回抽選での重み分布が許容誤差内か、重複禁止/スタック可ルールの正しさ、「魔法使い所持時に#9(幻惑の粉)が候補から除外される」ルールの検証。
- `BossPatternTests` — 固定ターンインデックスでのパターン循環順序、Floor 40未満では③が出現しないことの検証。
- **回帰ガード（最重要）**: 既存 `GameSessionTests` は無改変のまま green を維持することを前提とし、加えて `Relics = RelicEffects.None`（既定値）が固定シードスクリプトに対して Phase 5 以前と**バイト単位で同一の挙動**を再現することを明示的にアサートする回帰テストを1本追加する。

### 6.5 ヘッドレスシミュレーションでのバランス検証計画

`unity/verify/Sim/Program.cs` を拡張（または `Sim.Relics` 相当の別モードを追加）し、§2.2 の重み付けドラフトを組み込んだ簡易方策（レリック提示時は最高レアリティ優先、同率はキャラの弱点タグ一致を優先）で、キャラ×AI 15構成 × 1000ラン を「レリック無効（現行baseline）」「レリック有効」の両方で実行し `BALANCE_REPORT_PHASE5.md` を生成する。

**Pass/Fail 基準（[要検証]の初期仮置き値）**:

a. 盗賊の各AI構成の中央値が、レリック有効化前より**上昇すること**（必須）
b. Wizard-Hard の中央値が、レリック有効化後も Wizard-Normal の中央値を**大きく下回らないこと**（既存の逆転現象を悪化させない）
c. 「魔法使いの中央値 − 他4キャラ平均中央値」の差が、有効化前より**縮小すること**（拡大したら設計ミスとして要修正。これが原則3の実行可能な検証）
d. 100階クリア率が有効化前（0.0〜0.1%）から極端に上振れしないこと（初期仮置きの警告ライン: 10%超）

**既知の検証ギャップ**: Tier2レリック（#6, #15, #16 = コンボの守り・加速の証・刻の猶予）は、現行シミュレータが「常に Just 判定・拍圧力なし」を前提にしているため、上記シミュレーションでは効果を定量評価できない。BPM連動のタイミングミス確率を近似する合成ノイズモデルをシミュレータに追加すれば評価可能になるが、これは Phase 5 の必須スコープではなく任意の拡張として扱う（§8）。Tier2レリックは実機/プレイテストでの定性検証に留める。

---

## 7. スコープ分割

各フェーズは1スプリント単位で出荷可能に区切る。5a は Steam体験版に載せられる最小構成を意識する。

**前提（v2）**: 改訂フェーズ順（migration-plan §8）どおり **Phase 4.5 完了後に着手**。
新規画面を旧描画（legacy Text）で作って後から TMP へ二度直しする無駄を避けるため。

### Phase 5a（1スプリント）— 最小出荷可能構成

- Core: `RelicEffects` / `RelicDefinition` / `RelicCatalog`（8種、各弱点タグを最低1つ含む代表的サブセット: #1, #2, #4, #7, #8, #10, #12, #17）/ `RelicDraftService`（毎階層クリア時ドラフト、重み付けなしのシンプル抽選で可、スタック上限あり）
- Runtime: `GameController` の `IsRelicDraftPending` ゲート・`ChooseRelic`
- UI: `RelicDraftScreen`（**1/2/3キーで選択可能。Steam体験版のキーボード操作を前提にした必須要件**）+ レアリティ Bloom 発光と取得バースト（§1.5。Phase 4.5 未完了なら色表示のみに縮退）
- テスト: `RelicEffectsTests` + 回帰ガード（`Relics = RelicEffects.None` の挙動不変性）
- **含まないもの**: メタ通貨、分岐ルート、ボスパターン、弱点タグ重み付け（8種は均等抽選でよい）

### Phase 5b（1スプリント）

- レリックを18種に拡充、§2.2 の弱点タグ付き重み付けドラフト実装（魔法使い除外ルール含む）
- `unity/verify/Sim` の Relics 拡張 + `BALANCE_REPORT_PHASE5.md` 生成、§6.5 の pass/fail 基準の検証
- メタ進行: 残光通貨、`PlayerState` スキーマ拡張、コスメティック購入、レリックプールの階層到達アンロック
- `MetaShopScreen`

### Phase 5c（1スプリント）

- 分岐ルート（`RouteChoice`、`RouteChoiceScreen` + 深淵の照明プレビュー §1.5、デイリーチャレンジ中の無効化ルール）
- ボスパターン3種 + 舞台テレグラフ（タイル明滅/赤熱 §1.5）+ `OnBossPatternChanged` + ボス階の ZoneThemes 強調モード
- （オーナー承認があれば）実績追加候補: 「1ランで5種以上のレリック取得」「Epic以上のレリック取得」— `AchievementChecker` へのシグネチャ追加が必要なため要判断（§8）
- Steam実績（migration plan §4 の #8、★=最低優先度）は Phase 5 スコープ外、Phase 6c 送りとする（スコープクリープ防止のため明記）

### Sonnet 発注の型（コスト規律、v2 で追加）

- 実装・シミュレータ拡張・テスト追加は全て **model:'sonnet'**（feedback_subagent_model_sonnet 準拠）。Fable は設計改訂・スプリント間ゲート判定・敵対的検証の裁定のみ
- 1 スプリント = 1 ワークフロー。fan-out の目安 — 5a: Core（レリック8種+テスト）/ Runtime（ゲート+UI）の 2 体。5b: カタログ拡充 / 重み付けドラフト+シミュレータ / メタ進行+ショップ の 3 体。5c: 分岐ルート / ボス+テレグラフ の 2 体
- 各エージェントへ渡す固定コンテキスト 4 点: 本書該当節 / 設計原則 1〜5（§1）/ BALANCE_REPORT 該当所見 / 回帰ガード条件（§6.4）
- 敵対的検証の固定レンズ 2 本: 「`Relics = RelicEffects.None` でバイト単位挙動不変か」「魔法使い格差が拡大していないか（§6.5 基準 c）」— CONFIRMED のみ採用

---

## 8. 既存機能との相互作用（デイリーチャレンジ/実績/コンボ）

- **デイリーチャレンジ**: 既に `ChallengeCondition`（CharacterLock/NoSkillAllowed/ForcedAI/StartFloor）でランの条件を固定しており、同一日に挑む全プレイヤー間の公平性が前提の機能。レリックドラフト・分岐ルートは**デイリーチャレンジ中は無効化**する（v1の方針。将来「日替わりシードでレリックも同一に固定して提示する」拡張は§8未決事項として残す）。
- **実績（Achievement）**: 現行 `AchievementChecker.CheckAchievements(floor, skillUsed, currentBPM, gameWon)` はレリックを引数に取らない。Phase 5c で「レリック取得数」系の実績を追加する場合、シグネチャに `relicsCollected` 等を追加する必要がある（追加的変更のため原則1に反しない）が、これは既存実績体系の拡張であり必須スコープではない。オーナー判断待ち。
- **コンボシステム**: `SelectMove` の `ComboCount` リセット判定（Miss で0にリセット）に、レリック#6（コンボの守り）が割り込む。`ScoreMultiplier` のしきい値そのものにもレリック#14（連鎖の証）が作用するため、両者が同時に有効な場合の相互作用（しきい値を下げた上でMissを1回だけ無視する）を実装時にテストケースとして明示すること。

---

## 9. 未決事項（オーナー判断が必要）

1. **レリックはラン限りか永続混在か**: 本書はデフォルトを「全レリックはラン限り」とし、唯一の永続要素として「スターターパーク」1枠（Common/Uncommon限定、メタ進行でアンロック）を提案した。これで十分か、恒久ステータスアップのような別の永続システムが必要かはオーナー判断。
2. **デイリーチャレンジとレリックの将来的な両立**: v1は無効化で確定させたが、「日替わりシード固定でレリックも公平に提示する」方向へ将来拡張するかどうか。
3. **「100階クリアほぼ不可能」という所見への向き合い方**: Phase 5 を「弱いキャラ/AIの底上げ」と「100階クリア自体を現実的な目標にする強い上振れの許容」のどちらに寄せるかで、レリックの総パワー予算の目標値が変わる。§6.5 の pass/fail 基準(d)（100階クリア率10%超で警告）は仮置きであり、オーナーが目標ラインを決める必要がある。
4. **Hard AI が魔法使いに対して Normal より弱いという創発への対処方針**: これを `AIEngine.cs` 自体のバグとして直接修正する（Swift正本との差分が生じ、原則1の「非破壊」対象を超える）のか、現状維持のまま「幻惑の粉」のような対策レリック・除外ルールで扱う仕様として固定するのか。両立不可な選択であり明示的な決定が必要。
5. **残光とIAPの関係**: Phase 3（収益化再統合）は後回しになっているが、将来「残光をIAPで購入可能にするか」は既存の有料キャラクター課金モデル（¥240×3種）との整合を要する意思決定（P2W懸念）。本書は現時点でIAP接続を提案しない。
6. **レリックドラフトの頻度**: §2.1 で「毎階層クリア時」をデフォルト仮説としたが、実プレイでのペース感（UI操作の煩雑さ、高到達ラン時の総ドラフト回数）を実機プレイテストで確認してから確定すべき値。
7. **Tier2レリックの定量検証**: 現行ヘッドレスシミュレータは「常にJust判定」前提のため、#6/#15/#16 の効果を数値で裏付けられない。BPM連動ミス確率の合成ノイズモデルをシミュレータに追加するか、実機プレイテストのみで良しとするかはオーナー判断（§6.5参照）。
8. ~~ボス「威圧」パターンとマス消失特殊ルールの視覚的混同~~ → **v2 で解消**（§1.5: 恒久消失=タイル崩落で存在ごと消える / 威圧=存在するが赤熱して踏めない。存在の有無で区別）。オーナーの見た目確認だけ Phase 5c ゲートに残す。
9. **レリックのアイコンアート（v2 で追加）**: v1 出荷はテキスト + レアリティ色 + Bloom 発光のみで可能。18種のドット絵アイコンを作るか、作るなら AI 生成か手描きかはオーナー判断（Phase 4.5 の決定 D6=ゾーン背景アートと同じタイミングでまとめて決めるのが効率的）。

---

## 関連ドキュメント

- 移行計画・優先度の根拠: `docs/unity-migration-plan.md`
- 先行フェーズ（演出基盤）: `docs/unity-phase4-5-visual-upgrade-design.md`
- バランスの一次データ: `unity/verify/BALANCE_REPORT.md`（本書の判断根拠。Phase 5b で `BALANCE_REPORT_PHASE5.md` を追加生成予定）
- 現行ゲーム仕様: `docs/game-spec.md`
- 収益化設計（残光とIAPの非競合を確認する際の参照）: `docs/収益化設定ガイド.md`
