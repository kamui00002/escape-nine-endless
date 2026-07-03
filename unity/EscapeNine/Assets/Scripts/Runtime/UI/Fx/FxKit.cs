// FxKit.cs
// Phase 4 (juice) の演出基盤。カメラシェイク禁止 (Overlay Canvas はカメラに反応しない) のため、
// 全て対象 RectTransform の anchoredPosition / localScale、または Graphic.color を直接動かす。
// 外部 Tween ライブラリ (DOTween 等) は使わず、コルーチンによる自前実装のみで完結させる。
//
// ★ Reduce Motion: 全関数が FxKit.MotionEnabled (= !PlayerState.ReduceMotionEnabled) を先頭で
//   チェックし、false の場合は演出をスキップして即終値 (= 呼び出し時点で既に最終形なので無処理)
//   を返す。呼び出し側は常に同じ形で呼べばよく、Reduce Motion 対応を個別に書く必要がない。
//
// ★ 時間基準: Time.deltaTime ではなく Time.unscaledDeltaTime を使う。
//   HitStop() が Time.timeScale を一時的に落とすため、Scaled 時間で動くと
//   ヒットストップ中に演出まで止まってしまい「体感が止まる」効果自体が破綻する。

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI.Fx
{
    public static class FxKit
    {
        /// <summary>HitStop の契約上限 (共通ルール: timeScale ヒットストップは 0.1 秒以下)。</summary>
        private const float MaxHitStopDuration = 0.1f;

        /// <summary>「基準へ戻る」系ループの追従速度 (BeatPulse 等が共用)。</summary>
        public const float SettleLerpSpeed = 10f;

        // ---- HitStop 用の共有状態 (アプリ全体で 1 系統のみ。多重呼び出しは最長を採用) ----
        private static Coroutine _hitStopRoutine;
        private static float _hitStopEndUnscaled = -1f;
        private static float _hitStopOriginalTimeScale = 1f;

        /// <summary>
        /// Reduce Motion の逆。App.I.Player.ReduceMotionEnabled が未初期化 (App 未生成) の間は
        /// 安全側 (演出あり = true) にフォールバックする。
        /// </summary>
        public static bool MotionEnabled
        {
            get
            {
                var player = App.I != null ? App.I.Player : null;
                return player == null || !player.ReduceMotionEnabled;
            }
        }

        // ---- Punch Scale ----

        /// <summary>
        /// 対象を一瞬だけ拡大してから元のスケールへ戻す「パンチ」演出。
        /// 例: ボタン押下・スコア加点・スキル発動時の強調。
        /// </summary>
        public static Coroutine PunchScale(MonoBehaviour host, RectTransform rt, float punch = 0.15f, float duration = 0.25f)
        {
            if (host == null || rt == null || duration <= 0f) return null;
            if (!MotionEnabled) return null; // 即終値: 呼び出し前後でスケール変化なし

            return host.StartCoroutine(PunchScaleRoutine(rt, punch, duration));
        }

        private static IEnumerator PunchScaleRoutine(RectTransform rt, float punch, float duration)
        {
            Vector3 baseScale = rt.localScale;
            Vector3 peakScale = baseScale * (1f + Mathf.Max(punch, 0f));
            float half = Mathf.Max(duration * 0.5f, 0.0001f);

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.LerpUnclamped(baseScale, peakScale, EaseOutQuad(Mathf.Clamp01(t / half)));
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.LerpUnclamped(peakScale, baseScale, EaseOutBack(Mathf.Clamp01(t / half)));
                yield return null;
            }

            rt.localScale = baseScale;
        }

        // ---- Shake Rect ----

        /// <summary>
        /// 対象 RectTransform の anchoredPosition を減衰振動させる。
        /// カメラシェイク禁止の代替 (Overlay Canvas 対応)。
        /// </summary>
        public static Coroutine ShakeRect(MonoBehaviour host, RectTransform rt, float amplitude = 12f, float duration = 0.3f)
        {
            if (host == null || rt == null || duration <= 0f) return null;
            if (!MotionEnabled) return null; // 即終値: 位置変化なし

            return host.StartCoroutine(ShakeRectRoutine(rt, amplitude, duration));
        }

        private static IEnumerator ShakeRectRoutine(RectTransform rt, float amplitude, float duration)
        {
            Vector2 basePos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(t / duration);
                float ox = Random.Range(-1f, 1f) * amplitude * decay;
                float oy = Random.Range(-1f, 1f) * amplitude * decay;
                rt.anchoredPosition = basePos + new Vector2(ox, oy);
                yield return null;
            }
            rt.anchoredPosition = basePos;
        }

        // ---- Flash ----

        /// <summary>
        /// Graphic (Image/Text) を指定色へ瞬間的に切り替え、元の色へフェードで戻す。
        /// 例: 衝突時の赤フラッシュ、成功時の金フラッシュ。
        /// </summary>
        public static Coroutine Flash(MonoBehaviour host, Graphic g, Color flashColor, float duration = 0.2f)
        {
            if (host == null || g == null || duration <= 0f) return null;
            if (!MotionEnabled) return null; // 即終値: 色変化なし (元の色のまま)

            return host.StartCoroutine(FlashRoutine(g, flashColor, duration));
        }

        private static IEnumerator FlashRoutine(Graphic g, Color flashColor, float duration)
        {
            Color original = g.color;
            g.color = flashColor;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                g.color = Color.Lerp(flashColor, original, EaseOutQuad(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            g.color = original;
        }

        // ---- Hit Stop ----

        /// <summary>
        /// 一瞬 Time.timeScale を落として「間」を作る演出。共通ルールにより 0.1 秒を超えない。
        /// 多重呼び出しは最長の終了時刻を採用 (実行中のコルーチンが共有終了時刻を毎フレーム参照するため、
        /// 新規コルーチンを積み増さずに延長できる)。
        /// timeScale という「アプリ全体で 1 つ」の状態を扱うため、呼び出し元の host が
        /// 演出中に非活性化されても復元コルーチンが巻き込まれて死なないよう、
        /// FxLayer (常駐シングルトン) が生きていればそちらを実行ホストに使う。
        /// </summary>
        public static Coroutine HitStop(MonoBehaviour host, float duration = 0.07f)
        {
            if (!MotionEnabled) return null; // 即終値: timeScale は通常のまま

            duration = Mathf.Clamp(duration, 0f, MaxHitStopDuration);
            if (duration <= 0f) return null;

            MonoBehaviour runner = FxLayer.I != null ? (MonoBehaviour)FxLayer.I : host;
            if (runner == null) return null;

            float requestedEnd = Time.unscaledTime + duration;

            if (_hitStopRoutine == null)
            {
                _hitStopOriginalTimeScale = Time.timeScale;
                _hitStopEndUnscaled = requestedEnd;
                _hitStopRoutine = runner.StartCoroutine(HitStopRoutine());
            }
            else if (requestedEnd > _hitStopEndUnscaled)
            {
                _hitStopEndUnscaled = requestedEnd; // 実行中コルーチンが延長分をそのまま拾う
            }

            return _hitStopRoutine;
        }

        private static IEnumerator HitStopRoutine()
        {
            Time.timeScale = 0f;
            while (Time.unscaledTime < _hitStopEndUnscaled)
            {
                yield return null;
            }
            Time.timeScale = _hitStopOriginalTimeScale;
            _hitStopRoutine = null;
            _hitStopEndUnscaled = -1f;
        }

        // ---- Slide In ----

        /// <summary>
        /// 対象を fromOffset 分ずらした位置から現在の (呼び出し時点の) anchoredPosition へ
        /// イージング付きで滑り込ませる。呼び出し前にレイアウト確定済みであること前提
        /// (UIFactory.Place 等で最終位置に置いてから呼ぶ)。
        /// </summary>
        public static Coroutine SlideIn(MonoBehaviour host, RectTransform rt, Vector2 fromOffset, float duration = 0.3f)
        {
            if (host == null || rt == null || duration <= 0f) return null;
            if (!MotionEnabled) return null; // 即終値: 現在位置 (= 最終位置) のまま何もしない

            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + fromOffset;
            return host.StartCoroutine(SlideInRoutine(rt, target, duration));
        }

        private static IEnumerator SlideInRoutine(RectTransform rt, Vector2 target, float duration)
        {
            Vector2 start = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                rt.anchoredPosition = Vector2.LerpUnclamped(start, target, EaseOutBack(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        // ---- Easing (手書き。DOTween 等の外部依存を避けるため最小限を自前実装) ----

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
