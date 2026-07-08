// RelicCatalog.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.3 (レリック一覧) / §7 Phase 5a/5b に基づく。
// Swift正本には存在しない (Unity固有の追加機能)。
//
// Phase 5a スコープ: 18種のうち、各弱点タグを最低1つ含む代表的サブセット8種のみ実装
// (#1, #2, #4, #7, #8, #10, #12, #17)。
// Phase 5b スコープ: 残り10種 (#3, #5, #6, #9, #11, #13, #14, #15, #16, #18) を追加し18種完結。
// 弱点タグ重み付けドラフトは RelicDraftService 側 (ComputeWeight) で実装。
// 命名は設計書の通りプレースホルダー (最終コピーは実装時に別途詰める前提、§2.3 末尾の注記)。

namespace EscapeNine.Core
{
    public static class RelicCatalog
    {
        // ID定数 (Runtime/永続化からの参照用)
        public const string ShadowFootworkId = "shadow_footwork";     // #1 影の軽業
        public const string AfterimageVeilId = "afterimage_veil";     // #2 残像のヴェール
        public const string ShadowCloneArtId = "shadow_clone_art";    // #3 影分身の型 (5b)
        public const string VeteranStanceId = "veteran_stance";       // #4 老練の構え
        public const string PhoenixEmberId = "phoenix_ember";         // #5 不死鳥の残り火 (5b)
        public const string ComboWardId = "combo_ward";               // #6 コンボの守り (5b)
        public const string GroundingCharmId = "grounding_charm";     // #7 地固めの護符
        public const string LanternRingId = "lantern_ring";           // #8 灯火の指輪
        public const string BewilderingDustId = "bewildering_dust";   // #9 幻惑の粉 (5b)
        public const string SafeStartId = "safe_start";               // #10 護りの起点
        public const string ShadowPassageId = "shadow_passage";       // #11 影の抜け道 (5b)
        public const string ImprovisedShieldId = "improvised_shield"; // #12 二段構えの盾
        public const string ReserveBreathId = "reserve_breath";       // #13 予備の呼吸 (5b)
        public const string ChainProofId = "chain_proof";             // #14 連鎖の証 (5b)
        public const string AccelProofId = "accel_proof";             // #15 加速の証 (5b, リスク型)
        public const string GraceOfTimeId = "grace_of_time";          // #16 刻の猶予 (5b)
        public const string HeartboundPactId = "heartbound_pact";     // #17 心話の絆
        public const string CollectorsEyeId = "collectors_eye";       // #18 蒐集家の目 (5b)

        private static readonly RelicDefinition[] Definitions =
        {
            // #1 影の軽業 (Common, ThiefRescue) — 盗賊の斜め移動発動時、25%の確率でスキル残数を消費しない。
            new RelicDefinition(
                ShadowFootworkId,
                "影の軽業",
                "盗賊の斜め移動発動時、25%の確率でスキル残数を消費しない",
                RelicRarity.Common,
                RelicTag.ThiefRescue,
                stackLimit: 1,
                applyDelta: e => e.ThiefDiagonalSkillSaveChance = System.Math.Min(1.0, e.ThiefDiagonalSkillSaveChance + 0.25)),

            // #2 残像のヴェール (Rare, ThiefRescue/HardAICounter)
            // — 斜め移動を使ったターン、敵の移動をEasy相当 (追跡15%/逃走20%/残りランダム) に強制する。
            new RelicDefinition(
                AfterimageVeilId,
                "残像のヴェール",
                "斜め移動を使ったターン、敵の移動をEasy相当に強制する",
                RelicRarity.Rare,
                RelicTag.ThiefRescue | RelicTag.HardAICounter,
                stackLimit: 1,
                applyDelta: e => e.ThiefResidualVeil = true),

            // #4 老練の構え (Uncommon, General/Safety) — 現在の必要ターン数 -1 (最低3)。
            new RelicDefinition(
                VeteranStanceId,
                "老練の構え",
                "現在の必要ターン数-1 (最低3)",
                RelicRarity.Uncommon,
                RelicTag.General | RelicTag.Safety,
                stackLimit: 1,
                applyDelta: e => e.TurnCountReduction += 1),

            // #7 地固めの護符 (Uncommon, RequiresDisappear) — マス消失の発生数 -1 (最低0、階層41+で有効)。
            // RELIC_COHERENCE_AUDIT.md §2-A: マス消失ルールが無い階層でドラフト候補から完全除外するため
            // 旧 LateGame から RequiresDisappear へ変更 (RelicDraftService.ComputeWeight で0除外)。
            new RelicDefinition(
                GroundingCharmId,
                "地固めの護符",
                "マス消失の発生数-1 (最低0)",
                RelicRarity.Uncommon,
                RelicTag.RequiresDisappear,
                stackLimit: 1,
                applyDelta: e => e.DisappearCellReduction += 1),

            // #8 灯火の指輪 (Common, RequiresFog) — 霧の視界半径+1マス (階層21+で有効)。
            // RELIC_COHERENCE_AUDIT.md §2-C: 霧が無い階層 (41-60含む) で候補から完全除外するため
            // 旧 LateGame から RequiresFog へ変更。
            // stackLimit=1: 3x3盤の Chebyshev 最大距離は2、視界半径 1+bonus は bonus=1 で全マス可視
            // (霧を完全無効化) になるため bonus=2/3 は挙動を一切変えない死にスタック。2枚目以降を
            // 提示しないよう上限1にする (設計 §2.3 も上限1へ修正)。
            new RelicDefinition(
                LanternRingId,
                "灯火の指輪",
                "霧の視界半径+1マス",
                RelicRarity.Common,
                RelicTag.RequiresFog,
                stackLimit: 1,
                applyDelta: e => e.FogVisibilityRadiusBonus += 1),

            // #10 護りの起点 (Common, General/Safety) — 階層開始時、プレイヤーと敵の初期配置距離を2マス以上保証する。
            new RelicDefinition(
                SafeStartId,
                "護りの起点",
                "階層開始時、プレイヤーと敵の初期配置距離を2マス以上保証する",
                RelicRarity.Common,
                RelicTag.General | RelicTag.Safety,
                stackLimit: 1,
                applyDelta: e => e.MinStartDistance = System.Math.Max(e.MinStartDistance, 2)),

            // #12 二段構えの盾 (Rare, Safety) — 盾スキルを持たないキャラでも、1ラン1回だけ衝突を無効化する。
            new RelicDefinition(
                ImprovisedShieldId,
                "二段構えの盾",
                "盾スキルを持たないキャラでも、1ラン1回だけ衝突を無効化する",
                RelicRarity.Rare,
                RelicTag.Safety,
                stackLimit: 1,
                applyDelta: e => e.GenericShieldCharges += 1),

            // #17 心話の絆 (Rare, HardAICounter/General) — 拘束スキルを持たないキャラでも、鬼をタップすると1ラン2回まで拘束できる。
            new RelicDefinition(
                HeartboundPactId,
                "心話の絆",
                "拘束スキルを持たないキャラでも、鬼をタップすると1ラン2回まで拘束できる",
                RelicRarity.Rare,
                RelicTag.HardAICounter | RelicTag.General,
                stackLimit: 1,
                applyDelta: e => e.PseudoBindCharges += 2),

            // --- Phase 5b で追加 (残り10種、§2.3) ---

            // #3 影分身の型 (Epic, ThiefRescue, 盗賊専用) — 盗賊のスキル最大使用回数+3。
            new RelicDefinition(
                ShadowCloneArtId,
                "影分身の型",
                "盗賊のスキル最大使用回数+3 (盗賊専用)",
                RelicRarity.Epic,
                RelicTag.ThiefRescue,
                stackLimit: 1,
                applyDelta: e => e.ThiefSkillMaxUsageBonus += 3),

            // #5 不死鳥の残り火 (Epic, Safety) — 1ラン1回、鬼との衝突による敗北を無効化し継続する。
            new RelicDefinition(
                PhoenixEmberId,
                "不死鳥の残り火",
                "1ラン1回、鬼との衝突による敗北を無効化し継続する",
                RelicRarity.Epic,
                RelicTag.Safety,
                stackLimit: 1,
                applyDelta: e => e.ReviveCharges += 1),

            // #6 コンボの守り (Rare, Score, Tier2) — 1ラン1回、Miss判定でもコンボを継続させる。
            new RelicDefinition(
                ComboWardId,
                "コンボの守り",
                "1ラン1回、Miss判定でもコンボを継続させる",
                RelicRarity.Rare,
                RelicTag.Score,
                stackLimit: 1,
                applyDelta: e => e.ComboMissShieldCharges += 1),

            // #9 幻惑の粉 (Epic, HardAICounter) — 選択AIがHardの階層で、敵AIの予測追跡をNormal相当に格下げする。
            // §2.2: 魔法使いはドラフト対象外 (RelicDraftService.ComputeWeight で weight=0)。
            new RelicDefinition(
                BewilderingDustId,
                "幻惑の粉",
                "選択AIがHardの階層で、敵AIの予測追跡をNormal相当に格下げする",
                RelicRarity.Epic,
                RelicTag.HardAICounter,
                stackLimit: 1,
                applyDelta: e => e.NeutralizeHardPrediction = true),

            // #11 影の抜け道 (Rare, RequiresDisappear/Safety) — 1階層につき1回、消失マスへの進入による敗北を無効化する。
            // RELIC_COHERENCE_AUDIT.md §2-B: #7と同じ理由で RequiresDisappear へ変更。
            new RelicDefinition(
                ShadowPassageId,
                "影の抜け道",
                "1階層につき1回、消失マスへの進入による敗北を無効化する",
                RelicRarity.Rare,
                RelicTag.RequiresDisappear | RelicTag.Safety,
                stackLimit: 1,
                applyDelta: e => e.DisappearForgivenessPerFloor += 1),

            // #13 予備の呼吸 (Uncommon, General) — 全キャラのスキル最大使用回数+1。
            new RelicDefinition(
                ReserveBreathId,
                "予備の呼吸",
                "全キャラのスキル最大使用回数+1",
                RelicRarity.Uncommon,
                RelicTag.General,
                stackLimit: 1,
                applyDelta: e => e.SkillMaxUsageBonus += 1),

            // #14 連鎖の証 (Common, Score) — コンボ倍率のしきい値を1早める (3→2, 5→4)。
            new RelicDefinition(
                ChainProofId,
                "連鎖の証",
                "コンボ倍率のしきい値を1早める (3→2, 5→4)",
                RelicRarity.Common,
                RelicTag.Score,
                stackLimit: 1,
                applyDelta: e => e.ComboThresholdReduction += 1),

            // #15 加速の証 (Uncommon, Score, リスク型, Tier2) — 以後のBPM+8%、代わりにコンボ倍率
            // しきい値到達時の倍率+0.5。BpmMultiplierBonus は Runtime専用フック (Core は保持のみ、§2.4)。
            new RelicDefinition(
                AccelProofId,
                "加速の証",
                "以後のBPM+8%、代わりにコンボ倍率しきい値到達時の倍率+0.5",
                RelicRarity.Uncommon,
                RelicTag.Score,
                stackLimit: 1,
                applyDelta: e =>
                {
                    e.BpmMultiplierBonus += 0.08;
                    e.ComboThresholdBonusMultiplier += 0.5;
                }),

            // #16 刻の猶予 (Rare, General, Tier2) — 1ターンの締切拍数+1。
            // TurnCountdownBonus は Runtime専用フック (Core は保持のみ、§2.4)。
            // RELIC_COHERENCE_AUDIT.md §2-D: 効果は階層非依存の汎用強化であり霧/消失ルールと無関係なため、
            // 旧 LateGame (誤タグ付け。文脈連動フィルタを実装すると序盤に出なくなる誤ロックの原因だった)
            // から General へ変更。
            new RelicDefinition(
                GraceOfTimeId,
                "刻の猶予",
                "1ターンの締切拍数+1",
                RelicRarity.Rare,
                RelicTag.General,
                stackLimit: 1,
                applyDelta: e => e.TurnCountdownBonus += 1),

            // #18 蒐集家の目 (Legendary, General) — 以後3階層、レリックドラフトの候補数が3→4に増える
            // (パワー自体は増えない)。候補数の消費・減算はドラフト生成箇所 (GameController/Sim) が行う。
            new RelicDefinition(
                CollectorsEyeId,
                "蒐集家の目",
                "以後3階層、レリックドラフトの候補数が3→4に増える",
                RelicRarity.Legendary,
                RelicTag.General,
                stackLimit: 1,
                applyDelta: e => e.DraftCandidateBonusFloorsRemaining += 3),
        };

        /// <summary>Phase 5b のドラフトプール (18種完結)。</summary>
        public static System.Collections.Generic.IReadOnlyList<RelicDefinition> All => Definitions;

        /// <summary>IDからレリック定義を検索する。見つからない場合は null。</summary>
        public static RelicDefinition? Find(string id)
        {
            foreach (var def in Definitions)
            {
                if (def.Id == id) return def;
            }
            return null;
        }
    }
}
