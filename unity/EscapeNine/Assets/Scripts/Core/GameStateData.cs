// GameStateData.cs
// Swift 正本からの忠実移植: Models/GameState.swift (struct GameState)
// C# では GameState 名の衝突を避けるため GameStateData とする (enum GameStatus は別ファイル)。

namespace EscapeNine.Core
{
    public sealed class GameStateData
    {
        public int CurrentFloor = 1;
        public int TurnCount = 0;
        public int PlayerPosition = 1;
        public int EnemyPosition = 9;
        public GameStatus Status = GameStatus.Playing;
        public AILevel AILevel = AILevel.Normal;
        public SpecialRule SpecialRule = SpecialRule.None;
        public int SkillUsageCount = 0;
        public int MaxSkillUsage = 5;
    }
}
