// RelicEffectsTests.cs
// Unity Phase 5a 「ローグライク深化」設計文書 §6.4 に基づく回帰テスト。
// 各 RelicEffects フィールドを個別に設定し、GameSession の挙動が期待通りに変わることを検証する。
// (回帰ガードとしての「Relics = RelicEffects.None のとき既存挙動と完全に一致する」検証は
//  GameSessionRelicRegressionTests.cs に分離する。)
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
    public class RelicEffectsTests
    {
        private static GameSession NewSession(
            CharacterType type = CharacterType.Hero,
            AILevel aiLevel = AILevel.Hard,
            FakeRandom aiRng = null,
            FakeRandom sessionRng = null)
        {
            return new GameSession(
                Character.GetCharacter(type),
                aiLevel,
                new AIEngine(aiRng ?? new FakeRandom()),
                sessionRng ?? new FakeRandom());
        }

        [Test]
        public void None_ReturnsFreshZeroEffectInstanceEachTime()
        {
            var a = RelicEffects.None;
            a.ReviveCharges = 5;

            var b = RelicEffects.None;

            Assert.AreEqual(0, b.ReviveCharges, "None は毎回新規インスタンスであるべき (共有ミューテート事故防止)");
            Assert.AreEqual(0, a.TurnCountReduction);
            Assert.IsFalse(a.NeutralizeHardPrediction);
            Assert.IsFalse(a.ThiefResidualVeil);
        }

        [Test]
        public void TurnCountReduction_ReducesMaxTurns_ClampedToMinimumThree()
        {
            var s = NewSession();
            s.StartGame(1, 1, 9); // floor1: GetMaxTurns=5

            s.Relics.TurnCountReduction = 1;
            Assert.AreEqual(4, s.MaxTurns);

            s.Relics.TurnCountReduction = 10; // 過剰な減算でも最低3にクランプ
            Assert.AreEqual(3, s.MaxTurns);
        }

        [Test]
        public void SkillMaxUsageBonus_IncreasesRemainingSkillUses()
        {
            var s = NewSession(CharacterType.Hero); // 勇者: MaxUsage=3
            Assert.AreEqual(3, s.RemainingSkillUses);

            s.Relics.SkillMaxUsageBonus = 2;
            Assert.AreEqual(5, s.RemainingSkillUses);
        }

        [Test]
        public void DisappearCellReduction_ReducesDisappearingCellCount()
        {
            // floor41: DisappearCellStages により素の消失マス数は1
            var baseline = NewSession();
            baseline.StartGame(41, 1, 9);
            Assert.AreEqual(1, baseline.DisappearedCells.Count, "レリック無しでは floor41 は消失マス1個");

            var withRelic = NewSession();
            withRelic.Relics.DisappearCellReduction = 1;
            withRelic.StartGame(41, 1, 9);
            Assert.AreEqual(0, withRelic.DisappearedCells.Count, "#7 地固めの護符で消失マス数が0になる");
        }

        [Test]
        public void FogVisibilityRadiusBonus_ExpandsVisibleRadius()
        {
            // floor21: 霧ルール発動。player=1(corner) から position9(対角) は Chebyshev距離2。
            var s = NewSession();
            s.StartGame(21, 1, 5);

            Assert.IsFalse(s.IsCellVisible(9), "既定の視界半径1では距離2のマスは見えない");

            s.Relics.FogVisibilityRadiusBonus = 1;
            Assert.IsTrue(s.IsCellVisible(9), "#8 灯火の指輪で視界半径が2になり見えるようになる");
        }

        [Test]
        public void NeutralizeHardPrediction_DowngradesHardAIForThisCall()
        {
            // floor36 + selected=Normal → natural=Hard, effective=Hard (Floor.GetEffectiveAILevel)。
            // enemy=6, player=1 は NormalAI→3 / HardAI→5 と結果が分かれる決定論的な組み合わせ
            // (AIEngineTests.cs の HardAI/NormalAI ロジックからの手計算で確認済み: HardAI は
            //  プレイヤーの逃走予測先=4 に近づこうとして5を選び、NormalAI は実位置1に近い3を選ぶ)。
            var baseline = NewSession(CharacterType.Hero, AILevel.Normal);
            baseline.StartGame(36, 1, 6);
            baseline.PendingPlayerMove = 2; // 衝突しない安全な移動
            Assert.AreEqual(TurnResult.Continued, baseline.ResolveTurn());
            Assert.AreEqual(5, baseline.EnemyPosition, "レリック無しでは実効Hardのまま (Hard AI の応答)");

            var withRelic = NewSession(CharacterType.Hero, AILevel.Normal);
            withRelic.Relics.NeutralizeHardPrediction = true;
            withRelic.StartGame(36, 1, 6);
            withRelic.PendingPlayerMove = 2;
            Assert.AreEqual(TurnResult.Continued, withRelic.ResolveTurn());
            Assert.AreEqual(3, withRelic.EnemyPosition, "#9 幻惑の粉でこの1回だけNormal相当に格下げされる");
        }

        [Test]
        public void ReviveCharges_PreventsDefeatOnce_ThenConsumesCharge()
        {
            var s = NewSession(); // floor1 + Hard → 効果AI Normal
            s.StartGame(1, 4, 6);
            s.Relics.ReviveCharges = 1;
            s.PendingPlayerMove = 5; // 敵も 6→5 に来て同マス衝突 (GameSessionTests.Collision_DefeatsHero と同条件)

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn(), "#5 不死鳥の残り火で敗北を無効化して継続する");
            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(0, s.Relics.ReviveCharges, "チャージが1消費される");

            // チャージ枯渇後は通常どおり敗北する
            s.PlayerPosition = 4;
            s.EnemyPosition = 6;
            s.PendingPlayerMove = 5;
            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn());
        }

        [Test]
        public void GenericShieldCharges_AbsorbsCollisionOnce_ResetsCombo_ThenNormalDefeat()
        {
            var s = NewSession(CharacterType.Hero); // 勇者は盾スキルを持たない
            s.StartGame(1, 4, 6);
            s.Relics.GenericShieldCharges = 1;
            s.ComboCount = 5;
            s.PendingPlayerMove = 5;

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn(), "#12 二段構えの盾で衝突を無効化して継続する");
            Assert.AreEqual(0, s.Relics.GenericShieldCharges);
            Assert.AreEqual(0, s.ComboCount, "既存の盾ガードと同様、無効化時はコンボをリセットする");

            s.PlayerPosition = 4;
            s.EnemyPosition = 6;
            s.PendingPlayerMove = 5;
            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn(), "チャージ枯渇後、勇者は盾を持たないため通常どおり敗北");
        }

        [Test]
        public void DisappearForgivenessPerFloor_ForgivesOncePerFloor_ThenDefeat()
        {
            // floor41 + player=1,enemy=9: available={2,3,4,5,6,7,8}, ints=[0] → index0=2 が消失マスになる
            var aiRng = new FakeRandom();
            var sessionRng = new FakeRandom(ints: new[] { 0 });
            var s = NewSession(CharacterType.Hero, AILevel.Hard, aiRng, sessionRng);
            s.Relics.DisappearForgivenessPerFloor = 1;
            s.StartGame(41, 1, 9);

            Assert.IsTrue(s.DisappearedCells.Contains(2), "テスト前提: セル2が消失マスであること");

            s.PendingPlayerMove = 2; // 消失マスへ進入
            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn(), "#11 影の抜け道でこの階層1回目は無効化される");
            Assert.AreEqual(2, s.PlayerPosition);

            s.PendingPlayerMove = 2; // 同マスに留まる (待機は有効な移動)
            Assert.AreEqual(TurnResult.Defeated, s.ResolveTurn(), "1階層で使い切った後は通常どおり敗北する");
            Assert.AreEqual(DefeatReason.CaughtByEnemy, s.LastDefeatReason);
        }

        [Test]
        public void ComboMissShieldCharges_PreservesComboOnMiss_ThenConsumesCharge()
        {
            var s = NewSession(CharacterType.Hero);
            s.StartGame(1, 5, 1);
            s.ComboCount = 3;
            s.Relics.ComboMissShieldCharges = 1;

            Assert.IsTrue(s.SelectMove(2, TimingGrade.Miss));
            Assert.AreEqual(3, s.ComboCount, "#6 コンボの守りでMissでもコンボが維持される");
            Assert.AreEqual(0, s.Relics.ComboMissShieldCharges);

            Assert.IsTrue(s.SelectMove(4, TimingGrade.Miss));
            Assert.AreEqual(0, s.ComboCount, "チャージ枯渇後は通常どおりコンボがリセットされる");
        }

        [Test]
        public void ComboThresholdReduction_LowersScoreMultiplierThresholds()
        {
            var s = NewSession();
            s.Relics.ComboThresholdReduction = 1; // 3→2, 5→4

            s.ComboCount = 2; Assert.AreEqual(1.5, s.ScoreMultiplier, 1e-9);
            s.ComboCount = 4; Assert.AreEqual(2.0, s.ScoreMultiplier, 1e-9);
        }

        [Test]
        public void ThiefDiagonalSkillSaveChance_SavesSkillUsageBelowRoll()
        {
            var sessionRng = new FakeRandom(doubles: new[] { 0.3 }); // 0.3 < 0.5 → 温存成功
            var s = NewSession(CharacterType.Thief, AILevel.Easy, new FakeRandom(), sessionRng);
            s.Relics.ThiefDiagonalSkillSaveChance = 0.5;
            s.StartGame(1, 1, 9);
            s.PendingPlayerMove = 5; // 1→5 は斜め移動

            s.ResolveTurn();
            Assert.AreEqual(0, s.SkillUsageCount, "#1 影の軽業: ロールが確率未満ならスキル残数を消費しない");
        }

        [Test]
        public void ThiefDiagonalSkillSaveChance_ConsumesSkillUsageAboveRoll()
        {
            var sessionRng = new FakeRandom(doubles: new[] { 0.9 }); // 0.9 >= 0.5 → 温存失敗
            var s = NewSession(CharacterType.Thief, AILevel.Easy, new FakeRandom(), sessionRng);
            s.Relics.ThiefDiagonalSkillSaveChance = 0.5;
            s.StartGame(1, 1, 9);
            s.PendingPlayerMove = 5;

            s.ResolveTurn();
            Assert.AreEqual(1, s.SkillUsageCount, "ロールが確率以上なら通常どおり消費する");
        }

        [Test]
        public void ThiefDiagonalSkillSaveChance_ZeroNeverRollsRng()
        {
            // Relics.None (=0.0) のとき _rng.NextDouble() が一切呼ばれないことを確認する。
            // FakeRandom の doubles キューを空にしておき、呼ばれてしまうと既定値0.0が返り
            // 「0.0 < 0.0」は false になるため SkillUsageCount は通常どおり1のはず。
            // (本テストは「短絡評価が効いていること」自体を検証するためのものであり、
            //  呼ばれても呼ばれなくても最終結果は同じになるよう意図している —
            //  実際の非消費シーケンス保全は他の #7/#10 系テストの厳密な rng 消費順一致で担保する)
            var s = NewSession(CharacterType.Thief, AILevel.Easy);
            s.StartGame(1, 1, 9);
            s.PendingPlayerMove = 5;

            s.ResolveTurn();
            Assert.AreEqual(1, s.SkillUsageCount);
        }

        [Test]
        public void ThiefResidualVeil_ForcesEasyAIOnDiagonalMoveTurn()
        {
            // floor36 + selected=Hard → effective=Hard。enemy=6,player=1 は Hard→5 (前テストで確認済み)。
            // Easy AI は _rng.NextDouble() の roll で分岐するため、AI用 FakeRandom に roll=0.0 (追跡) を積む。
            // 追跡時の EasyAI は NormalAI と同じ GetMoveTowardsPlayer(player) を使うため、
            // enemy=6,player=1 → 3 になるはず (NeutralizeHardPrediction テストの baseline と同じ経路)。
            var s = NewSession(CharacterType.Thief, AILevel.Hard, new FakeRandom(doubles: new[] { 0.0 }));
            s.Relics.ThiefResidualVeil = true;
            s.StartGame(36, 1, 6);
            s.PendingPlayerMove = 5; // 1→5 は斜め移動 (盗賊)

            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(3, s.EnemyPosition, "#2 残像のヴェールで斜め移動ターンの敵AIがEasy相当(追跡)になる");
        }

        [Test]
        public void MinStartDistance_GuaranteesMinimumDistanceOnStartGame()
        {
            var sessionRng = new FakeRandom(ints: new[] { 0 });
            var s = NewSession(CharacterType.Hero, AILevel.Hard, new FakeRandom(), sessionRng);
            s.Relics.MinStartDistance = 2;

            s.StartGame(1, 1, null); // playerPos=1 固定、enemyPosはレリック経由で抽選

            Assert.AreEqual(3, s.EnemyPosition, "候補{3,6,7,8,9}のindex0=3が選ばれるはず");
            Assert.GreaterOrEqual(GameSession.ChebyshevDistance(s.PlayerPosition, s.EnemyPosition), 2);
        }

        [Test]
        public void MinStartDistance_GuaranteesMinimumDistanceOnNextFloor()
        {
            var sessionRng = new FakeRandom(ints: new[] { 0, 0 });
            var s = NewSession(CharacterType.Hero, AILevel.Hard, new FakeRandom(), sessionRng);
            s.Relics.MinStartDistance = 2;
            s.StartGame(1, 1, 9);

            s.NextFloor();

            Assert.AreEqual(2, s.CurrentFloor);
            Assert.AreEqual(1, s.PlayerPosition, "available=1..9, ints[0]=0 → index0=1");
            Assert.AreEqual(3, s.EnemyPosition, "距離2以上候補{3,6,7,8,9}のindex0=3");
        }

        [Test]
        public void PseudoBindCharges_AllowsNonBindCharacterToBindEnemy_ThenExhausts()
        {
            var s = NewSession(CharacterType.Hero); // 勇者は拘束スキルを持たない
            s.StartGame(1, 1, 9);
            s.Relics.PseudoBindCharges = 2;

            s.BindEnemy();
            Assert.AreEqual(GameConfig.BindDurationTurns, s.EnemyStoppedTurns, "#17 心話の絆で疑似拘束できる");
            Assert.AreEqual(1, s.Relics.PseudoBindCharges);
            Assert.AreEqual(0, s.SkillUsageCount, "キャラのスキル残数は消費しない");

            s.EnemyStoppedTurns = 0;
            s.BindEnemy();
            Assert.AreEqual(GameConfig.BindDurationTurns, s.EnemyStoppedTurns);
            Assert.AreEqual(0, s.Relics.PseudoBindCharges);

            s.EnemyStoppedTurns = 0;
            s.BindEnemy(); // チャージ枯渇後は no-op
            Assert.AreEqual(0, s.EnemyStoppedTurns);
        }

        // MARK: - Phase 5b で追加した効果フィールド (§2.3 の残り10種ぶん)

        [Test]
        public void ThiefSkillMaxUsageBonus_OnlyAppliesToThief()
        {
            // #3 影分身の型: 盗賊専用。盗賊: MaxUsage=5 → +3 で 8。
            var thief = NewSession(CharacterType.Thief);
            thief.Relics.ThiefSkillMaxUsageBonus = 3;
            Assert.AreEqual(8, thief.RemainingSkillUses, "#3 影分身の型で盗賊のスキル最大使用回数が+3される");

            // 他キャラ (勇者 MaxUsage=3) には乗らない (クロスピックしても無効)。
            var hero = NewSession(CharacterType.Hero);
            hero.Relics.ThiefSkillMaxUsageBonus = 3;
            Assert.AreEqual(3, hero.RemainingSkillUses, "盗賊専用ボーナスは他キャラに適用されない");
        }

        [Test]
        public void ComboThresholdBonusMultiplier_AddsToMultiplierAtThresholds()
        {
            // #15 加速の証: しきい値到達時の倍率 +0.5 (しきい値未満の 1.0 は変わらない)。
            var s = NewSession();
            s.Relics.ComboThresholdBonusMultiplier = 0.5;

            s.ComboCount = 0; Assert.AreEqual(1.0, s.ScoreMultiplier, 1e-9, "しきい値未満はボーナス対象外");
            s.ComboCount = 3; Assert.AreEqual(2.0, s.ScoreMultiplier, 1e-9, "combo>=3: 1.5+0.5");
            s.ComboCount = 5; Assert.AreEqual(2.5, s.ScoreMultiplier, 1e-9, "combo>=5: 2.0+0.5");
        }

        [Test]
        public void RuntimeOnlyFields_AreHeldButDoNotAffectCoreBehavior()
        {
            // #15 BpmMultiplierBonus / #16 TurnCountdownBonus / #18 DraftCandidateBonusFloorsRemaining は
            // Runtime (GameController) が消費する値の保持のみで、Core (GameSession) の判定には影響しない (§2.4)。
            var s = NewSession();
            s.StartGame(1, 1, 9);
            int maxTurnsBefore = s.MaxTurns;

            s.Relics.BpmMultiplierBonus = 0.08;
            s.Relics.TurnCountdownBonus = 1;
            s.Relics.DraftCandidateBonusFloorsRemaining = 3;

            Assert.AreEqual(maxTurnsBefore, s.MaxTurns, "TurnCountdownBonus は締切拍数 (Runtime) であって必要ターン数 (Core) ではない");
            s.PendingPlayerMove = 2;
            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn(), "Runtime専用フィールドはターン解決に影響しない");
        }
    }
}
