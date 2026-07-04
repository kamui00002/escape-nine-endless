// RelicDraftServiceTests.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.1 (ドラフトの仕組み) / §2.2 (弱点タグ重み付け) /
// §6.4 に基づくテスト。
// 固定乱数列での決定性、重複禁止、スタック可・上限あり、プール枯渇時のフォールバック、
// N回抽選での重み分布、魔法使い×HardAICounter の除外ルール、盗賊のThiefRescue優遇を検証する。
//
// Phase 5b 改訂: 5a の均等抽選 (NextInt) → §2.2 の重み付き抽選 (NextDouble) へ移行したため、
// 5a 時点の「NextInt キューによる手計算期待値」テストは重み付き前提に書き直した (設計 §7 Phase 5b)。
// プールも 8種 → 18種 に拡充済みのため件数期待値も更新している。
//
// FakeRandom は AIEngineTests.cs で定義されたもの (internal, 同一アセンブリ内で再利用可能)。

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class RelicDraftServiceTests
    {
        private const int CatalogSize = 18; // §2.3 の18種

        [Test]
        public void DraftCandidates_FixedRandomSequence_IsDeterministic()
        {
            var service1 = new RelicDraftService(new FakeRandom(doubles: new[] { 0.1, 0.5, 0.9 }));
            var result1 = service1.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            var service2 = new RelicDraftService(new FakeRandom(doubles: new[] { 0.1, 0.5, 0.9 }));
            var result2 = service2.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            Assert.AreEqual(3, result1.Count);
            for (int i = 0; i < result1.Count; i++)
            {
                Assert.AreEqual(result1[i].Id, result2[i].Id, "同じ乱数列なら同じ候補が同じ順で出るはず");
            }
        }

        [Test]
        public void DraftCandidates_WeightedRoll_PicksByCumulativeWeight_HandComputed()
        {
            // タグなしの小プールで §2.2 の基準レアリティ重みのみを検証する手計算ケース。
            // a=Common(45), b=Uncommon(30), c=Rare(18)。合計93。
            var pool = new List<RelicDefinition>
            {
                new RelicDefinition("a", "A", "", RelicRarity.Common, RelicTag.None, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("b", "B", "", RelicRarity.Uncommon, RelicTag.None, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("c", "C", "", RelicRarity.Rare, RelicTag.None, stackLimit: 1, applyDelta: e => { }),
            };

            // draw1: roll = 0.0 * 93 = 0 < 45 (aの累積) → a
            // draw2: 残り {b:30, c:18} 合計48。roll = 0.99 * 48 = 47.52。b累積30 <= 47.52 < 48 → c
            var service = new RelicDraftService(new FakeRandom(doubles: new[] { 0.0, 0.99 }), pool);
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: 2);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("a", result[0].Id, "roll=0 は累積重み先頭の Common を引く");
            Assert.AreEqual("c", result[1].Id, "roll=0.99 は累積重み末尾の Rare を引く");
        }

        [Test]
        public void DraftCandidates_NoDuplicatesWithinSingleDraft()
        {
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize);

            var seen = new HashSet<string>();
            foreach (var def in result)
            {
                Assert.IsTrue(seen.Add(def.Id), $"{def.Id} が同一ドラフト内で重複した");
            }
            Assert.AreEqual(CatalogSize, result.Count);
        }

        [Test]
        public void DraftCandidates_ExcludesNonStackableRelicOnceOwned()
        {
            var owned = new List<string> { RelicCatalog.VeteranStanceId }; // stackLimit=1
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: CatalogSize);

            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.VeteranStanceId), "スタック不可レリックは1回所持したら候補から除外される");
            Assert.AreEqual(CatalogSize - 1, result.Count, "残り17種は引き続き候補になる");
        }

        [Test]
        public void DraftCandidates_StackableRelic_EligibleBelowCap_ExcludedAtCap()
        {
            // #8 灯火の指輪 (lantern_ring) は stackLimit=3
            var owned2 = new List<string> { RelicCatalog.LanternRingId, RelicCatalog.LanternRingId };
            var serviceBelowCap = new RelicDraftService(new FakeRandom());
            var resultBelowCap = serviceBelowCap.DraftCandidates(owned2, CharacterType.Hero, count: CatalogSize);
            Assert.IsTrue(resultBelowCap.Exists(d => d.Id == RelicCatalog.LanternRingId), "2個所持 (上限3未満) ではまだ候補に入る");

            var owned3 = new List<string> { RelicCatalog.LanternRingId, RelicCatalog.LanternRingId, RelicCatalog.LanternRingId };
            var serviceAtCap = new RelicDraftService(new FakeRandom());
            var resultAtCap = serviceAtCap.DraftCandidates(owned3, CharacterType.Hero, count: CatalogSize);
            Assert.IsFalse(resultAtCap.Exists(d => d.Id == RelicCatalog.LanternRingId), "3個所持 (上限到達) では候補から除外される");
            Assert.AreEqual(CatalogSize - 1, resultAtCap.Count);
        }

        [Test]
        public void DraftCandidates_PoolExhaustion_ReturnsFewerThanRequested()
        {
            var pool = new List<RelicDefinition>
            {
                new RelicDefinition("a", "A", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("b", "B", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
            };
            var service = new RelicDraftService(new FakeRandom(), pool);

            // どちらも既に上限まで所持 -> 候補は0件
            var owned = new List<string> { "a", "b" };
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: 3);
            Assert.AreEqual(0, result.Count, "プールが完全に枯渇した場合は空リストを返す");
        }

        [Test]
        public void DraftCandidates_PoolPartialExhaustion_ReturnsOnlyRemainingEligible()
        {
            var pool = new List<RelicDefinition>
            {
                new RelicDefinition("a", "A", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
                new RelicDefinition("b", "B", "", RelicRarity.Common, RelicTag.General, stackLimit: 1, applyDelta: e => { }),
            };
            var service = new RelicDraftService(new FakeRandom(), pool);

            var owned = new List<string> { "a" }; // aは上限到達、bのみ残る
            var result = service.DraftCandidates(owned, CharacterType.Hero, count: 3);

            Assert.AreEqual(1, result.Count, "候補数が3未満でも要求数まで待たず、残っている分だけ返す");
            Assert.AreEqual("b", result[0].Id);
        }

        [Test]
        public void DraftCandidates_DefaultsToRelicCatalogPool()
        {
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: 3);

            Assert.AreEqual(3, result.Count);
            foreach (var def in result)
            {
                Assert.IsNotNull(RelicCatalog.Find(def.Id), "既定プールは RelicCatalog.All (18種) であるはず");
            }
        }

        // MARK: - §2.2 弱点タグ重み付け (Phase 5b)

        [Test]
        public void DraftCandidates_WizardNeverSeesHardAICounterRelics()
        {
            // §2.2 原則5 の明示ルール: character == 魔法使い × HardAICounter は重み0 = 候補から完全除外。
            // 全件 (count=18) 要求しても HardAICounter タグ付き3種 (#2, #9, #17) は決して現れない。
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Wizard, count: CatalogSize);

            Assert.AreEqual(CatalogSize - 3, result.Count, "HardAICounter 3種を除いた15種のみが候補になる");
            foreach (var def in result)
            {
                Assert.AreEqual(RelicTag.None, def.Tags & RelicTag.HardAICounter,
                    $"{def.Id} は HardAICounter タグ付きなのに魔法使いの候補に現れた");
            }
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.AfterimageVeilId), "#2 残像のヴェールは魔法使い除外");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.BewilderingDustId), "#9 幻惑の粉は魔法使い除外 (§2.2 明示ルール)");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.HeartboundPactId), "#17 心話の絆は魔法使い除外");
        }

        [Test]
        public void DraftCandidates_NonWizard_CanSeeHardAICounterRelics()
        {
            // 除外は魔法使い限定であることの対照検証 (勇者は全18種が候補になる)。
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize);

            Assert.AreEqual(CatalogSize, result.Count);
            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.BewilderingDustId), "#9 幻惑の粉は魔法使い以外なら候補に入る");
        }

        /// <summary>
        /// §2.2 の重み表をテスト側で独立に再計算するミラー実装 (実装ロジック検証用の仕様の写し)。
        /// 実装 (RelicDraftService.ComputeWeight) と定数がズレたら分布テストが落ちる。
        /// </summary>
        private static double SpecWeight(RelicDefinition def, CharacterType character, AILevel ai, int floor)
        {
            double w;
            switch (def.Rarity)
            {
                case RelicRarity.Common: w = 45; break;
                case RelicRarity.Uncommon: w = 30; break;
                case RelicRarity.Rare: w = 18; break;
                case RelicRarity.Epic: w = 6; break;
                default: w = 1; break; // Legendary
            }
            if ((def.Tags & RelicTag.ThiefRescue) != 0) w *= character == CharacterType.Thief ? 3.0 : 0.15;
            if ((def.Tags & RelicTag.HardAICounter) != 0)
            {
                if (character == CharacterType.Wizard) return 0;
                if (ai == AILevel.Hard) w *= 2.5;
            }
            if ((def.Tags & RelicTag.LateGame) != 0)
            {
                if (floor >= 61) w *= 2.5;
                else if (floor >= 41) w *= 2.0;
            }
            if ((def.Tags & RelicTag.General) != 0 && character == CharacterType.Wizard) w *= 0.5;
            if ((def.Tags & RelicTag.Safety) != 0
                && (character == CharacterType.Hero || character == CharacterType.Elf)
                && (ai == AILevel.Hard || ai == AILevel.Boss)) w *= 1.3;
            return w;
        }

        /// <summary>count=1 のドラフトを trials 回引いて ID 別の出現回数を数える。</summary>
        private static Dictionary<string, int> SampleSingleDraws(CharacterType character, AILevel ai, int floor, int trials, int seed)
        {
            var service = new RelicDraftService(new SystemRandomSource(seed));
            var counts = new Dictionary<string, int>();
            var noOwned = new List<string>();
            for (int i = 0; i < trials; i++)
            {
                var picked = service.DraftCandidates(noOwned, character, count: 1, selectedAI: ai, floor: floor);
                Assert.AreEqual(1, picked.Count);
                counts[picked[0].Id] = counts.TryGetValue(picked[0].Id, out var c) ? c + 1 : 1;
            }
            return counts;
        }

        [Test]
        public void DraftCandidates_WeightDistribution_MatchesSpecWithinTolerance()
        {
            // 固定シード N=20000 の1枚引きで、全18種の出現頻度が §2.2 の期待確率の ±4σ 以内に収まること。
            // (二項分布の標準偏差 σ = sqrt(N p (1-p))。4σ 超過が1種でもあれば重み実装のバグを疑う)
            const int trials = 20000;
            const CharacterType character = CharacterType.Hero;
            const AILevel ai = AILevel.Normal;
            const int floor = 1;

            double total = RelicCatalog.All.Sum(d => SpecWeight(d, character, ai, floor));
            var counts = SampleSingleDraws(character, ai, floor, trials, seed: 20260704);

            foreach (var def in RelicCatalog.All)
            {
                double p = SpecWeight(def, character, ai, floor) / total;
                double expected = trials * p;
                double sigma = System.Math.Sqrt(trials * p * (1 - p));
                int observed = counts.TryGetValue(def.Id, out var c) ? c : 0;

                Assert.LessOrEqual(System.Math.Abs(observed - expected), 4 * sigma + 1,
                    $"{def.Id}: observed={observed}, expected={expected:F1} (±4σ={4 * sigma:F1}) — §2.2 の重みから乖離");
            }
        }

        [Test]
        public void DraftCandidates_ThiefSeesThiefRescueRelicsFarMoreOften()
        {
            // §2.2: ThiefRescue は盗賊 ×3.0 / 他キャラ ×0.15。
            // 期待確率 (仕様から計算): 盗賊 ≈ 207/565 ≈ 36.6% / 勇者 ≈ 10.35/368.35 ≈ 2.8%
            const int trials = 10000;
            var thiefCounts = SampleSingleDraws(CharacterType.Thief, AILevel.Normal, 1, trials, seed: 1234);
            var heroCounts = SampleSingleDraws(CharacterType.Hero, AILevel.Normal, 1, trials, seed: 1234);

            int ThiefRescueHits(Dictionary<string, int> counts) =>
                RelicCatalog.All
                    .Where(d => (d.Tags & RelicTag.ThiefRescue) != 0)
                    .Sum(d => counts.TryGetValue(d.Id, out var c) ? c : 0);

            double thiefFreq = (double)ThiefRescueHits(thiefCounts) / trials;
            double heroFreq = (double)ThiefRescueHits(heroCounts) / trials;

            Assert.Greater(thiefFreq, 0.30, "盗賊は ThiefRescue 系を約36.6%で引くはず (優遇 ×3.0)");
            Assert.Less(heroFreq, 0.06, "勇者は ThiefRescue 系を約2.8%でしか引かないはず (抑制 ×0.15)");
        }

        [Test]
        public void DraftCandidates_HardAISelection_BoostsHardAICounterRelics()
        {
            // §2.2: selectedAI == Hard かつ魔法使い以外 → HardAICounter ×2.5
            const int trials = 10000;
            var hardCounts = SampleSingleDraws(CharacterType.Hero, AILevel.Hard, 1, trials, seed: 777);
            var normalCounts = SampleSingleDraws(CharacterType.Hero, AILevel.Normal, 1, trials, seed: 777);

            int HardCounterHits(Dictionary<string, int> counts) =>
                RelicCatalog.All
                    .Where(d => (d.Tags & RelicTag.HardAICounter) != 0)
                    .Sum(d => counts.TryGetValue(d.Id, out var c) ? c : 0);

            double hardFreq = (double)HardCounterHits(hardCounts) / trials;
            double normalFreq = (double)HardCounterHits(normalCounts) / trials;

            Assert.Greater(hardFreq, normalFreq * 1.5,
                $"Hard選択時はHardAICounter系の出現頻度が明確に上がるはず (hard={hardFreq:F3}, normal={normalFreq:F3})");
        }

        [Test]
        public void DraftCandidates_LateFloors_BoostLateGameRelics()
        {
            // §2.2: floor >= 41 → LateGame ×2.0 / floor >= 61 → ×2.5
            const int trials = 10000;
            var earlyCounts = SampleSingleDraws(CharacterType.Hero, AILevel.Normal, 1, trials, seed: 555);
            var lateCounts = SampleSingleDraws(CharacterType.Hero, AILevel.Normal, 61, trials, seed: 555);

            int LateGameHits(Dictionary<string, int> counts) =>
                RelicCatalog.All
                    .Where(d => (d.Tags & RelicTag.LateGame) != 0)
                    .Sum(d => counts.TryGetValue(d.Id, out var c) ? c : 0);

            double earlyFreq = (double)LateGameHits(earlyCounts) / trials;
            double lateFreq = (double)LateGameHits(lateCounts) / trials;

            Assert.Greater(lateFreq, earlyFreq * 1.5,
                $"階層61+ではLateGame系の出現頻度が明確に上がるはず (late={lateFreq:F3}, early={earlyFreq:F3})");
        }
    }
}
