// StageQuality.cs
// Wave 5 (Phase 4.5): 品質ティア。docs/unity-phase4-5-visual-upgrade-design.md §5 の性能予算
// (iOS 実機 GPU ≤12ms / Bloom は iOS で Low+ダウンサンプル2x / DoF はモバイル全面禁止) に対する
// 実行時の逃げ道。3 段階 (High/Medium/Low) で Bloom 強度・パーティクル数・RT フォーマットを
// 一括制御する。
//
// Depth of Field はどのティアでも一切使わない (D7: モバイル全面オフ)。デスクトップ含め本設計では
// 追加しない — §5 の降格順序で「DoF → Chromatic Aberration → パーティクル密度 → Bloom 解像度」の
// 先頭に挙げられている項目であり、最後まで残す BeatVolumePulse の脈動と優先順位が逆なため、
// ティア分岐を作らず常時オフのまま据え置く (StagePostFx.cs も同じ結論を Setup() コメントに記載済み)。
//
// 既定値は Application.isMobilePlatform ? Medium : High (design 指定)。
// SettingsScreen の「演出設定」カードの「演出品質」セグメントから PlayerState.StageQualityTier
// 経由で上書きできる。

using UnityEngine;

namespace EscapeNine.Runtime.Stage
{
    /// <summary>品質ティア。PlayerState.StageQualityTier に永続化される (int ordinal)。</summary>
    public enum StageQualityTier
    {
        High,
        Medium,
        Low,
    }

    public static class StageQuality
    {
        /// <summary>Medium ティアの Bloom 強度倍率 (design 指定: 0.6 倍)。</summary>
        private const float MediumBloomIntensityScale = 0.6f;

        private const int ParticlesHigh = 40;   // StageParticles の従来既定値と同一
        private const int ParticlesMedium = 24;
        private const int ParticlesLow = 0;

        /// <summary>プラットフォーム既定値 (design 指定)。PlayerState 未設定時のフォールバックにも使う。</summary>
        public static StageQualityTier PlatformDefault =>
            Application.isMobilePlatform ? StageQualityTier.Medium : StageQualityTier.High;

        /// <summary>Low のみ Bloom を完全無効化する。</summary>
        public static bool BloomEnabled(StageQualityTier tier) => tier != StageQualityTier.Low;

        /// <summary>
        /// Bloom 強度倍率。BeatVolumePulse.Update() が毎フレーム BloomIntensityBase から
        /// 強度を再計算するため、この倍率は StagePostFx.BloomIntensityScale 経由でその計算式へ
        /// 掛け合わされる (直接 intensity へ書き込んでも次フレームで上書きされてしまうため)。
        /// </summary>
        public static float BloomIntensityScale(StageQualityTier tier) =>
            tier == StageQualityTier.Medium ? MediumBloomIntensityScale : 1f;

        public static int MaxParticles(StageQualityTier tier) => tier switch
        {
            StageQualityTier.High => ParticlesHigh,
            StageQualityTier.Medium => ParticlesMedium,
            _ => ParticlesLow,
        };

        /// <summary>Low のみ LDR (RenderTextureFormat.Default)。High/Medium は HDR (Bloom の閾値超え発光を保持)。</summary>
        public static RenderTextureFormat RtFormat(StageQualityTier tier) =>
            tier == StageQualityTier.Low ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;

        /// <summary>
        /// 品質ティアを舞台一式へ一括適用する。GameScreen.OnShow (画面表示のたび、設定変更を
        /// 次回表示から反映) から呼ばれる想定。各引数は null 許容
        /// (StagePostFx/StageParticles/StageRenderView が劣化環境で欠けているケースは何もしない)。
        /// </summary>
        public static void Apply(StageQualityTier tier, StagePostFx postFx, StageParticles particles,
            StageRenderView renderView)
        {
            postFx?.ApplyQuality(BloomEnabled(tier), BloomIntensityScale(tier));
            particles?.ApplyQuality(MaxParticles(tier));
            renderView?.SetFormat(RtFormat(tier));
        }
    }
}
