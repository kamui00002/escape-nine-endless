// RelicCatalog.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.3 (レリック一覧) / §7 Phase 5a に基づく。
// Swift正本には存在しない (Unity固有の追加機能)。
//
// Phase 5a スコープ: 18種のうち、各弱点タグを最低1つ含む代表的サブセット8種のみ実装する
// (#1, #2, #4, #7, #8, #10, #12, #17)。残り10種と弱点タグ重み付けドラフトはPhase 5bで追加。
// 命名は設計書の通りプレースホルダー (最終コピーは実装時に別途詰める前提、§2.3 末尾の注記)。

namespace EscapeNine.Core
{
    public static class RelicCatalog
    {
        // ID定数 (Runtime/永続化からの参照用)
        public const string ShadowFootworkId = "shadow_footwork";     // #1 影の軽業
        public const string AfterimageVeilId = "afterimage_veil";     // #2 残像のヴェール
        public const string VeteranStanceId = "veteran_stance";       // #4 老練の構え
        public const string GroundingCharmId = "grounding_charm";     // #7 地固めの護符
        public const string LanternRingId = "lantern_ring";           // #8 灯火の指輪
        public const string SafeStartId = "safe_start";               // #10 護りの起点
        public const string ImprovisedShieldId = "improvised_shield"; // #12 二段構えの盾
        public const string HeartboundPactId = "heartbound_pact";     // #17 心話の絆

        private static readonly RelicDefinition[] Definitions5a =
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

            // #7 地固めの護符 (Uncommon, LateGame) — マス消失の発生数 -1 (最低0、階層41+で有効)。
            new RelicDefinition(
                GroundingCharmId,
                "地固めの護符",
                "マス消失の発生数-1 (最低0)",
                RelicRarity.Uncommon,
                RelicTag.LateGame,
                stackLimit: 1,
                applyDelta: e => e.DisappearCellReduction += 1),

            // #8 灯火の指輪 (Common, スタック可・上限3, LateGame) — 霧の視界半径+1マス (階層21+で有効)。
            new RelicDefinition(
                LanternRingId,
                "灯火の指輪",
                "霧の視界半径+1マス",
                RelicRarity.Common,
                RelicTag.LateGame,
                stackLimit: 3,
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
        };

        /// <summary>Phase 5a のドラフトプール (8種)。</summary>
        public static System.Collections.Generic.IReadOnlyList<RelicDefinition> All => Definitions5a;

        /// <summary>IDからレリック定義を検索する。見つからない場合は null。</summary>
        public static RelicDefinition? Find(string id)
        {
            foreach (var def in Definitions5a)
            {
                if (def.Id == id) return def;
            }
            return null;
        }
    }
}
