// RelicDraftService.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.1 (ドラフトの仕組み) / §2.2 (弱点タグ重み付け) /
// §6.1 に基づく。Swift正本には存在しない (Unity固有の追加機能)。
//
// Phase 5b スコープ: §2.2 の弱点タグ付き重み付けドラフトを実装する。
// 重み = baseRarityWeight(rarity) × tagMultiplier(tags, character, selectedAI, floor)。
// 重複禁止 (1ドラフト内で同じレリックは1回だけ) / スタック可・上限あり / プール枯渇時は
// 候補数が減る (0件なら空リスト) は Phase 5a から変更なし。
// 魔法使い所持時の HardAICounter タグは重み0=候補から完全除外する (§2.2 原則5の明示ルール)。

using System.Collections.Generic;

namespace EscapeNine.Core
{
    public sealed class RelicDraftService
    {
        private readonly IRandomSource _rng;
        private readonly IReadOnlyList<RelicDefinition> _pool;

        /// <param name="rng">省略時は既定の System 乱数。</param>
        /// <param name="pool">省略時は RelicCatalog.All (18種)。テストでは差し替え可能。</param>
        public RelicDraftService(IRandomSource rng = null, IReadOnlyList<RelicDefinition> pool = null)
        {
            _rng = rng ?? new SystemRandomSource();
            _pool = pool ?? RelicCatalog.All;
        }

        /// <summary>
        /// プールから1つを本サービスの乱数源で選ぶ。深淵ルート報酬 (§4) の Rare+ 確定差し替え等、
        /// ドラフト外の抽選を Runtime が行う際に、UnityEngine.Random を直接使うとデイリーチャレンジの
        /// シード再現性が崩れるため、この経路で同じ IRandomSource に統一する。空プールは呼び出し側が弾く想定。
        /// </summary>
        public RelicDefinition PickOne(IReadOnlyList<RelicDefinition> pool)
        {
            return pool[_rng.NextInt(pool.Count)];
        }

        /// <summary>
        /// ドラフト候補を生成する (既定3択)。
        /// </summary>
        /// <param name="ownedIds">現在のランで既に所持しているレリックID (スタック分は同じIDが複数回含まれる想定)。</param>
        /// <param name="character">ドラフト対象キャラクター (弱点タグ重み付けに使用)。</param>
        /// <param name="count">提示する候補数 (既定3。#18 蒐集家の目所持中は呼び出し側が4を渡す)。</param>
        /// <param name="selectedAI">プレイヤーが選択したAI難易度 (既定Normal、HardAICounterタグの重み付けに使用)。</param>
        /// <param name="floor">
        /// レリック抽選対象の「次に入る階層」(既定1、RequiresFog/RequiresDisappearタグの
        /// ハード除外判定に Floor.GetSpecialRule(floor) 経由で使用。RELIC_COHERENCE_AUDIT.md §2-J)。
        /// 呼び出し側 (GameController.OfferRelicDraft) は「直前にクリアした階層」ではなくこちらを渡すこと。
        /// </param>
        public List<RelicDefinition> DraftCandidates(
            IReadOnlyList<string> ownedIds,
            CharacterType character,
            int count = 3,
            AILevel selectedAI = AILevel.Normal,
            int floor = 1,
            string excludeId = null)
        {
            var ownedCounts = new Dictionary<string, int>();
            if (ownedIds != null)
            {
                foreach (var id in ownedIds)
                {
                    ownedCounts[id] = ownedCounts.TryGetValue(id, out var c) ? c + 1 : 1;
                }
            }

            // スタック上限に達していないレリックのみが抽選対象。
            // さらに、重み<=0 (魔法使い所持時のHardAICounterタグ等、§2.2原則5) のものは
            // 候補プールから完全除外する (「非常に出にくい」ではなく「出ない」を保証するため)。
            var weighted = new List<(RelicDefinition def, double weight)>();
            foreach (var def in _pool)
            {
                if (excludeId != null && def.Id == excludeId) continue; // 呼び出し側指定の個別除外 (例: 最後のドラフトで #18)
                int owned = ownedCounts.TryGetValue(def.Id, out var c) ? c : 0;
                if (owned >= def.StackLimit) continue;

                double weight = ComputeWeight(def, character, selectedAI, floor);
                if (weight > 0) weighted.Add((def, weight));
            }

            var result = new List<RelicDefinition>();
            int draws = System.Math.Min(count, weighted.Count);
            for (int i = 0; i < draws; i++)
            {
                double total = 0;
                foreach (var item in weighted) total += item.weight;

                double roll = _rng.NextDouble() * total;
                double cumulative = 0;
                int pickIdx = weighted.Count - 1; // 浮動小数の丸め誤差でcumulativeが僅かに届かない場合の保険
                for (int j = 0; j < weighted.Count; j++)
                {
                    cumulative += weighted[j].weight;
                    if (roll < cumulative) { pickIdx = j; break; }
                }

                result.Add(weighted[pickIdx].def);
                weighted.RemoveAt(pickIdx); // 同一ドラフト内での重複表示を禁止
            }
            return result;
        }

        /// <summary>
        /// ドラフト候補の重み。§2.2 の重み付け表と1:1対応。
        /// 戻り値が0以下の場合、呼び出し側 (DraftCandidates) が候補から除外する。
        ///
        /// RELIC_COHERENCE_AUDIT.md §3/§4 是正 (ハード除外の追加):
        /// 「仕組みが発動していない/無意味な階層・キャラでは候補から完全除外 (0.0)」という
        /// 既存の魔法使い×HardAICounter除外 (§2.2原則5) と同じ作法を、以下にも適用する。
        /// - RequiresFog/RequiresDisappear: 対象の特殊ルールが発動していない floor では0
        /// - ThiefRescue: 盗賊以外は0 (旧×0.15は撤廃。効果がSkillType.Diagonal限定のため常に無価値)
        /// - #17 心話の絆: エルフは0 (GameSession.BindEnemy() が Bind分岐で必ずreturnし
        ///   PseudoBindCharges 分岐に絶対に落ちないため、エルフには常に無価値)
        /// </summary>
        private static double ComputeWeight(RelicDefinition def, CharacterType character, AILevel selectedAI, int floor)
        {
            double weight = BaseRarityWeight(def.Rarity);
            RelicTag tags = def.Tags;

            if ((tags & RelicTag.ThiefRescue) != 0)
            {
                if (character != CharacterType.Thief) return 0.0;
                weight *= 3.0;
            }

            if ((tags & RelicTag.HardAICounter) != 0)
            {
                if (character == CharacterType.Wizard)
                {
                    // §2.2 原則5: 魔法使いはHardAICounter系レリックをドラフト対象外にする
                    // (Hard vs 魔法使い創発の悪化を防ぐ明示ルール)。他のタグ倍率を計算するまでもなく0。
                    return 0.0;
                }
                if (selectedAI == AILevel.Hard)
                {
                    weight *= 2.5;
                }
            }

            if (def.Id == RelicCatalog.HeartboundPactId && character == CharacterType.Elf)
            {
                // #17 心話の絆はエルフに常に無価値 (RELIC_COHERENCE_AUDIT.md §2-F)。
                // タグ (HardAICounter|General) 単位では表現できない個別ルールのため Id で分岐する。
                return 0.0;
            }

            if (def.Id == RelicCatalog.BewilderingDustId && selectedAI == AILevel.Easy)
            {
                // #9 幻惑の粉は「実効AI==Hard の時に予測追跡をNormal格下げ」する効果。Easy選択では
                // GetEffectiveAILevel が自然Hardを Normal へ下げるため実効Hardが発生せず (深淵ルート以外)、
                // 常に発動しない。thief/elf/wizard と同じ「発動しない文脈では出さない」作法でハード除外する。
                return 0.0;
            }

            if ((tags & RelicTag.RequiresFog) != 0)
            {
                SpecialRule rule = Floor.GetSpecialRule(floor);
                if (rule != SpecialRule.Fog && rule != SpecialRule.FogDisappear) return 0.0;
            }

            if ((tags & RelicTag.RequiresDisappear) != 0)
            {
                SpecialRule rule = Floor.GetSpecialRule(floor);
                if (rule != SpecialRule.Disappear && rule != SpecialRule.FogDisappear) return 0.0;
            }

            if ((tags & RelicTag.General) != 0 && character == CharacterType.Wizard)
            {
                weight *= 0.5;
            }

            if ((tags & RelicTag.Safety) != 0
                && (character == CharacterType.Hero || character == CharacterType.Elf)
                && (selectedAI == AILevel.Hard || selectedAI == AILevel.Boss))
            {
                weight *= 1.3;
            }

            return weight;
        }

        /// <summary>
        /// 指定の文脈でこのレリックがドラフト提示に足るか (ComputeWeight &gt; 0)。
        /// 深淵ルートの Rare+ 確定枠 (GameController.EnsureRarePlusSlot) が、通常ドラフトと同じ
        /// ハード除外 (ThiefRescue×非盗賊 / #17×エルフ / #9×Easy / RequiresFog/Disappear の階層除外 /
        /// 魔法使い×HardAICounter) を尊重するために使う。これが無いと確定枠が「絶対に出ないはずの
        /// 死にレリック」を注入できてしまう (RELIC_COHERENCE_AUDIT 同型)。
        /// </summary>
        public static bool IsEligible(RelicDefinition def, CharacterType character, AILevel selectedAI, int floor)
            => ComputeWeight(def, character, selectedAI, floor) > 0.0;

        /// <summary>基準レアリティ出現率 (§2.2)。Common45 / Uncommon30 / Rare18 / Epic6 / Legendary1。</summary>
        private static double BaseRarityWeight(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 45.0;
                case RelicRarity.Uncommon: return 30.0;
                case RelicRarity.Rare: return 18.0;
                case RelicRarity.Epic: return 6.0;
                case RelicRarity.Legendary: return 1.0;
                default: return 1.0;
            }
        }
    }
}
