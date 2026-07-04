// BeatVolumePulse.cs
// Wave 3 (a): Conductor.SongPositionBeats (dspTime 由来) の拍内位相で
// StagePostFx の Bloom.intensity / Vignette.intensity を脈動させる。
//
// ★ Time.time / Time.deltaTime は拍位相の計算に使わない (dspTime 駆動固定。BeatPulse.cs と同じ流儀)。
//   Time.unscaledDeltaTime は「非再生時 / Reduce Motion 時に基準値へ戻る」補間の速度にのみ使う。
//
// 脈動カーブは UI/Fx/BeatPulse.cs と同一の式 ((1-frac)^2: 拍の頭で最大→次拍にかけて急減衰) を踏襲する。
// Conductor に IsPlaying 相当の public プロパティが無いため、BeatPulse.cs / BeatIndicatorWidget と
// 同じ「SongPositionBeats > 0」を再生中の代用ガードとして使う (未再生時は 0 を返す仕様のため)。

using System;
using UnityEngine;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.Stage
{
    public sealed class BeatVolumePulse : MonoBehaviour
    {
        [Tooltip("Bloom.intensity に掛ける拍頭ピーク時の倍率係数 (design 指定: 0.35)。")]
        public float bloomPulseFactor = 0.35f;

        [Tooltip("Vignette.intensity に掛ける拍頭ピーク時の倍率係数 (design 指定: 0.15)。")]
        public float vignettePulseFactor = 0.15f;

        private Conductor _conductor;
        private StagePostFx _postFx;

        private float _bloomCurrent;
        private float _vignetteCurrent;
        private bool _hasCurrent;

        /// <summary>Conductor / StagePostFx への参照を受け取る (design 指定: 「Configure で受け取る」)。</summary>
        public void Configure(Conductor conductor, StagePostFx postFx)
        {
            _conductor = conductor;
            _postFx = postFx;
        }

        private void Update()
        {
            if (_postFx == null || _postFx.Bloom == null || _postFx.Vignette == null) return;

            // Wave 5: 品質ティアの Bloom 強度倍率 (StagePostFx.ApplyQuality が設定)。本メソッドが
            // 毎フレーム BloomIntensityBase から書き直すため、倍率は基準値の方へ掛けて反映する
            // (ApplyQuality が intensity へ直接書き込んでも本メソッドに上書きされてしまうため)。
            float bloomBase = StagePostFx.BloomIntensityBase * _postFx.BloomIntensityScale;

            float targetBloom = bloomBase;
            float targetVignette = StagePostFx.VignetteIntensityBase;

            // BeatPulse.cs と同じ代用ガード (Conductor に IsPlaying 相当が無いため)。
            double beats = _conductor != null ? _conductor.SongPositionBeats : 0.0;
            bool playing = _conductor != null && beats > 0.0;

            if (FxKit.MotionEnabled && playing)
            {
                double frac = beats - Math.Floor(beats); // 拍内位相 [0,1)
                float pulse = (float)(1.0 - frac);
                pulse *= pulse; // BeatPulse.cs と同一の減衰カーブ (拍の頭で最大→急減衰)

                targetBloom = bloomBase * (1f + bloomPulseFactor * pulse);
                targetVignette = StagePostFx.VignetteIntensityBase * (1f + vignettePulseFactor * pulse);

                _bloomCurrent = targetBloom;
                _vignetteCurrent = targetVignette;
            }
            else
            {
                // 非再生時 / Reduce Motion 時は基準値へ滑らかに復帰 (BeatPulse.SettleToBase と同じ思想)。
                float k = Time.unscaledDeltaTime * FxKit.SettleLerpSpeed;
                float fromBloom = _hasCurrent ? _bloomCurrent : targetBloom;
                float fromVignette = _hasCurrent ? _vignetteCurrent : targetVignette;
                _bloomCurrent = Mathf.Lerp(fromBloom, targetBloom, k);
                _vignetteCurrent = Mathf.Lerp(fromVignette, targetVignette, k);
            }

            _hasCurrent = true;
            _postFx.Bloom.intensity.value = _bloomCurrent;
            _postFx.Vignette.intensity.value = _vignetteCurrent;
        }
    }
}
