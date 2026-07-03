// SafeAreaFitter.cs
// Swift 正本: SwiftUI では safe area がフレームワークで自動処理されるため対応コード無し。
// uGUI には自動処理が無いので、Screen.safeArea をアンカー比率に変換する標準実装を置く。
// Canvas 直下の「SafeArea」パネルにアタッチし、全画面 UI をその子として組む前提
// (UIFactory.Place の比率レイアウトが safe area 内で完結するようにするため)。

using UnityEngine;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// Screen.safeArea を RectTransform のアンカーへ変換する。
    /// ノッチ / Dynamic Island / ホームインジケータ領域を自動で避ける。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;

        // 毎フレーム apply すると無駄なレイアウト再計算が走るため、
        // safeArea / 解像度 / 向きをキャッシュし変化した時だけ適用する。
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            // iOS の回転・iPad Split View・エディタの Game ビューリサイズを検知する。
            // イベント通知 API が無いためポーリングが uGUI の標準手法。
            if (_lastSafeArea != Screen.safeArea
                || _lastScreenSize.x != Screen.width
                || _lastScreenSize.y != Screen.height
                || _lastOrientation != Screen.orientation)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;

            // 起動直後やエディタ初期化中に 0 が来るフレームがあるため防御
            if (Screen.width <= 0 || Screen.height <= 0) return;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            // ピクセル矩形 → 親 (Canvas 全面) に対する 0..1 アンカー比率へ変換。
            // offset を 0 にして固定 px を残さない (UIFactory.Place と同じ原則)。
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
