// RelicDefinition.cs
// Unity Phase 5「ローグライク深化」設計文書 §2.2 (弱点タグ) / §2.3 (レリック一覧) / §6.1 に基づく。
// Swift正本には存在しない (Unity固有の追加機能)。

using System;

namespace EscapeNine.Core
{
    /// <summary>レリックのレアリティ。§2.2 の基準出現率と対応。</summary>
    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>弱点タグ。§2.2。1つのレリックが複数タグを持ちうるため [Flags]。</summary>
    [Flags]
    public enum RelicTag
    {
        None = 0,
        ThiefRescue = 1 << 0,   // 盗賊救済
        HardAICounter = 1 << 1, // Hard AI対抗
        LateGame = 1 << 2,      // 終盤対策 (階層41+/61+)
        General = 1 << 3,       // 汎用強化
        Safety = 1 << 4,        // セーフティネット
        Score = 1 << 5          // スコア/コンボ系
    }

    /// <summary>
    /// レリックの静的データ。Character.GetCharacter(type) と同じ「静的ファクトリ」の作法に合わせ、
    /// RelicCatalog 側で readonly struct のインスタンスを定数的に構築する。
    /// </summary>
    public readonly struct RelicDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly RelicRarity Rarity;
        public readonly RelicTag Tags;

        /// <summary>同一ラン内で所持できる最大個数。1 = スタック不可 (原則「1ラン中に同じレリックは1回まで」)。</summary>
        public readonly int StackLimit;

        private readonly Action<RelicEffects> _applyDelta;

        public RelicDefinition(
            string id,
            string name,
            string description,
            RelicRarity rarity,
            RelicTag tags,
            int stackLimit,
            Action<RelicEffects> applyDelta)
        {
            Id = id;
            Name = name;
            Description = description;
            Rarity = rarity;
            Tags = tags;
            StackLimit = stackLimit;
            _applyDelta = applyDelta;
        }

        /// <summary>
        /// このレリック1個分の効果を effects に加算適用する。
        /// スタック可レリックを複数所持している場合は、所持数分だけ呼び出し側が繰り返し呼ぶ。
        /// </summary>
        public void ApplyTo(RelicEffects effects) => _applyDelta?.Invoke(effects);
    }
}
