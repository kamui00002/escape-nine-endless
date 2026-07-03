// BeatPulse.cs
// Phase 4 (juice): BeatIndicatorWidget.cs のコメントで Phase 4 送りとされていた
// 「拍パルス (scale 1.5 → 1.0 の spring)」を汎用コンポーネントとして提供する。
// 同一 GameObject の RectTransform (必須) / Graphic (任意) を、
// Conductor.SongPositionBeats の小数部 (拍内位相) に合わせて脈動させる。
//
// 拍クロックは Conductor の dspTime 駆動 (Time.deltaTime 非依存) を単一の真実とするため、
// 本コンポーネントも Update() 内で Conductor から都度読み出すだけで、独自のタイマーは持たない。
// Conductor に IsPlaying 相当の public プロパティが無いため、BeatIndicatorWidget.Update() と
// 同じ「SongPositionBeats > 0」を代用ガードとして踏襲する (未再生時は 0 を返す仕様のため)。

using System;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI.Fx
{
    public sealed class BeatPulse : MonoBehaviour
    {
        [Tooltip("拍の頭で加算されるスケール量 (例 0.04 = +4%)。")]
        public float scaleAmount = 0.04f;

        [Tooltip("Graphic があるとき、拍の頭で加算されるアルファ量 (例 0.15)。")]
        public float alphaAmount = 0.15f;

        [Tooltip("true: Conductor 再生中のみ脈動し、非再生時/Reduce Motion 時は基準値へ滑らかに戻る。")]
        public bool onlyWhilePlaying = true;

        private RectTransform _rt;
        private Graphic _graphic;
        private Vector3 _baseScale = Vector3.one;
        private float _baseAlpha = 1f;
        private bool _hasBase;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _graphic = GetComponent<Graphic>(); // Image/Text 等。無ければスケールのみ脈動
        }

        private void OnEnable()
        {
            CaptureBaseIfNeeded();
        }

        private void CaptureBaseIfNeeded()
        {
            if (_hasBase) return;
            if (_rt != null) _baseScale = _rt.localScale;
            if (_graphic != null) _baseAlpha = _graphic.color.a;
            _hasBase = true;
        }

        private void Update()
        {
            if (_rt == null) return;
            CaptureBaseIfNeeded();

            var conductor = App.I != null ? App.I.Conductor : null;
            double beats = conductor != null ? conductor.SongPositionBeats : 0.0;
            bool playing = conductor != null && beats > 0.0; // BeatIndicatorWidget と同じ代用ガード

            if (!FxKit.MotionEnabled || (onlyWhilePlaying && !playing))
            {
                SettleToBase();
                return;
            }

            double frac = beats - Math.Floor(beats); // 拍内位相 [0,1)
            float pulse = (float)(1.0 - frac);
            pulse *= pulse; // 拍の頭で最大、次拍に向けて急速に減衰

            _rt.localScale = _baseScale * (1f + scaleAmount * pulse);
            ApplyAlpha(Mathf.Clamp01(_baseAlpha + alphaAmount * pulse));
        }

        /// <summary>非再生時 / Reduce Motion 時に基準値へ滑らかに戻す。</summary>
        private void SettleToBase()
        {
            float k = Time.unscaledDeltaTime * FxKit.SettleLerpSpeed;
            _rt.localScale = Vector3.Lerp(_rt.localScale, _baseScale, k);
            if (_graphic != null)
            {
                ApplyAlpha(Mathf.Lerp(_graphic.color.a, _baseAlpha, k));
            }
        }

        private void ApplyAlpha(float alpha)
        {
            if (_graphic == null) return;
            Color c = _graphic.color;
            _graphic.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
