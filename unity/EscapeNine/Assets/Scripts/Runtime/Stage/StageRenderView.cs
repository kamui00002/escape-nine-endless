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

        /// <summary>
        /// Wave 5: 品質ティアの RT フォーマット (StageQuality.SetFormat が設定)。既定は
        /// HDR (Bloom の閾値超え発光を保持する従来値、W3 導入時のコメント参照)。
        /// </summary>
        private RenderTextureFormat _format = RenderTextureFormat.DefaultHDR;

        public void Configure(RectTransform anchor, RawImage rawImage, Camera camera)
        {
            _anchor = anchor;
            _rawImage = rawImage;
            _camera = camera;
        }

        /// <summary>
        /// Wave 5: RT フォーマットを切り替える (StageQuality.Apply から呼ばれる)。同一サイズでも
        /// フォーマット変更時は RT を確実に再生成させるため、変更があれば即座に解放する
        /// (次の Apply() が _rt==null を見て新フォーマットで作り直す)。解放前にカメラ/RawImage を
        /// 切り離す手順は OnDisable と同一 (カメラのターゲットに設定されたままの Release は
        /// Unity が警告を出すため)。
        /// </summary>
        public void SetFormat(RenderTextureFormat format)
        {
            if (_format == format) return;
            _format = format;

            if (_camera != null && _camera.targetTexture == _rt) _camera.targetTexture = null;
            if (_rawImage != null)
            {
                _rawImage.texture = null;
                _rawImage.enabled = false; // 再結線まで白矩形を出さない (次の Apply() が再有効化する)
            }
            ReleaseRt();
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
                // HDR 化 (Wave 3): LDR (0-1 clamp) の RT だと Bloom の閾値超え発光が
                // クランプで潰れて滲みが出ない。DefaultHDR で 1.0 超の輝度を保持したまま
                // RT へ描画し、Volume の Bloom がそれを拾えるようにする。
                // Wave 5: Low ティアは SetFormat() 経由で _format が LDR (Default) に切り替わる
                // (Bloom 自体が Low では無効化されるため HDR を維持する意味が無く、
                // メモリ/帯域節約のため LDR へ落とす)。
                _rt = new RenderTexture(w, h, 24, _format) { name = "BoardStageRT" };
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
