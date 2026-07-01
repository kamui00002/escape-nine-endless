// ShareTextBuilder.cs
// Swift 正本からの忠実移植: Views/Components/ShareSheet.swift (enum ShareTextBuilder)
// Wordle 風シェアテキストの組み立て。純関数のため決定論的にテスト可能。

using System;

namespace EscapeNine.Core
{
    public static class ShareTextBuilder
    {
        /// <summary>シェア URL (LP 仮値)。本番公開後に差し替え。Swift: shareURL</summary>
        public const string ShareURL = "https://escape9.app";

        private const string CellPlayer = "🟩"; // プレイヤー最終位置
        private const string CellEnemy = "🟧";  // 敵最終位置
        private const string CellEmpty = "⬛";  // その他

        /// <summary>
        /// 結果テキストを組み立てる。Swift: ShareTextBuilder.build(...)
        /// 例:
        ///   Escape9 #138 → 9階クリア (38秒)
        ///   ⬛🟩⬛
        ///   ⬛⬛⬛
        ///   ⬛⬛🟧
        ///   https://escape9.app
        /// </summary>
        public static string Build(
            int floor,
            double elapsedSeconds,
            bool isVictory,
            int playerPosition,
            int enemyPosition,
            int? dailyChallengeId = null)
        {
            string header = BuildHeader(floor, elapsedSeconds, isVictory, dailyChallengeId);
            string grid = BuildGrid(playerPosition, enemyPosition);
            return $"{header}\n{grid}\n{ShareURL}";
        }

        private static string BuildHeader(int floor, double elapsedSeconds, bool isVictory, int? dailyChallengeId)
        {
            string prefix = dailyChallengeId.HasValue ? $"Escape9 #{dailyChallengeId.Value}" : "Escape9";
            string outcome = isVictory ? $"{floor}階クリア" : $"{floor}階で敗北";
            int seconds = (int)Math.Round(elapsedSeconds, MidpointRounding.AwayFromZero); // Swift: elapsedSeconds.rounded()
            return $"{prefix} → {outcome} ({seconds}秒)";
        }

        /// <summary>1-9 の position を 3x3 絵文字グリッドへ。Swift: buildGrid(playerPosition:enemyPosition:)</summary>
        private static string BuildGrid(int playerPosition, int enemyPosition)
        {
            var rows = new string[3];
            for (int row = 0; row < 3; row++)
            {
                string line = "";
                for (int col = 0; col < 3; col++)
                {
                    int position = row * 3 + col + 1; // 1-9
                    if (position == playerPosition) line += CellPlayer;
                    else if (position == enemyPosition) line += CellEnemy;
                    else line += CellEmpty;
                }
                rows[row] = line;
            }
            return string.Join("\n", rows);
        }
    }
}
