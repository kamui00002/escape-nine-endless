// DailyChallenge.cs
// Swift 正本からの忠実移植: Models/DailyChallenge.swift
// Swift は enum-with-associated-values だが、C# では Kind + ペイロードの struct で表現する。

using System.Collections.Generic;

namespace EscapeNine.Core
{
    public enum ChallengeConditionKind
    {
        CharacterLock, // 指定キャラで挑戦
        NoSkillAllowed, // スキル使用禁止
        ForcedAI,       // AI難易度固定
        StartFloor      // 開始フロア指定
    }

    /// <summary>デイリーチャレンジの条件。Swift: enum ChallengeCondition</summary>
    public readonly struct ChallengeCondition
    {
        public readonly ChallengeConditionKind Kind;
        public readonly CharacterType Character; // CharacterLock のみ有効
        public readonly AILevel AILevel;         // ForcedAI のみ有効
        public readonly int Floor;               // StartFloor のみ有効

        private ChallengeCondition(ChallengeConditionKind kind, CharacterType character, AILevel aiLevel, int floor)
        {
            Kind = kind;
            Character = character;
            AILevel = aiLevel;
            Floor = floor;
        }

        public static ChallengeCondition CharacterLock(CharacterType type) =>
            new ChallengeCondition(ChallengeConditionKind.CharacterLock, type, AILevel.Normal, 0);

        public static ChallengeCondition NoSkillAllowed() =>
            new ChallengeCondition(ChallengeConditionKind.NoSkillAllowed, CharacterType.Hero, AILevel.Normal, 0);

        public static ChallengeCondition ForcedAI(AILevel level) =>
            new ChallengeCondition(ChallengeConditionKind.ForcedAI, CharacterType.Hero, level, 0);

        public static ChallengeCondition StartFloor(int floor) =>
            new ChallengeCondition(ChallengeConditionKind.StartFloor, CharacterType.Hero, AILevel.Normal, floor);

        /// <summary>表示文字列。Swift: ChallengeCondition.description</summary>
        public string Description
        {
            get
            {
                switch (Kind)
                {
                    case ChallengeConditionKind.CharacterLock: return $"{Character.Name()}で挑戦";
                    case ChallengeConditionKind.NoSkillAllowed: return "スキル使用禁止";
                    case ChallengeConditionKind.ForcedAI: return $"鬼の強さ固定: {AILevel.RawValue()}";
                    case ChallengeConditionKind.StartFloor: return $"{Floor}階層スタート";
                    default: return "";
                }
            }
        }
    }

    /// <summary>デイリーチャレンジ。Swift: struct DailyChallenge</summary>
    public sealed class DailyChallenge
    {
        public string Date;                       // "2026-03-17" 形式
        public List<ChallengeCondition> Conditions;
        public bool IsCompleted;
        public int? AchievedFloor;

        public DailyChallenge(string date, List<ChallengeCondition> conditions, bool isCompleted = false, int? achievedFloor = null)
        {
            Date = date;
            Conditions = conditions;
            IsCompleted = isCompleted;
            AchievedFloor = achievedFloor;
        }

        public string DisplayTitle => $"デイリーチャレンジ {Date}";
    }
}
