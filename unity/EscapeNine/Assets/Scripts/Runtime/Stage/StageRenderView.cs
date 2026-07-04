// StageRenderView.cs
// Wave 2 v2: 3D 舞台 (BoardStage) の表示方式。
//
// 当初の「camera.rect + 背景穴あけバンド」方式は実機検証で 3 点破綻した (2026-07-04):
//   ① GameScreen の全画面 Background(不透明) が穴を塞ぎ、カメラ映像が一切見えない
//   ② 全幅バンドが後続生成のため上部 HUD を覆い隠す (uGUI は後の兄弟が上に描画)
//   ③ Editor では GetWorldCorners (Canvas 描画px系) と Screen.width (Game view パネルpx) が
//      食い違い、camera.rect が歪む (実測: パネル 1514x1150 vs 描画 1080x2532)
//
// v2 はカメラを RenderTexture へ描画し、BoardAnchor に付けた RawImage で表示する。
// 盤面表示が uGUI レイアウトの一要素になるため、穴あけも viewport 同期も不要になり、
// SafeArea / DesktopPillarbox / ウィンドウリサイズへの追従はレイアウトシステムが担う。
// W3 のポストプロセス (URP Volume) は RT ターゲットのカメラにもそのまま適用される。

using UnityEngine;
using UnityEngine.UI;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StageRenderView : MonoBehaviour
    {
        private RectTransform _anchor;
        private RawImage _rawImage;
        private Camera _camera;
        private RenderTexture _rt;
        private readonly Vector3[] _corners = new Vector3[4];

        public void Configure(RectTransform anchor, RawImage rawImage, Camera camera)
        {
            _anchor = anchor;
            _rawImage = rawImage;
            _camera = camera;
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>
        /// BoardAnchor の実ピクセルサイズに合わせて RT を(再)生成し、カメラと RawImage へ結線する。
        /// サイズが変わったフレームだけ再生成 (リサイズ追従)。
        /// </summary>
        public void Apply()
        {
            if (_anchor == null || _rawImage == null || _camera == null) return;

            _anchor.GetWorldCorners(_corners); // Overlay Canvas では Canvas 描画ピクセル系
            int w = Mathf.RoundToInt(_corners[2].x - _corners[0].x);
            int h = Mathf.RoundToInt(_corners[2].y - _corners[0].y);
            if (w < 8 || h < 8) return; // 未レイアウトのゼロ矩形は無視

            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                ReleaseRt();
                _rt = new RenderTexture(w, h, 24) { name = "BoardStageRT" };
                _camera.targetTexture = _rt;
                _camera.rect = new Rect(0f, 0f, 1f, 1f); // 旧方式が設定した部分 rect を打ち消す
                _rawImage.texture = _rt;
                _rawImage.enabled = true; // RT が繋がるまでは白い矩形が出ないよう無効で待つ
            }
        }

        private void OnDisable()
        {
            // 他画面へ戻る時 (BoardStage ごと SetActive(false)) に必ず通る後始末。
            if (_camera != null && _camera.targetTexture == _rt) _camera.targetTexture = null;
            if (_rawImage != null)
            {
                _rawImage.texture = null;
                _rawImage.enabled = false;
            }
            ReleaseRt();
        }

        private void ReleaseRt()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
        }
    }
}
