// Skill.cs
// Swift 正本からの忠実移植: Models/Skill.swift

namespace EscapeNine.Core
{
    public static class SkillTypeInfo
    {
        public static string Name(this SkillType type) => type switch
        {
            SkillType.Dash => "ダッシュ",
            SkillType.Diagonal => "斜め移動",
            SkillType.Invisible => "透明化",
            SkillType.Bind => "拘束",
            SkillType.Shield => "盾ガード",
            _ => ""
        };

        public static string Description(this SkillType type) => type switch
        {
            SkillType.Dash => "2マス移動できる",
            SkillType.Diagonal => "斜め方向に移動可能",
            SkillType.Invisible => "鬼に当たっても無敵",
            SkillType.Bind => $"鬼を{GameConfig.BindDurationTurns}ターン停止させる",
            SkillType.Shield => "次の衝突を1回無効化する",
            _ => ""
        };
    }

    /// <summary>スキル定義。Swift: struct Skill</summary>
    public readonly struct Skill
    {
        public readonly SkillType Type;
        public readonly string Name;
        public readonly string Description;
        public readonly int MaxUsage;

        public Skill(SkillType type, string name, string description, int maxUsage)
        {
            Type = type;
            Name = name;
            Description = description;
            MaxUsage = maxUsage;
        }
    }
}
