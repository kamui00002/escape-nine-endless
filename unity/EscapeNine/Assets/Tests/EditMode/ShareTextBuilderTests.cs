// ShareTextBuilderTests.cs
// Swift 正本 (ShareSheet.ShareTextBuilder) と同一のシェア文字列を担保。

using NUnit.Framework;
using EscapeNine.Core;

namespace EscapeNine.Tests.EditMode
{
    public class ShareTextBuilderTests
    {
        [Test]
        public void Build_DailyChallengeVictory()
        {
            string result = ShareTextBuilder.Build(
                floor: 9,
                elapsedSeconds: 38,
                isVictory: true,
                playerPosition: 2,
                enemyPosition: 9,
                dailyChallengeId: 138);

            string expected =
                "Escape9 #138 → 9階クリア (38秒)\n" +
                "⬛🟩⬛\n" +
                "⬛⬛⬛\n" +
                "⬛⬛🟧\n" +
                "https://escape9.app";

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Build_NormalDefeat_RoundsSeconds()
        {
            string result = ShareTextBuilder.Build(
                floor: 5,
                elapsedSeconds: 12.4,
                isVictory: false,
                playerPosition: 1,
                enemyPosition: 5,
                dailyChallengeId: null);

            string expected =
                "Escape9 → 5階で敗北 (12秒)\n" +
                "🟩⬛⬛\n" +
                "⬛🟧⬛\n" +
                "⬛⬛⬛\n" +
                "https://escape9.app";

            Assert.AreEqual(expected, result);
        }
    }
}
