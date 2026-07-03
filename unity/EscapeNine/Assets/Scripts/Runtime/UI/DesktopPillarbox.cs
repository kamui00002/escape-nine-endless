// DesktopPillarbox.cs
// Phase 6a: デスクトップ(Steam体験版)技術基盤。Swift 正本には対応なし (iOS 専用 UI のため) —
// Unity デスクトップ版のみの追加コンポーネント。
//
// 全 UI は UIFactory.Place の親比率レイアウト + CanvasScaler 参照解像度 1170x2532 (縦長スマホ前提)
// で組まれている (MainSceneBuilder.ReferenceResolution)。デスクトップは自由なウィンドウ比率
// (横長ウィンドウ等) で起動できてしまうため、横長時は contentRoot (Canvas/ScreenRoot) を
// 縦長 1170:2532 の中央カラムへ拘束し、左右の余白には一段暗い背景を敷いて
// 「携帯電話を横に置いたような」見た目にする (ピラーボックス)。
//
// モバイル実機 (Application.isMobilePlatform == true) では Update/Awake が早期 return し、
// アタッチされても contentRoot の anchor には一切触れない (無害)。
// Application.isMobilePlatform は Editor では常に false (ビルドターゲットに関わらず) のため、
// Editor 実行中は常にピラーボックスが有効になる。

using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopPillarbox : MonoBehaviour
    {
        /// <summary>参照解像度 (MainSceneBuilder.ReferenceResolution 1170x2532 と対)。</summary>
        private const float ReferenceWidth = 1170f;
        private const float ReferenceHeight = 2532f;

        /// <summary>UITheme.Background を一段暗くする乗算係数 (色相/彩度は保持、明度のみ落とす)。</summary>
        private const float BackdropDarkenFactor = 0.55f;

        private RectTransform _contentRoot;

        // SafeAreaFitter と同じキャッシュ比較方式: 解像度が変化したフレームだけ再計算する。
        private Vector2Int _lastScreenSize = new Vector2Int(-1, -1);

        /// <summary>
        /// contentRoot (通常は Canvas/ScreenRoot) へ本コンポーネントをアタッチし、
        /// その背面 (兄弟の手前) に全画面の暗い背景を敷く。二重 Install は既存インスタンスを返す。
        /// </summary>
        public static DesktopPillarbox Install(RectTransform contentRoot)
        {
            if (contentRoot == null)
            {
                Debug.LogError("[DesktopPillarbox] contentRoot が null のため生成できない");
                return null;
            }

            DesktopPillarbox existing = contentRoot.GetComponent<DesktopPillarbox>();
            if (existing != null) return existing;

            // 背景は contentRoot と同じ親の下に、contentRoot より手前 (描画順で背面) に生成する。
            // Screen Space Overlay Canvas は「後の兄弟ほど手前に描画される」ため、
            // SetAsFirstSibling で常に contentRoot より背面に固定できる。
            Transform parent = contentRoot.parent;
            if (parent != null)
            {
                var backdropGo = new GameObject("DesktopPillarboxBackdrop", typeof(RectTransform), typeof(Image));
                backdropGo.transform.SetParent(parent, false);
                backdropGo.transform.SetAsFirstSibling();

                var backdropRt = (RectTransform)backdropGo.transform;
                backdropRt.anchorMin = Vector2.zero;
                backdropRt.anchorMax = Vector2.one;
                backdropRt.offsetMin = Vector2.zero;
                backdropRt.offsetMax = Vector2.zero;

                Image backdropImg = backdropGo.GetComponent<Image>();
                Color bg = UITheme.Background;
                backdropImg.color = new Color(
                    bg.r * BackdropDarkenFactor, bg.g * BackdropDarkenFactor, bg.b * BackdropDarkenFactor, 1f);
                backdropImg.raycastTarget = false; // 装飾のみ、タップを吸わない
            }
            else
            {
                Debug.LogWarning("[DesktopPillarbox] contentRoot に親が無いため背景は生成しない (拘束のみ適用)");
            }

            var pillarbox = contentRoot.gameObject.AddComponent<DesktopPillarbox>();
            pillarbox._contentRoot = contentRoot;
            return pillarbox;
        }

        private void Awake()
        {
            if (_contentRoot == null) _contentRoot = (RectTransform)transform; // Install 未経由の保険
            TryApply();
        }

        private void Update()
        {
            TryApply();
        }

        /// <summary>モバイル実機では無処理。解像度キャッシュが変化したフレームだけ再計算する。</summary>
        private void TryApply()
        {
            if (Application.isMobilePlatform) return; // 実機モバイルでは常に無害
            if (Screen.width <= 0 || Screen.height <= 0) return;
            if (_lastScreenSize.x == Screen.width && _lastScreenSize.y == Screen.height) return;

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            Apply();
        }

        private void Apply()
        {
            // --- 均一スケール方式 (v2) ---
            // 旧実装はアンカー拘束のみで、①設計比率より細い窓を素通し ②CanvasScaler の
            // スケール係数が「窓全体」基準のためカラム幅と食い違う、の 2 点により
            // 細長い窓で文字が余計に折り返して重なる崩れが起きた (2026-07-04 オーナー実機報告)。
            // v2 では contentRoot を常に参照解像度 1170x2532 の固定サイズで組み、
            // 窓に収まる等倍率 (min) で localScale 縮放する。これによりどんな窓サイズ/比率でも
            // レイアウトは Editor 検証済みの iPhone 比率と完全相似形になる (文字折り返し不変)。
            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;

            // 窓の実ピクセルを Canvas 単位系へ変換してから、参照解像度に対する収まり倍率を取る
            float availW = Screen.width / scaleFactor;
            float availH = Screen.height / scaleFactor;
            float fit = Mathf.Min(availW / ReferenceWidth, availH / ReferenceHeight);

            _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _contentRoot.pivot = new Vector2(0.5f, 0.5f);
            _contentRoot.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            _contentRoot.anchoredPosition = Vector2.zero;
            _contentRoot.localScale = new Vector3(fit, fit, 1f);
        }
    }
}
