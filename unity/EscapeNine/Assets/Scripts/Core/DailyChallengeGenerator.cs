// DailyChallengeGenerator.cs
// Swift 正本からの忠実移植: DailyChallengeService.buildChallenge(for:)
// UTC 日付文字列 → LCG シード → 1〜2 個の条件を決定論的に生成。
// 保存/読込 (UserDefaults) はプラットフォーム依存のため Core には含めない (Runtime 層で実装)。

using System.Collections.Generic;

namespace EscapeNine.Core
{
    public static class DailyChallengeGenerator
    {
        // Swift の CharacterType.allCases と同順 (宣言順)。index はこの並びに依存する。
        private static readonly CharacterType[] AllCharacters =
        {
            CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard, CharacterType.Elf, CharacterType.Knight
        };

        // startFloor 候補 (5の倍数、5〜40)。Swift と同順。
        private static readonly int[] FloorOptions = { 5, 10, 15, 20, 25, 30, 35, 40 };

        // forcedAI は Hard を除外し Easy/Normal のみ。Swift と同順。
        private static readonly AILevel[] ForcedAILevels = { AILevel.Easy, AILevel.Normal };

        /// <summary>日付文字列から当日のチャレンジを生成。Swift: buildChallenge(for:)</summary>
        public static DailyChallenge BuildChallenge(string dateString)
        {
            long seed = SeededRng.SeedFromDateString(dateString);
            var rng = new SeededRng(seed);

            int conditionCount = (rng.NextInt() % 2) + 1; // 1〜2個
            var conditions = new List<ChallengeCondition>();
            var usedTypes = new HashSet<int>();

            for (int i = 0; i < conditionCount; i++)
            {
                int typeIndex;
                do
                {
                    typeIndex = rng.NextInt() % 4;
                } while (usedTypes.Contains(typeIndex));
                usedTypes.Add(typeIndex);

                switch (typeIndex)
                {
                    case 0:
                        int characterIndex = rng.NextInt() % AllCharacters.Length;
                        conditions.Add(ChallengeCondition.CharacterLock(AllCharacters[characterIndex]));
                        break;
                    case 1:
                        conditions.Add(ChallengeCondition.NoSkillAllowed());
                        break;
                    case 2:
                        int levelIndex = rng.NextInt() % ForcedAILevels.Length;
                        conditions.Add(ChallengeCondition.ForcedAI(ForcedAILevels[levelIndex]));
                        break;
                    case 3:
                        int floorIndex = rng.NextInt() % FloorOptions.Length;
                        conditions.Add(ChallengeCondition.StartFloor(FloorOptions[floorIndex]));
                        break;
                }
            }

            return new DailyChallenge(dateString, conditions, isCompleted: false, achievedFloor: null);
        }
    }
}
