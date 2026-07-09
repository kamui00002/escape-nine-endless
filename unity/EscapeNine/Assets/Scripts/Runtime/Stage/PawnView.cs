// PawnView.cs
// Wave 2 (3D BoardStage): プレイヤー / 鬼のワールド表現。
// 旧 uGUI 盤面 GridBoardWidget.cs (W5 で削除済み。以下の言及は移植元の記録) の
// オーバーレイ Image (Player/EnemySprite) に相当するが、
// ワールド空間ではセルの子ではなく SpriteRenderer によるビルボードとして表現する
// (design 指定: 毎 LateUpdate でカメラ方向へ Y 軸のみ回転)。
//
// 移動時の PunchScale (パンチ) と衝突演出の Flash は、FxKit.cs (Scripts/Runtime/UI/Fx/)
// が RectTransform/Graphic 専用のため直接は使えず、同じ形状 (イージング・秒数) を
// Transform.localScale / SpriteRenderer.color 向けにここで再実装する
// (FxKit.cs 自体は改造しない — 既存呼び出し元への影響を避けるため)。
// Reduce Motion 判定は FxKit.MotionEnabled をそのまま参照するので、対応漏れはない。

using System.Collections;
using UnityEngine;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.Stage
{
    public sealed class PawnView : MonoBehaviour
    {
        /// <summary>タイル上面からの浮遊高さ (World 単位)。</summary>
        public const float HoverHeight = 0.6f;

        // 旧 GridBoardWidget.MoveHopPunch/MoveHopDuration から引き継いだ値 (W5 の削除後はここが唯一の定義)。
        private const float MoveHopPunch = 0.18f;
        private const float MoveHopDuration = 0.18f;

        // 2026-07-09 オーナー「ボスも普通の鬼のまま」→ 演出でボス化を試みたが、鬼スプライトは元々背が高く
        // 盤面の縦フレームがキツいため、1.42→1.15 に下げても実機で上端をはみ出した (オーナー報告)。
        // スケール拡大での差別化は断念し 1.0 (通常サイズ) に戻す。ボス差別化は専用ボススプライト (AI生成) と
        // 既存のボス威圧テレグラフ (床の赤明滅) に委ねる。SetBossPresence の配線は将来の別演出用に残置。
        private const float BossScaleMul = 1.0f;

        private SpriteRenderer _renderer;
        private Coroutine _flashRoutine;
        private Coroutine _punchRoutine;
        // 基準スケール倍率 (通常1.0 / ボス階でBossScaleMul)。PunchHop/ResetFx はこれを土台に合成する。
        private float _baseScaleMul = 1f;

        public static PawnView Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var pawn = go.AddComponent<PawnView>();
            pawn._renderer = go.AddComponent<SpriteRenderer>();
            pawn._renderer.enabled = false;

            // 鬼タップ判定用 (StageInput が参照する。プレイヤー側は未使用だが害はない)。
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.6f, 0.6f, 0.2f);
            collider.isTrigger = true;

            return pawn;
        }

        public BoxCollider Collider => GetComponent<BoxCollider>();

        /// <summary>スプライトと表示可否を設定する (GridBoardWidget: Image.sprite / Image.enabled 相当)。</summary>
        public void Render(Sprite sprite, bool visible)
        {
            _renderer.sprite = sprite;
            _renderer.enabled = sprite != null && visible;
        }

        /// <summary>接地面 (x, z) 座標を設定し、浮遊高さを一定に保つ。</summary>
        public void SetGroundPosition(Vector3 groundXZ)
        {
            transform.localPosition = new Vector3(groundXZ.x, HoverHeight, groundXZ.z);
        }

        /// <summary>ボス階の鬼を拡大表示して威圧感を出す (BoardStage.Render が毎回 session.IsBossFloor で駆動)。
        /// PunchHop 実行中は上書きせず、_baseScaleMul のみ更新して次の非パンチ時に反映する
        /// (パンチアニメを途中で潰さないため)。</summary>
        public void SetBossPresence(bool isBoss)
        {
            float target = isBoss ? BossScaleMul : 1f;
            if (Mathf.Approximately(target, _baseScaleMul)) return;
            _baseScaleMul = target;
            if (_punchRoutine == null) transform.localScale = Vector3.one * _baseScaleMul;
        }

        /// <summary>移動ホップ (squash&amp;stretch 風の強調)。GridBoardWidget の PunchScale 呼び出しと同タイミング。</summary>
        public void PunchHop()
        {
            if (!FxKit.MotionEnabled) return;
            if (_punchRoutine != null) StopCoroutine(_punchRoutine);
            _punchRoutine = StartCoroutine(PunchRoutine(MoveHopPunch, MoveHopDuration));
        }

        private IEnumerator PunchRoutine(float punch, float duration)
        {
            Vector3 baseScale = Vector3.one * _baseScaleMul; // ボス拡大を土台に合成 (現在値の残留を拾わない)
            Vector3 peak = baseScale * (1f + Mathf.Max(punch, 0f));
            float half = Mathf.Max(duration * 0.5f, 0.0001f);

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.LerpUnclamped(baseScale, peak, EaseOutQuad(Mathf.Clamp01(t / half)));
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.LerpUnclamped(peak, baseScale, EaseOutBack(Mathf.Clamp01(t / half)));
                yield return null;
            }
            transform.localScale = baseScale;
        }

        /// <summary>瞬間的に指定色へフラッシュしてから元の色 (白) へ戻る (GridBoardWidget.FlashPlayer 相当)。</summary>
        public void Flash(Color color, float duration)
        {
            if (!FxKit.MotionEnabled) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color flashColor, float duration)
        {
            Color original = _renderer.color;
            _renderer.color = flashColor;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _renderer.color = Color.Lerp(flashColor, original, EaseOutQuad(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            _renderer.color = original;
        }

        /// <summary>FxKit コルーチンの中断残留を基準値へ戻す (GridBoardWidget.ResetFxState 相当)。</summary>
        public void ResetFx()
        {
            if (_punchRoutine != null) { StopCoroutine(_punchRoutine); _punchRoutine = null; }
            if (_flashRoutine != null) { StopCoroutine(_flashRoutine); _flashRoutine = null; }
            _baseScaleMul = 1f; // 新ラン/リセット時は素の大きさへ (次の BoardStage.Render がボス階なら再拡大)
            transform.localScale = Vector3.one;
            if (_renderer != null) _renderer.color = Color.white;
        }

        /// <summary>ビルボード: Y 軸のみカメラへ向ける (design 指定)。</summary>
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f) return;

            // SpriteRenderer の正面は -Z 向き (2D 標準配置) のため、+Z をカメラの反対方向へ向ける。
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }

        // ---- Easing (FxKit.cs と同一の式を意図的に複製。FxKit.cs 自体は改造しない) ----

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
