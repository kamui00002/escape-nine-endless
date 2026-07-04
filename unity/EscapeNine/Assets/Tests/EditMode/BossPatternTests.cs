// BossPatternTests.cs
// Unity Phase 5c「ローグライク深化」設計文書 §5 (ボスパターン) / §6.4 に基づくテスト。
//
// 検証観点:
//   - BossPatternRotation.PatternForTurn: 2ターンごとの固定順ローテーション、
//     Floor40未満では③威圧が出現しないこと
//   - GameSession.CurrentBossPattern がターン経過とともに同じ順序で切り替わること
//   - ③威圧パターンの TemporaryBossZone (GetAvailableMovesからの除外・進入時の敗北・1ターンで消える)
//   - レリック(#9 幻惑の粉 / #2 残像のヴェール)がボス階では効かないこと (§5.3の意図的な仕様決定)
//
// 盤面:
//   1 2 3
//   4 5 6
//   7 8 9
//
// FakeRandom は AIEngineTests.cs で定義されたもの (internal, 同一アセンブリ内で再利用可能)。
// FakeRandom() (キュー空) は NextDouble()==0.0 を返すため、BossAI(追跡95%)は常に追跡分岐になる。

using System.Linq;
using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class BossPatternTests
    {
        private static GameSession NewBossSession(CharacterType type = CharacterType.Hero, AILevel aiLevel = AILevel.Normal)
        {
            return new GameSession(
                Character.GetCharacter(type),
                aiLevel,
                new AIEngine(new FakeRandom()),
                new FakeRandom());
        }

        // --- BossPatternRotation (純粋関数) ---

        [Test]
        public void PatternForTurn_TwoPatternCycle_BelowFloor40()
        {
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(0, 30));
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(1, 30));
            Assert.AreEqual(BossPattern.Foresight, BossPatternRotation.PatternForTurn(2, 30));
            Assert.AreEqual(BossPattern.Foresight, BossPatternRotation.PatternForTurn(3, 30));
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(4, 30), "①②のみで循環して戻る");
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(4, 10));
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(4, 20));
        }

        [Test]
        public void PatternForTurn_ThreePatternCycle_FromFloor40()
        {
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(0, 40));
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(1, 40));
            Assert.AreEqual(BossPattern.Foresight, BossPatternRotation.PatternForTurn(2, 40));
            Assert.AreEqual(BossPattern.Foresight, BossPatternRotation.PatternForTurn(3, 40));
            Assert.AreEqual(BossPattern.Intimidation, BossPatternRotation.PatternForTurn(4, 40));
            Assert.AreEqual(BossPattern.Intimidation, BossPatternRotation.PatternForTurn(5, 40));
            Assert.AreEqual(BossPattern.Pursuit, BossPatternRotation.PatternForTurn(6, 40), "一周して①に戻る");
        }

        [Test]
        public void PatternForTurn_IntimidationNeverAppearsBelowFloor40()
        {
            for (int floor = 10; floor <= 30; floor += 10)
            {
                for (int turn = 0; turn < 20; turn++)
                {
                    Assert.AreNotEqual(BossPattern.Intimidation, BossPatternRotation.PatternForTurn(turn, floor),
                        $"floor={floor}, turn={turn} で③威圧が出現してはいけない");
                }
            }
        }

        // --- AIEngine.CalculateBossMove (移動アルゴリズム本体) ---

        [Test]
        public void CalculateBossMove_Pursuit_MatchesExistingBossAI()
        {
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.0 })); // roll<0.95 → 追跡
            // enemy=6, player=1: 追跡はNormalAIと同じGetMoveTowardsPlayerを使うため結果は3
            // (CalculateBossMove_Foresight_MatchesHardAIPrediction の5と分かれる決定論的な組み合わせ)。
            Assert.AreEqual(3, ai.CalculateBossMove(6, 1, BossPattern.Pursuit, 0));
        }

        [Test]
        public void CalculateBossMove_Foresight_MatchesHardAIPrediction()
        {
            var ai = new AIEngine(new FakeRandom());
            // enemy=6, player=1: HardAI由来の予測先読みでは5になる
            // (RelicEffectsTests.NeutralizeHardPrediction_DowngradesHardAIForThisCall と同じ手計算根拠)。
            Assert.AreEqual(5, ai.CalculateBossMove(6, 1, BossPattern.Foresight, 0));
        }

        [Test]
        public void CalculateBossMove_Intimidation_StillChasesLikePursuit_DoesNotStandStill()
        {
            // §5.2: ③はFloor40+で解禁され「既存の難易度ランプ哲学と整合させる」ためのパターンなので、
            // 追跡を止めてしまうと最新解禁パターンが逆に休憩ターンになりランプが逆転してしまう。
            // よって移動自体はPursuitと同じ (BossAI) であることを直接検証する
            // (隣接マスへの威圧はGameSession.TemporaryBossZone側の独立した効果、§5.1・§5.2)。
            var ai = new AIEngine(new FakeRandom(doubles: new[] { 0.0 }));
            Assert.AreEqual(3, ai.CalculateBossMove(6, 1, BossPattern.Intimidation, 0));
        }

        [Test]
        public void CalculateIntimidationZone_CyclesThroughAdjacentCells()
        {
            var ai = new AIEngine(new FakeRandom());
            // enemy=9: GameEngine.GetAvailableMoves(9) = {6,8} (上, 左の順)
            Assert.AreEqual(6, ai.CalculateIntimidationZone(9, 0));
            Assert.AreEqual(8, ai.CalculateIntimidationZone(9, 1));
            Assert.AreEqual(6, ai.CalculateIntimidationZone(9, 2), "周期選択なので2周目は先頭に戻る");
        }

        // --- GameSession 統合 ---

        [Test]
        public void GameSession_CurrentBossPatternRotatesEveryTwoTurns_FromFloor40()
        {
            var s = NewBossSession();
            s.StartGame(39, 1, 9);
            Assert.AreEqual(FloorAdvanceResult.Advanced, s.NextFloor());
            Assert.AreEqual(40, s.CurrentFloor);
            Assert.IsTrue(s.IsBossFloor);

            // 敵を拘束し続け、AI呼び出し自体を経由させずローテーションの純粋な経過だけを検証する。
            s.EnemyStoppedTurns = 100;
            s.PlayerPosition = 1;
            s.EnemyPosition = 9;

            var observed = new System.Collections.Generic.List<BossPattern>();
            for (int i = 0; i < 6; i++)
            {
                observed.Add(s.CurrentBossPattern);
                s.PendingPlayerMove = s.PlayerPosition; // その場待機 (同マス移動は有効)
                Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    BossPattern.Pursuit, BossPattern.Pursuit,
                    BossPattern.Foresight, BossPattern.Foresight,
                    BossPattern.Intimidation, BossPattern.Intimidation
                },
                observed);
        }

        [Test]
        public void TemporaryBossZone_OnlyActiveDuringIntimidationTurns_ThenClearsAfterRotating()
        {
            var s = NewBossSession();
            s.StartGame(39, 1, 9);
            s.NextFloor(); // floor40
            s.EnemyStoppedTurns = 100; // 敵の位置(9)を固定してゾーン計算を安定させる
            s.PlayerPosition = 1;
            s.EnemyPosition = 9; // GameEngine.GetAvailableMoves(9) = {6,8}

            // turn0,1: Pursuit → ゾーンなし
            Assert.AreEqual(0, s.TemporaryBossZone.Count);
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();
            Assert.AreEqual(0, s.TemporaryBossZone.Count);
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();

            // turn2,3: Foresight → ゾーンなし
            Assert.AreEqual(0, s.TemporaryBossZone.Count);
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();
            Assert.AreEqual(0, s.TemporaryBossZone.Count);
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();

            // turn4,5: Intimidation → ゾーンあり (隣接{6,8}のうち周期選択で切り替わる)
            Assert.AreEqual(BossPattern.Intimidation, s.CurrentBossPattern);
            Assert.IsTrue(s.TemporaryBossZone.SetEquals(new[] { 6 }));
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();
            Assert.AreEqual(BossPattern.Intimidation, s.CurrentBossPattern);
            Assert.IsTrue(s.TemporaryBossZone.SetEquals(new[] { 8 }));
            s.PendingPlayerMove = s.PlayerPosition; s.ResolveTurn();

            // turn6: 再びPursuitへ循環 → ゾーンは1ターンで消える
            Assert.AreEqual(BossPattern.Pursuit, s.CurrentBossPattern);
            Assert.AreEqual(0, s.TemporaryBossZone.Count);
        }

        [Test]
        public void TemporaryBossZone_ExcludedFromAvailableMoves_AndEnteringItCausesDefeat()
        {
            var s = NewBossSession();
            s.StartGame(39, 1, 9);
            s.NextFloor(); // floor40
            s.EnemyStoppedTurns = 100;
            s.PlayerPosition = 5; // 中央 (上下左右すべて移動可能)
            s.EnemyPosition = 9;

            // turn0〜3を空送りしてturn4 (Intimidation) まで進める
            for (int i = 0; i < 4; i++)
            {
                s.PendingPlayerMove = s.PlayerPosition;
                Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            }

            Assert.AreEqual(BossPattern.Intimidation, s.CurrentBossPattern);
            int zoneCell = s.TemporaryBossZone.First();
            Assert.IsFalse(s.GetAvailableMoves().Contains(zoneCell), "威圧ゾーンはGetAvailableMovesから除外される");

            s.PendingPlayerMove = zoneCell; // 直接代入 (SelectMoveを経由しない危険な移動)
            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn());
            Assert.AreEqual(DefeatReason.CaughtByEnemy, s.LastDefeatReason);
        }

        // --- レリック非干渉 (§5.3) ---

        [Test]
        public void RelicNeutralizeHardPrediction_DoesNotAffectBossFloorMovement()
        {
            // §5.3: ボス階は effective を経由しないため #9 幻惑の粉 (NeutralizeHardPrediction) は no-op。
            var baseline = NewBossSession();
            baseline.StartGame(39, 1, 9);
            baseline.NextFloor(); // floor40, turn0 = Pursuit
            baseline.PlayerPosition = 1;
            baseline.EnemyPosition = 9;
            baseline.PendingPlayerMove = 2;
            Assert.AreEqual(TurnResult.Continued, baseline.ResolveTurn());

            var withRelic = NewBossSession();
            withRelic.Relics.NeutralizeHardPrediction = true;
            withRelic.StartGame(39, 1, 9);
            withRelic.NextFloor();
            withRelic.PlayerPosition = 1;
            withRelic.EnemyPosition = 9;
            withRelic.PendingPlayerMove = 2;
            Assert.AreEqual(TurnResult.Continued, withRelic.ResolveTurn());

            Assert.AreEqual(baseline.EnemyPosition, withRelic.EnemyPosition,
                "ボス階ではNeutralizeHardPredictionが効かず、両者は同じ挙動になる");
        }

        [Test]
        public void RelicThiefResidualVeil_DoesNotOverrideBossPattern_EvenWhenDiagonalSkillUsed()
        {
            // §5.3: #2 残像のヴェールは「発動条件自体は満たしうる」ため、明示的にボス階では効かせない仕様。
            var baseline = NewBossSession(CharacterType.Thief);
            baseline.StartGame(39, 1, 9);
            baseline.NextFloor(); // floor40, turn0 = Pursuit
            baseline.PlayerPosition = 1;
            baseline.EnemyPosition = 9;
            baseline.PendingPlayerMove = 5; // 1→5 は斜め移動 (盗賊スキル消費)

            var withRelic = NewBossSession(CharacterType.Thief);
            withRelic.Relics.ThiefResidualVeil = true;
            withRelic.StartGame(39, 1, 9);
            withRelic.NextFloor();
            withRelic.PlayerPosition = 1;
            withRelic.EnemyPosition = 9;
            withRelic.PendingPlayerMove = 5;

            Assert.AreEqual(TurnResult.Continued, baseline.ResolveTurn());
            Assert.AreEqual(TurnResult.Continued, withRelic.ResolveTurn());
            Assert.AreEqual(baseline.EnemyPosition, withRelic.EnemyPosition,
                "ボス階では#2残像のヴェールが発動条件を満たしても敵移動に影響しない");
        }
    }
}
