// StageInput.cs
// Wave 2: BoardStage (3D 盤面) のタップ入力。旧 uGUI 盤面 GridBoardWidget.cs
// (W5 で削除済み) の Button ベースのタップ受けをワールド空間向けに置き換える —
// Camera.ScreenPointToRay + Physics.Raycast でタイルの BoxCollider (TileHitTarget) を拾い、
// BoardStage.HandleCellTap(position) を呼ぶ (旧 GridBoardWidget.HandleCellTap と
// 同一の鬼マス優先判定に委譲する)。
//
// EventSystem.current.IsPointerOverGameObject() (タッチは fingerId 版) が true の場合は
// 無視する (design 指定) — HUD ボタン (uGUI) の上をタップした時に盤面レイキャストが
// 二重発火しないようにするための安全策。
//
// 入力 API はプロジェクト方針 (KeyboardInput.cs コメント参照: 旧 Input Manager 前提、
// activeInputHandler=0) に合わせ、legacy UnityEngine.Input を使う。

using UnityEngine;
using UnityEngine.EventSystems;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StageInput : MonoBehaviour
    {
        private const float MaxRayDistance = 100f;

        private Camera _camera;
        private BoardStage _stage;
        private RectTransform _anchor;

        public void Configure(Camera camera, BoardStage stage, RectTransform anchor)
        {
            _camera = camera;
            _stage = stage;
            _anchor = anchor;
        }

        private void Update()
        {
            if (_camera == null || _stage == null) return;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Ended) continue; // タップ確定時のみ (Button.onClick 相当)
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                    TryHit(touch.position);
                }
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                TryHit(Input.mousePosition);
            }
        }

        private void TryHit(Vector2 screenPosition)
        {
            // RenderTexture 表示 (StageRenderView) のため、スクリーン座標を BoardAnchor 内の
            // 0..1 (uv) に写像してから ViewportPointToRay へ渡す — カメラは RT 全面へ描画している。
            if (_anchor == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_anchor, screenPosition, null, out Vector2 local)) return;
            Rect r = _anchor.rect;
            var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return; // 盤面領域の外のタップは無視

            Ray ray = _camera.ViewportPointToRay(uv);
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxRayDistance)) return;

            var tile = hit.collider.GetComponent<TileHitTarget>();
            if (tile != null)
            {
                _stage.HandleCellTap(tile.Position);
                return;
            }

            if (_stage.IsEnemyCollider(hit.collider))
            {
                _stage.HandleCellTap(_stage.EnemyPosition);
            }
        }
    }
}
