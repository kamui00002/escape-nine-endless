// FxLayer.cs
// Phase 4 (juice) の破片バーストレイヤー。ParticleSystem は Overlay Canvas との描画順が
// 壊れるため禁止 (共通ルール) — 代わりに uGUI Image の破片をあらかじめプールしておき、
// 放射速度 + 重力 + 回転 + フェードで飛散させる「疑似パーティクル」を自前実装する。
//
// Canvas/ScreenRoot の最後の子として常駐させることで、どの画面がアクティブでも
// (画面は SetActive の切替のみで破棄されないため) 常に最前面に描画される。

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI.Fx
{
    public sealed class FxLayer : MonoBehaviour
    {
        public static FxLayer I { get; private set; }

        /// <summary>プールする破片の数 (固定契約の目安「32 個程度」)。</summary>
        private const int PoolSize = 32;

        /// <summary>破片 1 個の一辺 (参照解像度 px。CanvasScaler が画面比で拡縮するため実質比率単位)。</summary>
        private const float ShardSize = 22f;

        private const float Gravity = 1600f;   // 落下加速度 (参照解像度 px/s^2)
        private const float Lifetime = 0.55f;  // 破片の寿命 (秒、unscaled)

        private RectTransform _root;
        private RectTransform[] _shards;
        private bool[] _busy;
        private int _cursor;

        /// <summary>
        /// screenRoot 配下の最後の子として FxLayer を生成する。二重生成は無視して既存インスタンスを返す。
        /// </summary>
        public static FxLayer Install(RectTransform parent)
        {
            if (I != null) return I;
            if (parent == null)
            {
                Debug.LogError("[FxLayer] parent が null のため生成できない");
                return null;
            }

            var go = new GameObject("FxLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling(); // 画面群より後ろに描画されないよう最前面へ

            return go.AddComponent<FxLayer>();
        }

        private void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;

            _root = (RectTransform)transform;
            _shards = new RectTransform[PoolSize];
            _busy = new bool[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                _shards[i] = CreateShard(i);
            }
        }

        private RectTransform CreateShard(int index)
        {
            var go = new GameObject("FxShard_" + index, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ShardSize, ShardSize);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false; // 破片がタップを吸わないように

            go.SetActive(false);
            return rt;
        }

        /// <summary>対象 RectTransform の中心から破片を放射する。</summary>
        public void BurstAt(RectTransform target, Color color, int count = 12, float speed = 600f)
        {
            if (target == null) return;
            if (!FxKit.MotionEnabled) return; // Reduce Motion: 演出なし (装飾のみのため無処理でよい)

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, target.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPoint, null, out Vector2 local))
            {
                return;
            }
            BurstInternal(local, color, count, speed);
        }

        /// <summary>画面比率座標 (0..1、左下原点) から破片を放射する。</summary>
        public void BurstScreen(Vector2 normalizedPos, Color color, int count = 12, float speed = 600f)
        {
            if (!FxKit.MotionEnabled) return;

            Rect r = _root.rect;
            Vector2 local = new Vector2(
                (normalizedPos.x - 0.5f) * r.width,
                (normalizedPos.y - 0.5f) * r.height);
            BurstInternal(local, color, count, speed);
        }

        private void BurstInternal(Vector2 origin, Color color, int count, float speed)
        {
            if (_shards == null || _shards.Length == 0) return;
            count = Mathf.Clamp(count, 0, _shards.Length);

            int spawned = 0;
            int scanned = 0;
            while (spawned < count && scanned < _shards.Length)
            {
                int i = _cursor;
                _cursor = (_cursor + 1) % _shards.Length;
                scanned++;
                if (_busy[i]) continue; // 使用中の破片は割り込まない (寿命 0.55s なので通常は枯渇しない)

                _busy[i] = true;
                float angle = (spawned / (float)count) * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
                float mag = speed * Random.Range(0.6f, 1f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * mag;
                StartCoroutine(ShardRoutine(_shards[i], i, origin, velocity, color));
                spawned++;
            }
        }

        private IEnumerator ShardRoutine(RectTransform rt, int slot, Vector2 origin, Vector2 velocity, Color color)
        {
            rt.gameObject.SetActive(true);
            rt.anchoredPosition = origin;
            rt.localRotation = Quaternion.identity;

            var img = rt.GetComponent<Image>();
            img.color = color;
            float spinDegPerSec = Random.Range(-720f, 720f);

            Vector2 pos = origin;
            Vector2 vel = velocity;
            float t = 0f;

            while (t < Lifetime)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;

                vel += Vector2.down * Gravity * dt;
                pos += vel * dt;
                rt.anchoredPosition = pos;
                rt.Rotate(0f, 0f, spinDegPerSec * dt);

                float alpha = 1f - Mathf.Clamp01(t / Lifetime);
                img.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            rt.gameObject.SetActive(false);
            _busy[slot] = false;
        }
    }
}
