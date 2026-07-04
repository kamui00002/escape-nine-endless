// RelicConfigTests.cs
// Unity Phase 5c「ドラフト頻度/取得上限レバー」導入 (unity/verify/BALANCE_REPORT_PHASE5.md §6.5 案A/B) の
// ゲーティングロジック (RelicConfig.ShouldOfferDraft) のテスト。
//
// GameController.cs (Runtime) と unity/verify/Sim/Program.cs の両方が本メソッドを直接呼び出しており、
// ここで判定ロジックそのものの正しさを担保しておけば、両呼び出し元の動作が一致することも保証される。
//
// 重要: 判定式は「所持数が上限に達している → false」→「初回クリア(<=1) → true」→
// 「clearedFloor が draftInterval の倍数か」の順で評価される (RelicConfig.cs 参照)。
// 本番既定値 (RelicConfig.DraftInterval/MaxRelicsPerRun) が将来チューニングし直されてもこのテストが
// 壊れないよう、境界値を確認するテストは明示的な interval/cap 引数を使う。既定値そのものを検証する
// テストは1本だけ用意し、明示的に「既定値 = RelicConfig の定数」であることを確認する。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class RelicConfigTests
    {
        // MARK: - 初回クリア (clearedFloor <= 1) は上限未達なら常に true

        [Test]
        public void ShouldOfferDraft_FirstClear_AlwaysTrue_EvenWhenIntervalDoesNotDivideOne()
        {
            // interval=2 の場合、floor=1 は 1%2!=0 で本来ならfalseになるはずだが、
            // 「序盤の体験を守る」特例でtrueになる。
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(clearedFloor: 1, ownedRelicCount: 0, draftInterval: 2, maxRelicsPerRun: 999));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(clearedFloor: 1, ownedRelicCount: 0, draftInterval: 3, maxRelicsPerRun: 999));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(clearedFloor: 1, ownedRelicCount: 0, draftInterval: 5, maxRelicsPerRun: 999));
        }

        [Test]
        public void ShouldOfferDraft_FirstClear_BlockedIfCapAlreadyReached()
        {
            // 上限判定は初回クリア特例より優先される (#18 蒐集家の目等で理論上あり得る所持数超過ケースの保険)。
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(clearedFloor: 1, ownedRelicCount: 6, draftInterval: 2, maxRelicsPerRun: 6));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(clearedFloor: 1, ownedRelicCount: 10, draftInterval: 2, maxRelicsPerRun: 6));
        }

        // MARK: - interval=1 (毎階層) は旧来どおり常に true (上限未達の間)

        [Test]
        public void ShouldOfferDraft_IntervalOne_AlwaysTrueForAnyFloor()
        {
            foreach (int floor in new[] { 1, 2, 3, 7, 41, 99 })
            {
                Assert.IsTrue(RelicConfig.ShouldOfferDraft(floor, ownedRelicCount: 0, draftInterval: 1, maxRelicsPerRun: 999),
                    $"interval=1 は floor={floor} でも常に true のはず");
            }
        }

        // MARK: - interval>1: clearedFloor が倍数のときのみ true (初回クリアを除く)

        [Test]
        public void ShouldOfferDraft_IntervalTwo_TrueOnEvenFloors_FalseOnOddFloorsAboveOne()
        {
            const int interval = 2;
            const int cap = 999;

            Assert.IsTrue(RelicConfig.ShouldOfferDraft(2, 0, interval, cap));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(4, 0, interval, cap));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(6, 0, interval, cap));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(8, 0, interval, cap));

            Assert.IsFalse(RelicConfig.ShouldOfferDraft(3, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(5, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(7, 0, interval, cap));
        }

        [Test]
        public void ShouldOfferDraft_IntervalThree_TrueOnlyOnMultiplesOfThree()
        {
            const int interval = 3;
            const int cap = 999;

            Assert.IsTrue(RelicConfig.ShouldOfferDraft(3, 0, interval, cap));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(6, 0, interval, cap));
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(9, 0, interval, cap));

            Assert.IsFalse(RelicConfig.ShouldOfferDraft(2, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(4, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(5, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(7, 0, interval, cap));
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(8, 0, interval, cap));
        }

        // MARK: - 所持数上限 (maxRelicsPerRun)

        [Test]
        public void ShouldOfferDraft_OwnedBelowCap_ReturnsTrueWhenIntervalConditionMet()
        {
            // interval=2, floor=4 (倍数) かつ所持5 < 上限6 → true
            Assert.IsTrue(RelicConfig.ShouldOfferDraft(4, ownedRelicCount: 5, draftInterval: 2, maxRelicsPerRun: 6));
        }

        [Test]
        public void ShouldOfferDraft_OwnedExactlyAtCap_ReturnsFalse()
        {
            // interval=2, floor=4 (倍数で本来ならtrue) でも所持数が上限に到達していれば false。
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(4, ownedRelicCount: 6, draftInterval: 2, maxRelicsPerRun: 6));
        }

        [Test]
        public void ShouldOfferDraft_OwnedAboveCap_ReturnsFalse()
        {
            Assert.IsFalse(RelicConfig.ShouldOfferDraft(4, ownedRelicCount: 7, draftInterval: 2, maxRelicsPerRun: 6));
        }

        // MARK: - 既定値 (GameController が実際に使う引数省略呼び出し) が RelicConfig の定数と一致すること

        [Test]
        public void ShouldOfferDraft_DefaultParameters_MatchRelicConfigConstants()
        {
            // 引数省略時の挙動が、明示的に RelicConfig.DraftInterval/MaxRelicsPerRun を渡した場合と
            // 完全に一致することを確認する。これにより、本番既定値 (RelicConfig の定数) が将来
            // 再チューニングされても、このテスト自体は境界値のロジックではなく
            // 「省略時は定数を見る」という契約だけを検証し続けられる。
            for (int floor = 1; floor <= 12; floor++)
            {
                for (int owned = 0; owned <= RelicConfig.MaxRelicsPerRun + 1; owned++)
                {
                    bool withDefaults = RelicConfig.ShouldOfferDraft(floor, owned);
                    bool withExplicitConsts = RelicConfig.ShouldOfferDraft(
                        floor, owned, RelicConfig.DraftInterval, RelicConfig.MaxRelicsPerRun);
                    Assert.AreEqual(withExplicitConsts, withDefaults,
                        $"floor={floor}, owned={owned}: 引数省略時は RelicConfig の定数と一致するはず");
                }
            }
        }
    }
}
