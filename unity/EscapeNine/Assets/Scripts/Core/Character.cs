// Character.cs
// Swift 正本からの忠実移植: Models/Character.swift

namespace EscapeNine.Core
{
    public static class CharacterTypeInfo
    {
        public static string Name(this CharacterType type) => type switch
        {
            CharacterType.Hero => "勇者",
            CharacterType.Thief => "盗賊",
            CharacterType.Wizard => "魔法使い",
            CharacterType.Elf => "エルフ",
            CharacterType.Knight => "ナイト",
            _ => ""
        };

        public static bool IsFree(this CharacterType type) => type switch
        {
            CharacterType.Hero => true,
            CharacterType.Thief => true,
            _ => false // wizard / elf / knight は有料
        };

        /// <summary>価格 (無料キャラは null)。Swift: CharacterType.price</summary>
        public static int? Price(this CharacterType type) =>
            type.IsFree() ? (int?)null : GameConfig.PremiumCharacterPrice;
    }

    /// <summary>キャラクター定義。Swift: struct Character</summary>
    public readonly struct Character
    {
        public readonly string Id;
        public readonly CharacterType Type;
        public readonly string Name;
        public readonly Skill Skill;
        public readonly bool IsFree;
        public readonly int? Price;
        public readonly string SpriteName;

        public Character(string id, CharacterType type, string name, Skill skill, bool isFree, int? price, string spriteName)
        {
            Id = id;
            Type = type;
            Name = name;
            Skill = skill;
            IsFree = isFree;
            Price = price;
            SpriteName = spriteName;
        }

        /// <summary>種別からキャラを生成。Swift: Character.getCharacter(for:)</summary>
        public static Character GetCharacter(CharacterType type)
        {
            Skill skill;
            switch (type)
            {
                case CharacterType.Hero:
                    skill = new Skill(SkillType.Dash, "ダッシュ", "2マス移動できる", GameConfig.HeroSkillMaxUsage);
                    break;
                case CharacterType.Thief:
                    skill = new Skill(SkillType.Diagonal, "斜め移動", "斜め方向に移動可能", GameConfig.ThiefSkillMaxUsage);
                    break;
                case CharacterType.Wizard:
                    skill = new Skill(SkillType.Invisible, "透明化", "鬼に当たっても無敵", GameConfig.WizardSkillMaxUsage);
                    break;
                case CharacterType.Elf:
                    skill = new Skill(SkillType.Bind, "拘束", $"鬼を{GameConfig.BindDurationTurns}ターン停止させる", GameConfig.ElfSkillMaxUsage);
                    break;
                case CharacterType.Knight:
                    skill = new Skill(SkillType.Shield, "盾ガード", "次の衝突を1回無効化する", GameConfig.KnightSkillMaxUsage);
                    break;
                default:
                    skill = new Skill(SkillType.Dash, "ダッシュ", "2マス移動できる", GameConfig.HeroSkillMaxUsage);
                    break;
            }

            return new Character(
                id: type.RawValue(),
                type: type,
                name: type.Name(),
                skill: skill,
                isFree: type.IsFree(),
                price: type.Price(),
                spriteName: type.RawValue()
            );
        }

        public static Character DefaultCharacter => GetCharacter(CharacterType.Hero);
    }
}
