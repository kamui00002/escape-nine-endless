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

        // これらのプール機構テスト (重複禁止/所持済み除外/スタック上限) は、RELIC_COHERENCE_AUDIT.md 是正
        // (ThiefRescue の非盗賊0除外、RequiresFog/RequiresDisappear の階層ゲート) の影響を受けないよう、
        // 意図的に character=Thief かつ floor=61 (霧+マス消失が両方発動=全タグのハード除外条件を満たす)
        // を使う。これにより18種全てが weight>0 になり、元の「フルカタログ前提」の個数アサーションを
        // そのまま維持できる (盗賊自身はThiefRescueで除外されず、floor61+はRequiresFog/Disappear双方満たす)。
        private const int FullPoolFloor = 61;
        private const CharacterType FullPoolCharacter = CharacterType.Thief;

        [Test]
        public void DraftCandidates_NoDuplicatesWithinSingleDraft()
        {
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), FullPoolCharacter, count: CatalogSize, floor: FullPoolFloor);

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
            var result = service.DraftCandidates(owned, FullPoolCharacter, count: CatalogSize, floor: FullPoolFloor);

            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.VeteranStanceId), "スタック不可レリックは1回所持したら候補から除外される");
            Assert.AreEqual(CatalogSize - 1, result.Count, "残り17種は引き続き候補になる");
        }

        [Test]
        public void DraftCandidates_LanternRing_ExcludedOnceOwned()
        {
            // #8 灯火の指輪 は stackLimit=1 に変更 (3x3盤で2枚目以降が無効な死にスタックを避ける修正、Fable指摘)。
            // 全レリックが stackLimit=1 になったため、1枚所持で候補から除外される。
            var owned1 = new List<string> { RelicCatalog.LanternRingId };
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(owned1, FullPoolCharacter, count: CatalogSize, floor: FullPoolFloor);
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.LanternRingId), "1個所持 (上限1到達) で候補から除外される");
            Assert.AreEqual(CatalogSize - 1, result.Count);
        }

        // 深淵ルートの Rare+ 確定枠 (GameController.EnsureRarePlusSlot) が通常ドラフトと同じハード除外を
        // 尊重できるよう公開した IsEligible の回帰ガード。これが漏れると「絶対に出ないはずの死にレリック」を
        // 確定枠が注入できてしまう (Fable指摘のバイパス)。
        [Test]
        public void IsEligible_HardExclusions()
        {
            var thiefRescue = RelicCatalog.Find(RelicCatalog.ShadowFootworkId).Value; // #1 ThiefRescue (盗賊専用)
            Assert.IsFalse(RelicDraftService.IsEligible(thiefRescue, CharacterType.Hero, AILevel.Normal, FullPoolFloor), "ThiefRescue は非盗賊で不適格");
            Assert.IsTrue(RelicDraftService.IsEligible(thiefRescue, CharacterType.Thief, AILevel.Normal, FullPoolFloor), "盗賊なら適格");

            var dust = RelicCatalog.Find(RelicCatalog.BewilderingDustId).Value; // #9 幻惑の粉 (Easyで不発)
            Assert.IsFalse(RelicDraftService.IsEligible(dust, CharacterType.Hero, AILevel.Easy, FullPoolFloor), "#9 は Easy選択で不適格");
            Assert.IsTrue(RelicDraftService.IsEligible(dust, CharacterType.Hero, AILevel.Normal, FullPoolFloor), "Normal なら適格");
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
            // floor=61 (霧+マス消失が両方発動) で RequiresFog/RequiresDisappear のゲートを外し、
            // 魔法使い固有の除外だけを見る。魔法使いは非盗賊でもあるため、HardAICounter 3種
            // (#2, #9, #17) に加えて ThiefRescue 系 (#1, #3。#2はHardAICounterと重複) も除外され、
            // 計5種を除いた13種のみが候補になる (RELIC_COHERENCE_AUDIT.md §2-E 是正の副次効果)。
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Wizard, count: CatalogSize, floor: FullPoolFloor);

            Assert.AreEqual(CatalogSize - 5, result.Count, "ThiefRescue2種+HardAICounter3種を除いた13種のみが候補になる");
            foreach (var def in result)
            {
                Assert.AreEqual(RelicTag.None, def.Tags & RelicTag.HardAICounter,
                    $"{def.Id} は HardAICounter タグ付きなのに魔法使いの候補に現れた");
            }
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.ShadowFootworkId), "#1 影の軽業は非盗賊除外 (魔法使いも対象)");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.AfterimageVeilId), "#2 残像のヴェールは魔法使い除外");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.ShadowCloneArtId), "#3 影分身の型は非盗賊除外 (魔法使いも対象)");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.BewilderingDustId), "#9 幻惑の粉は魔法使い除外 (§2.2 明示ルール)");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.HeartboundPactId), "#17 心話の絆は魔法使い除外");
        }

        [Test]
        public void DraftCandidates_NonWizard_CanSeeHardAICounterRelics()
        {
            // 除外は魔法使い限定であることの対照検証。勇者は非盗賊のため ThiefRescue 系3種
            // (#1/#2/#3) は除外されるが、HardAICounter 系 (#9等) は候補に入る。
            var service = new RelicDraftService(new FakeRandom());
            var result = service.DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: FullPoolFloor);

            Assert.AreEqual(CatalogSize - 3, result.Count, "ThiefRescue系3種を除いた15種が候補になる");
            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.BewilderingDustId), "#9 幻惑の粉は魔法使い以外なら候補に入る");
        }

        /// <summary>
        /// §2.2 の重み表をテスト側で独立に再計算するミラー実装 (実装ロジック検証用の仕様の写し)。
        /// 実装 (RelicDraftService.ComputeWeight) と定数がズレたら分布テストが落ちる。
        ///
        /// RELIC_COHERENCE_AUDIT.md §3/§4 是正の反映: 旧 LateGame の floor 倍率ブーストは廃止し、
        /// RequiresFog/RequiresDisappear は「対象ルールが発動していない floor では0」のハード除外に、
        /// ThiefRescue は「非盗賊は0」(旧×0.15を撤廃) に、#17心話の絆は「エルフは0」に変更した。
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
            if ((def.Tags & RelicTag.ThiefRescue) != 0)
            {
                if (character != CharacterType.Thief) return 0;
                w *= 3.0;
            }
            if ((def.Tags & RelicTag.HardAICounter) != 0)
            {
                if (character == CharacterType.Wizard) return 0;
                if (ai == AILevel.Hard) w *= 2.5;
            }
            if (def.Id == RelicCatalog.HeartboundPactId && character == CharacterType.Elf) return 0;
            if ((def.Tags & RelicTag.RequiresFog) != 0)
            {
                SpecialRule rule = Floor.GetSpecialRule(floor);
                if (rule != SpecialRule.Fog && rule != SpecialRule.FogDisappear) return 0;
            }
            if ((def.Tags & RelicTag.RequiresDisappear) != 0)
            {
                SpecialRule rule = Floor.GetSpecialRule(floor);
                if (rule != SpecialRule.Disappear && rule != SpecialRule.FogDisappear) return 0;
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
            // RELIC_COHERENCE_AUDIT.md §2-E 是正: ThiefRescue は盗賊 ×3.0 / 他キャラは完全除外 (0)。
            // 旧×0.15 (常に0.15の確率で非盗賊にも提示) は撤廃した (効果がSkillType.Diagonal専用のため
            // 非盗賊には常に無価値、§2-E)。
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
            Assert.AreEqual(0, heroFreq, "勇者は ThiefRescue 系が完全除外され0%のはず (非盗賊への0.15倍出現は撤廃)");
        }

        [Test]
        public void DraftCandidates_ThiefRescueRelics_NeverAppearForNonThief()
        {
            // RELIC_COHERENCE_AUDIT.md §2-E: 盗賊専用 (#1 影の軽業 / #2 残像のヴェール / #3 影分身の型) は
            // 非盗賊キャラのドラフト候補から完全除外される (旧実装は×0.15で提示していた)。
            var result = new RelicDraftService(new FakeRandom())
                .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize);

            Assert.IsFalse(result.Exists(d => (d.Tags & RelicTag.ThiefRescue) != 0),
                "勇者にはThiefRescueタグ付きレリックが一切候補に出ないはず");
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.ShadowFootworkId));
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.AfterimageVeilId));
            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.ShadowCloneArtId));
        }

        [Test]
        public void DraftCandidates_ThiefRescueRelics_AvailableForThief()
        {
            var result = new RelicDraftService(new FakeRandom())
                .DraftCandidates(new List<string>(), CharacterType.Thief, count: CatalogSize);

            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.ShadowFootworkId));
            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.AfterimageVeilId));
            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.ShadowCloneArtId));
        }

        [Test]
        public void DraftCandidates_HeartboundPact_ExcludedForElf()
        {
            // RELIC_COHERENCE_AUDIT.md §2-F: エルフは既に拘束スキル(Bind)持ちだが
            // GameSession.BindEnemy() は Bind分岐で必ずreturnし PseudoBindCharges 分岐に
            // 絶対に落ちないため、#17心話の絆はエルフには常に無価値。ドラフト候補から完全除外する。
            var result = new RelicDraftService(new FakeRandom())
                .DraftCandidates(new List<string>(), CharacterType.Elf, count: CatalogSize);

            Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.HeartboundPactId),
                "エルフには心話の絆が一切候補に出ないはず");
        }

        [Test]
        public void DraftCandidates_HeartboundPact_AvailableForNonElfNonWizard()
        {
            var result = new RelicDraftService(new FakeRandom())
                .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize);

            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.HeartboundPactId));
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

        // MARK: - RELIC_COHERENCE_AUDIT.md §3/§4: RequiresFog/RequiresDisappear のハード除外
        // (旧 LateGame の floor 倍率ブーストを廃止し、仕組みが発動していない floor では0除外にした)

        [Test]
        public void DraftCandidates_RequiresDisappearRelics_ExcludedWhenDisappearRuleInactive()
        {
            // #7 地固めの護符 / #11 影の抜け道: マス消失ルール (Disappear/FogDisappear) が
            // 発動していない階層 (1-40) では候補から完全除外される (RELIC_COHERENCE_AUDIT.md §2-A/B)。
            foreach (int floor in new[] { 1, 20, 21, 40 }) // None(1-20) / Fog(21-40) = いずれも消失なし
            {
                var result = new RelicDraftService(new FakeRandom())
                    .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: floor);
                Assert.IsFalse(result.Exists(d => (d.Tags & RelicTag.RequiresDisappear) != 0),
                    $"階層{floor}(マス消失なし)ではRequiresDisappearタグは一切候補に出ないはず");
            }
        }

        [Test]
        public void DraftCandidates_RequiresDisappearRelics_AvailableWhenDisappearRuleActive()
        {
            foreach (int floor in new[] { 41, 60, 61, 100 }) // Disappear(41-60) / FogDisappear(61-100)
            {
                var result = new RelicDraftService(new FakeRandom())
                    .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: floor);
                Assert.IsTrue(result.Exists(d => (d.Tags & RelicTag.RequiresDisappear) != 0),
                    $"階層{floor}(マス消失あり)ではRequiresDisappearタグが候補になり得るはず");
            }
        }

        [Test]
        public void DraftCandidates_RequiresFogRelics_ExcludedWhenFogRuleInactive()
        {
            // #8 灯火の指輪: 霧ルール (Fog/FogDisappear) が発動していない階層では候補から完全除外される。
            // 階層41-60 (マス消失のみ・霧なし) を含むのが重要 (RELIC_COHERENCE_AUDIT.md §2-C の
            // 「霧無効期間に重みが上がる」二重の矛盾が再発していないことの確認)。
            foreach (int floor in new[] { 1, 20, 41, 60 }) // None(1-20) / Disappear(41-60) = いずれも霧なし
            {
                var result = new RelicDraftService(new FakeRandom())
                    .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: floor);
                Assert.IsFalse(result.Exists(d => d.Id == RelicCatalog.LanternRingId),
                    $"階層{floor}(霧なし)では灯火の指輪は候補に出ないはず");
            }
        }

        [Test]
        public void DraftCandidates_RequiresFogRelics_AvailableWhenFogRuleActive()
        {
            foreach (int floor in new[] { 21, 40, 61, 100 }) // Fog(21-40) / FogDisappear(61-100)
            {
                var result = new RelicDraftService(new FakeRandom())
                    .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: floor);
                Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.LanternRingId),
                    $"階層{floor}(霧あり)では灯火の指輪が候補になり得るはず");
            }
        }

        [Test]
        public void DraftCandidates_GraceOfTime_AvailableFromFloorOne()
        {
            // RELIC_COHERENCE_AUDIT.md §2-D: #16刻の猶予は効果が階層非依存の汎用強化のため、
            // 旧LateGameタグの誤ロック (文脈連動フィルタ導入で序盤に出なくなる問題) を
            // Generalタグへの是正で解消したことを確認する (誤ロックしていない)。
            var result = new RelicDraftService(new FakeRandom())
                .DraftCandidates(new List<string>(), CharacterType.Hero, count: CatalogSize, floor: 1);

            Assert.IsTrue(result.Exists(d => d.Id == RelicCatalog.GraceOfTimeId),
                "刻の猶予は階層1から候補に出るはず");
        }
    }
}
