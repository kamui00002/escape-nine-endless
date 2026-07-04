// RouteChoice.cs
// Unity Phase 5「ローグライク深化」設計文書 (docs/unity-phase5-roguelike-design.md) §4 (分岐ルート) /
// §6.1 に基づく Phase 5c 新規機構。Swift正本には存在しない (Unity固有の追加機能)。
//
// 階層クリア後 (レリックドラフトの直前) に「安全なルート」/「深淵のルート」の2択を提示する。
// §4 は深淵ルートの効果を「実効AIレベルを1段引き上げる（Easy→Normal→Hard、Hardは据え置き）、
// または特殊ルールを1段階前倒しする」と並列に記述している。片方だけを実装すると本文の記述を
// 半分切り捨てることになるため、本実装は「両方を同時に適用する」解釈を採用した
// (§4本文の全要素を反映する読み方。オーナー側で単独適用が意図なら要修正 [要確認])。
//
// レリックドラフトへの確定Rare枠付与・残光ボーナス付与 (§4後半の深淵ルートの報酬) は
// 本ファイル・GameSession統合のスコープ外 (RelicDraftService/MetaProgressionCalculator 側の別タスク)。
// Floor 6以降からの提示開始制限 (§4「頻度」) はRuntime/UI側の関心事のため、Coreでは強制しない。
//
// GameSession.NextFloor(RouteChoice) が「1階層限定」のオーバーライドとして RouteFloorOverride を
// 保持し、次の NextFloor 呼び出しで自動的に (Safe または新しい選択へ) 差し替わる (=クリアされる)。
namespace EscapeNine.Core
{
    /// <summary>階層クリア後に選ぶ分岐ルート。Swift正本には存在しない。</summary>
    public enum RouteChoice
    {
        Safe,
        Abyss
    }

    /// <summary>
    /// 「1階層限定」のルート選択オーバーライドを保持する構造体。
    /// GameSession.NextFloor が生成し、その階層の実効AIレベル/特殊ルール算出にのみ適用する。
    /// Safe を既定値とすることで、Relics.None と同様「未使用時は既存挙動と完全に一致する」を担保する。
    /// </summary>
    public readonly struct RouteFloorOverride
    {
        public readonly RouteChoice Choice;

        private RouteFloorOverride(RouteChoice choice)
        {
            Choice = choice;
        }

        public static readonly RouteFloorOverride Safe = new RouteFloorOverride(RouteChoice.Safe);

        /// <summary>選択値からオーバーライドを生成する。Safe以外は現状Abyssのみ。</summary>
        public static RouteFloorOverride For(RouteChoice choice) =>
            choice == RouteChoice.Abyss ? new RouteFloorOverride(RouteChoice.Abyss) : Safe;

        /// <summary>
        /// 実効AIレベルを1段引き上げる (Easy→Normal→Hard、Hard/Bossは据え置き)。
        /// Safe のときは引数をそのまま返す (既存挙動と完全に一致する)。
        /// </summary>
        public AILevel ApplyToEffectiveAILevel(AILevel effective)
        {
            if (Choice != RouteChoice.Abyss) return effective;
            switch (effective)
            {
                case AILevel.Easy: return AILevel.Normal;
                case AILevel.Normal: return AILevel.Hard;
                case AILevel.Hard: return AILevel.Hard;   // 据え置き (§4)
                case AILevel.Boss: return AILevel.Boss;   // ボスは変更しない (Floor.GetEffectiveAILevelと同じ扱い)
                default: return effective;
            }
        }

        /// <summary>
        /// 特殊ルールを1段階前倒しする (None→Fog→Disappear→FogDisappear)。
        /// Safe のときは引数をそのまま返す (既存挙動と完全に一致する)。
        /// </summary>
        public SpecialRule ApplyToSpecialRule(SpecialRule rule)
        {
            if (Choice != RouteChoice.Abyss) return rule;
            switch (rule)
            {
                case SpecialRule.None: return SpecialRule.Fog;
                case SpecialRule.Fog: return SpecialRule.Disappear;
                case SpecialRule.Disappear: return SpecialRule.FogDisappear;
                case SpecialRule.FogDisappear: return SpecialRule.FogDisappear; // 最大なので据え置き
                default: return rule;
            }
        }
    }
}
