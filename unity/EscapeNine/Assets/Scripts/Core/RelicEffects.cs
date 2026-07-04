// RelicEffects.cs
// Unity Phase 5「ローグライク深化」設計文書 (docs/unity-phase5-roguelike-design.md) §1原則2・§2.4・§6.1 に基づく
// 新規機構。Swift正本には存在しない (Unity固有の追加機能)。
//
// 加算オーバーレイの POCO: GameConfig の既存数値そのものは変更せず、
// このオブジェクトの値を判定式に上乗せする形で GameSession に統合する (原則2)。
//
// 設計判断: readonly struct ではなく mutable な sealed class にした理由 ―
//   ReviveCharges / GenericShieldCharges / PseudoBindCharges / ComboMissShieldCharges は
//   「ラン中に消費されるチャージ」であり、GameSession が `Relics.ReviveCharges--` のように
//   参照先を直接デクリメントする設計 (§2.4)。struct だと `GameSession.Relics` が
//   auto-property の場合に呼び出し側でコピーしか書き換えられず (CS1612)、意図した
//   「同一ランを通じて共有されるチャージの消費」が成立しない。よって GameStateData.cs と
//   同様の「public フィールドを持つ sealed class」の作法に合わせた。
//
// `None` は毎回 new インスタンスを返す static プロパティ (シングルトンではない)。
// 複数の GameSession / テストケースが同時に "レリックなし" 状態を持つ際、
// チャージ系フィールドを誤って共有ミューテートしてしまう事故を構造的に防ぐため。
namespace EscapeNine.Core
{
    public sealed class RelicEffects
    {
        // --- §2.4 GameSession 統合フック (設計書の表と1:1対応) ---

        /// <summary>MaxTurns から減算するターン数 (最低3にクランプ)。#4 老練の構え。</summary>
        public int TurnCountReduction;

        /// <summary>Skill.MaxUsage に加算するボーナス使用回数。#13 予備の呼吸 (5aカタログ外)。</summary>
        public int SkillMaxUsageBonus;

        /// <summary>マス消失の発生数から減算 (0未満は0)。#7 地固めの護符。</summary>
        public int DisappearCellReduction;

        /// <summary>霧の視界半径 (Chebyshev) に加算。#8 灯火の指輪 (スタック可)。</summary>
        public int FogVisibilityRadiusBonus;

        /// <summary>true の場合、実効AIがHardのときの1回の敵AI呼び出しをNormal相当に格下げする。
        /// #9 幻惑の粉 (5aカタログ外だが§2.4のフック仕様として実装)。</summary>
        public bool NeutralizeHardPrediction;

        /// <summary>衝突による敗北を無効化して継続できる残り回数。#5 不死鳥の残り火 (5aカタログ外)。</summary>
        public int ReviveCharges;

        /// <summary>盾スキルを持たないキャラでも衝突を1回無効化できる即席シールドの残り回数。#12 二段構えの盾。</summary>
        public int GenericShieldCharges;

        /// <summary>1階層につき消失マスへの進入による敗北を無効化できる回数 (階層開始時にこの値へ再チャージ)。
        /// #11 影の抜け道 (5aカタログ外だが§2.4のフック仕様として実装)。</summary>
        public int DisappearForgivenessPerFloor;

        /// <summary>Miss判定でもコンボを維持できる残り回数。#6 コンボの守り (5aカタログ外、Tier2)。</summary>
        public int ComboMissShieldCharges;

        /// <summary>コンボ倍率のしきい値 (3/5) から減算する値。#14 連鎖の証 (5aカタログ外)。</summary>
        public int ComboThresholdReduction;

        // --- 5aカタログの一部レリックが §2.4 の10フックだけでは表現できないため追加したフィールド ---
        // (盗賊救済・護りの起点・心話の絆は固有の状態を要求するため、上記の汎用フックに無理に
        //  押し込めず素直に専用フィールドを追加した。値=0/falseなら既存挙動と完全に一致する)

        /// <summary>盗賊の斜め移動発動時に、この確率でスキル残数を消費しない (0〜1)。#1 影の軽業。</summary>
        public double ThiefDiagonalSkillSaveChance;

        /// <summary>盗賊の斜め移動を使ったターンの敵移動をEasy相当に強制するか。#2 残像のヴェール。</summary>
        public bool ThiefResidualVeil;

        /// <summary>階層開始時にプレイヤーと敵の初期配置に保証する最小Chebyshev距離 (0=保証なし=既存挙動)。#10 護りの起点。</summary>
        public int MinStartDistance;

        /// <summary>拘束スキルを持たないキャラでも敵を拘束できる残り回数。#17 心話の絆。</summary>
        public int PseudoBindCharges;

        /// <summary>ゼロ効果 (レリック未装備) を表す新規インスタンス。
        /// 呼び出す度に new する (チャージ系フィールドの共有ミューテート事故を防ぐため、意図的にシングルトンにしない)。</summary>
        public static RelicEffects None => new RelicEffects();
    }
}
