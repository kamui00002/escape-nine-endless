// RouteChoiceTests.cs
// Unity Phase 5c「ローグライク深化」設計文書 §4 (分岐ルート) / §6.4 に基づくテスト。
//
// 検証観点:
//   - Abyss選択で実効AIレベル+1段(Hard据え置き)と特殊ルール1段階前倒しの両方が
//     「その1階層限定」で反映されること (§4本文の「AIレベル、または特殊ルール」の並列記述を
//      両方適用する解釈で実装したため、両方の効果を独立に検証する)
//   - 次の NextFloor 呼び出しで自動的に Safe (既定) へ戻ること
//   - DailyChallengeMode 中は Abyss を要求しても常に Safe が強制されること
//   - NextFloor() を引数なしで呼ぶ既存呼び出し元が Phase 5c 導入前と完全に一致する回帰ガード
//
// テスト階層は floor17 を使う (floor16-20は自然難易度Normal域かつ非ボス階、Fog開始(21)未満なので
// Safe時の特殊ルールはNoneになる。floor18/20 は10の倍数のボス階になり得る/隣接するため避ける)。
//
// 盤面:
//   1 2 3
//   4 5 6
//   7 8 9
//
// FakeRandom は AIEngineTests.cs で定義されたもの (internal, 同一アセンブリ内で再利用可能)。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class RouteChoiceTests
    {
        private static GameSession NewSession(AILevel aiLevel)
        {
            return new GameSession(
                Character.GetCharacter(CharacterType.Hero),
                aiLevel,
                new AIEngine(new FakeRandom()),
                new FakeRandom());
        }

        [Test]
        public void Abyss_BumpsEffectiveAILevelByOneStep_ForThatFloorOnly()
        {
            // floor16→17 (Abyss選択): floor17の自然難易度はNormal (AINaturalHardFloor=36未満)、
            // かつ17は10の倍数ではないためボス階ではない (ボス階はeffectiveがFloor.GetEffectiveAILevel
            // 自体で常にBossになり、ResolveTurn側もCalculateBossMoveへ分岐するため検証に使えない)。
            // selectedAI=Normalなので素の実効AIはNormal→Abyssで+1段されHardになる。
            // enemy=6, player=1 は NormalAI→3 / HardAI→5 と結果が分かれる決定論的な組み合わせ
            // (RelicEffectsTests.NeutralizeHardPrediction_DowngradesHardAIForThisCall と同じ手計算根拠、
            //  AIEngineの計算自体は階層に依存しないためそのまま流用できる)。
            var s = NewSession(AILevel.Normal);
            s.StartGame(16, 1, 6);
            Assert.IsFalse(s.IsBossFloor);

            Assert.AreEqual(FloorAdvanceResult.Advanced, s.NextFloor(RouteChoice.Abyss));
            Assert.AreEqual(17, s.CurrentFloor);
            Assert.IsFalse(s.IsBossFloor);
            Assert.AreEqual(RouteChoice.Abyss, s.CurrentRouteChoice);

            // NextFloorの配置乱数で上書きされた位置を検証用に固定し直す (RelicEffectsTests等と同じ手法)。
            s.PlayerPosition = 1;
            s.EnemyPosition = 6;
            s.PendingPlayerMove = 2; // 衝突しない安全な移動

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(5, s.EnemyPosition, "AbyssでNormal→Hardに格上げされ、HardAIの応答(5)になる");
        }

        [Test]
        public void Safe_KeepsNaturalAILevel()
        {
            var s = NewSession(AILevel.Normal);
            s.StartGame(16, 1, 6);

            s.NextFloor(RouteChoice.Safe); // 明示的にSafe (既定と同じ)
            Assert.AreEqual(RouteChoice.Safe, s.CurrentRouteChoice);

            s.PlayerPosition = 1;
            s.EnemyPosition = 6;
            s.PendingPlayerMove = 2;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(3, s.EnemyPosition, "Safeでは実効AIはNormalのまま (NormalAIの応答)");
        }

        [Test]
        public void Abyss_FrontloadsSpecialRuleByOneStage_ForThatFloorOnly()
        {
            // floor17のSafe時の素の特殊ルールはNone (FogStartFloor=21未満)。
            // Abyssで1段階前倒しされNoneからFogになることを検証する。
            var s = NewSession(AILevel.Normal);
            s.StartGame(16, 1, 6);

            s.NextFloor(RouteChoice.Abyss);
            Assert.AreEqual(SpecialRule.Fog, s.CurrentSpecialRule, "Abyssで特殊ルールがNone→Fogに前倒しされる");
        }

        [Test]
        public void Abyss_OverrideClearsAutomaticallyOnNextFloorCall()
        {
            var s = NewSession(AILevel.Normal);
            s.StartGame(16, 1, 6);

            s.NextFloor(RouteChoice.Abyss); // floor17をAbyssで
            Assert.AreEqual(RouteChoice.Abyss, s.CurrentRouteChoice);
            Assert.AreEqual(SpecialRule.Fog, s.CurrentSpecialRule);

            s.NextFloor(); // floor18へ (既定Safe) → 1階層限定のオーバーライドが自動的にクリアされる
            Assert.AreEqual(18, s.CurrentFloor);
            Assert.AreEqual(RouteChoice.Safe, s.CurrentRouteChoice);
            Assert.AreEqual(SpecialRule.None, s.CurrentSpecialRule, "floor18は素のルールNoneに戻る (前倒しは引き継がれない)");
        }

        [Test]
        public void DailyChallengeMode_ForcesSafeEvenWhenAbyssRequested()
        {
            var s = NewSession(AILevel.Normal);
            s.DailyChallengeMode = true;
            s.StartGame(16, 1, 6);

            s.NextFloor(RouteChoice.Abyss); // デイリー中はAbyssを要求してもSafeに強制される
            Assert.AreEqual(RouteChoice.Safe, s.CurrentRouteChoice);
            Assert.AreEqual(SpecialRule.None, s.CurrentSpecialRule, "デイリー中は特殊ルールも前倒しされない");

            s.PlayerPosition = 1;
            s.EnemyPosition = 6;
            s.PendingPlayerMove = 2;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(3, s.EnemyPosition, "デイリー中はAbyss要求してもNormalAIの応答のまま");
        }

        [Test]
        public void NextFloor_DefaultArgument_MatchesPreRouteChoiceBehavior()
        {
            // NextFloor() を引数なしで呼ぶ既存呼び出し元は RouteChoice.Safe 扱いとなり、
            // Phase 5c 導入前と完全に同じ挙動になることの回帰ガード。
            // GameSessionTests.NextFloor_ResetsSkillEveryTenFloors と同条件。
            var s = new GameSession(
                Character.GetCharacter(CharacterType.Hero),
                AILevel.Hard,
                new AIEngine(new FakeRandom()),
                new FakeRandom());
            s.StartGame(1, 1, 9);
            s.CurrentFloor = 10;
            s.SkillUsageCount = 2;

            Assert.AreEqual(FloorAdvanceResult.Advanced, s.NextFloor());
            Assert.AreEqual(11, s.CurrentFloor);
            Assert.AreEqual(0, s.SkillUsageCount);
            Assert.AreEqual(RouteChoice.Safe, s.CurrentRouteChoice);
            Assert.AreEqual(SpecialRule.None, s.CurrentSpecialRule, "floor11はFog開始階(21)未満なのでNone");
        }
    }
}
