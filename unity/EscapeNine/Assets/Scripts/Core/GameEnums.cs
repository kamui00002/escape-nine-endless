// GameEnums.cs
// Swift 正本からの忠実移植: Models/GameState.swift, Models/Character.swift, Models/Skill.swift
// Core は純 .NET (noEngineReferences) — UnityEngine 非依存にして他エンジンへも流用可能に保つ。

namespace EscapeNine.Core
{
    /// <summary>ゲーム進行状態。Swift: GameStatus</summary>
    public enum GameStatus
    {
        Idle,    // ゲーム開始前・リセット後
        Playing,
        Paused,
        Win,
        Lose
    }

    /// <summary>AI 難易度。Swift: AILevel (rawValue "Easy"/"Normal"/"Hard"/"Boss")</summary>
    public enum AILevel
    {
        Easy,
        Normal,
        Hard,
        Boss
    }

    /// <summary>敗因。Swift: DefeatReason</summary>
    public enum DefeatReason
    {
        CaughtByEnemy, // 敵に捕まった
        TimeOut        // 時間切れ（移動しなかった）
    }

    /// <summary>階層の特殊ルール。Swift: SpecialRule</summary>
    public enum SpecialRule
    {
        None,
        Fog,
        Disappear,
        FogDisappear
    }

    /// <summary>プレイアブルキャラ種別。Swift: CharacterType</summary>
    public enum CharacterType
    {
        Hero,
        Thief,
        Wizard,
        Elf,
        Knight
    }

    /// <summary>スキル種別。Swift: SkillType</summary>
    public enum SkillType
    {
        Dash,
        Diagonal,
        Invisible,
        Bind,
        Shield
    }

    /// <summary>Swift の rawValue 文字列と相互変換するためのヘルパー (ランキング/保存の互換維持)。</summary>
    public static class EnumRawValues
    {
        public static string RawValue(this AILevel level) => level switch
        {
            AILevel.Easy => "Easy",
            AILevel.Normal => "Normal",
            AILevel.Hard => "Hard",
            AILevel.Boss => "Boss",
            _ => "Normal"
        };

        public static string RawValue(this CharacterType type) => type switch
        {
            CharacterType.Hero => "hero",
            CharacterType.Thief => "thief",
            CharacterType.Wizard => "wizard",
            CharacterType.Elf => "elf",
            CharacterType.Knight => "knight",
            _ => "hero"
        };

        public static string RawValue(this SkillType type) => type switch
        {
            SkillType.Dash => "dash",
            SkillType.Diagonal => "diagonal",
            SkillType.Invisible => "invisible",
            SkillType.Bind => "bind",
            SkillType.Shield => "shield",
            _ => "dash"
        };
    }
}
