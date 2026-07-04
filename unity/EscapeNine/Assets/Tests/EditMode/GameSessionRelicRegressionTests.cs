// GameSessionRelicRegressionTests.cs
// Unity Phase 5a「ローグライク深化」設計文書 §1原則1・§6.4「回帰ガード（最重要）」に基づくテスト。
//
// Relics = RelicEffects.None (既定値) のとき、GameSession の挙動が Phase 5 導入前と
// 完全に一致することを、複数ターン + 階層遷移をまたぐ決定論的スクリプトで検証する。
// 期待値は AIEngine.NormalAI / GameEngine.GetAvailableMoves のロジックを独立に
// Python で再実装して手計算とクロスチェック済み (以下のコメントに追跡した値を記載)。
//
// 既存の GameSessionTests.cs / AIEngineTests.cs 等、80本の既存テストは本タスクで一切変更していない。
// 本ファイルはそれに加えて「レリックの統合実装そのものが既存挙動を壊していないか」を
// 明示的にアサートする追加の安全網。
//
// 盤面:
//   1 2 3
//   4 5 6
//   7 8 9

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class GameSessionRelicRegressionTests
    {
        [Test]
        public void RelicsNone_MultiTurnAndFloorTransition_MatchesPreRelicBehavior()
        {
            // floor1 + selected=Hard → 効果AI Normal (GameSessionTests.cs の NewSession と同条件)。
            var s = new GameSession(
                Character.GetCharacter(CharacterType.Hero),
                AILevel.Hard,
                new AIEngine(new FakeRandom()),
                new FakeRandom());

            // 明示的に None を代入する (既定値と同じだが、回帰ガードの意図を読み手に明示するため)。
            s.Relics = RelicEffects.None;

            s.StartGame(1, 1, 9);

            // turn1: NormalAI(enemy=9, player=1) = 6 (AIEngineTests.NormalAI_MovesTowardPlayer と同一の組み合わせ)
            s.PendingPlayerMove = 2;
            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(2, s.PlayerPosition);
            Assert.AreEqual(6, s.EnemyPosition);
            Assert.AreEqual(1, s.TurnCount);

            // turn2: NormalAI(enemy=6, player=2) = 3
            s.PendingPlayerMove = 5;
            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(5, s.PlayerPosition);
            Assert.AreEqual(3, s.EnemyPosition);
            Assert.AreEqual(2, s.TurnCount);

            // turn3: NormalAI(enemy=3, player=5) = 6
            s.PendingPlayerMove = 4;
            Assert.AreEqual(TurnResult.Continued, s.ResolveTurn());
            Assert.AreEqual(4, s.PlayerPosition);
            Assert.AreEqual(6, s.EnemyPosition);
            Assert.AreEqual(3, s.TurnCount);

            Assert.AreEqual(GameStatus.Playing, s.Status);
            Assert.AreEqual(0, s.ComboCount, "PendingPlayerMove の直接代入では SelectMove を経由しないためComboCountは動かない");

            // 階層遷移: floor1→2, 特殊ルールなし・消失マスなしのため available は 1..9 の全マス。
            // FakeRandom はキューが空だと常に0を返すため、NextInt(9)=0, NextInt(8)=0 で決定論的。
            var advance = s.NextFloor();
            Assert.AreEqual(FloorAdvanceResult.Advanced, advance);
            Assert.AreEqual(2, s.CurrentFloor);
            Assert.AreEqual(0, s.TurnCount);
            Assert.AreEqual(1, s.PlayerPosition);
            Assert.AreEqual(2, s.EnemyPosition);
        }
    }
}
