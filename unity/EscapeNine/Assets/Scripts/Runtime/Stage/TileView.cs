// TileView.cs
// Wave 2 (3D BoardStage): GridCellWidget.cs (uGUI 版セル) のワールド空間版。
// 1 マス = Cube プリミティブ (scale (1, 0.18, 1))。上面色でセル状態を表現し、
// 霧/消失/選択のマークはタイル直上に浮かべた世界空間 TMP (TextMeshPro 3D) で表す。
//
// 情報パリティ: 受け取る CellVisual は GridCellWidget.cs (namespace EscapeNine.Runtime.UI)
// で定義済みの構造体をそのまま再利用する (新規に複製しない)。BoardStage.Render() は
// GridBoardWidget.Render() と全く同じ手順 (GameSession.IsCellVisible / IsCellDisappeared /
// GetAvailableMoves / PendingPlayerMove) で CellVisual を組み立てるため、可視性の
// 判定ロジックは GameSession に一元化されたまま変わらない。
//
// 色のブレンド定数 (FogFillColor 等) は GridCellWidget.ApplyBlend() と同一の値を
// 意図的に複製している (GridCellWidget.cs 側は変更禁止のため、値を共有プロパティとして
// 公開させることができない)。値を変える場合は両ファイルを併せて確認すること。
//
// マテリアルは URP Lit ("Universal Render Pipeline/Lit") を使い、色替えは
// MaterialPropertyBlock 経由 (SRP Batcher が個体別マテリアルインスタンスでも
// 同一シェーダー・同一プロパティレイアウトならバッチ対象にできるため)。
//
// GridCellWidget との意図的な差分:
//   - 霧マークの文字は「霧」ではなく「?」にしている (このタスクの設計指定に従う)。
//     情報としては「視界が制限されている」ことを示す点で等価。
//   - 枠線 (border) の 4 本描画は省略し、状態色を単色ブレンドのみで表現する
//     (このタスクの設計指定が Cube 1 個の「上面色」のみを挙げているため)。

using UnityEngine;
using TMPro;
using EscapeNine.Runtime.UI; // CellVisual を再利用

namespace EscapeNine.Runtime.Stage
{
    /// <summary>
    /// タイルの Collider に付け、Physics.Raycast のヒットから盤面座標 (1..9) を
    /// 引けるようにするマーカー。StageInput が参照する。
    /// </summary>
    public sealed class TileHitTarget : MonoBehaviour
    {
        public int Position;
    }

    /// <summary>
    /// 盤面 1 マスのワールド表現。GridCellWidget 同様 MonoBehaviour ではない素のクラス
    /// (毎フレーム処理は BoardStage.Update() から Tick(deltaTime) を呼んでもらう)。
    /// </summary>
    public sealed class TileView
    {
        public readonly int Position;

        /// <summary>タイル 1 枚分の footprint (中心間隔と同じにすると隙間なく並ぶ)。</summary>
        public const float Footprint = 0.94f;

        /// <summary>Cube の高さ (design 指定: scale (1, 0.18, 1))。</summary>
        private const float TileHeight = 0.18f;

        /// <summary>マークを浮かべる高さ (タイル上面 + 0.15、design 指定)。</summary>
        private const float MarkHeightAboveTop = 0.15f;

        /// <summary>消失時に沈み込む最大量 (World 単位)。</summary>
        private const float MaxSinkOffset = 0.12f;

        /// <summary>霧/消失フェード所要秒 (GridCellWidget.SpecialFadeDuration と同一)。</summary>
        private const float SpecialFadeDuration = 0.28f;

        // GridCellWidget.ApplyBlend() と同一の事前計算色 (意図的複製。理由はファイル冒頭コメント参照)。
        private static readonly Color FogFillColor = Color.Lerp(UITheme.Background, UITheme.Fog, 0.4f);
        private static readonly Color DisappearedFillColor = UITheme.Disappeared;
        private const float FogMarkAlpha = 0.6f;   // 3D は文字が小さく見えるため 2D 版 (0.15) より強めに視認性確保
        private const float XMarkAlpha = 0.85f;    // 同上 (2D 版は 0.3)

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Transform _fill;
        private readonly Renderer _fillRenderer;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        private readonly TextMeshPro _selectedMark;
        private readonly TextMeshPro _fogMark;
        private readonly TextMeshPro _xMark;

        private Color _normalFillColor = UITheme.Grid;
        private float _fogAlpha;
        private float _fogTarget;
        private float _disappearAlpha;
        private float _disappearTarget;
        private float _disabledDim;

        private TileView(int position, Transform fill, Renderer fillRenderer,
            TextMeshPro selectedMark, TextMeshPro fogMark, TextMeshPro xMark)
        {
            Position = position;
            _fill = fill;
            _fillRenderer = fillRenderer;
            _selectedMark = selectedMark;
            _fogMark = fogMark;
            _xMark = xMark;
        }

        /// <summary>
        /// 盤面座標 (1..9) のタイルを生成する。worldCenter はタイル中心の (x, 0, z) 座標
        /// (BoardStage が row/col からタイル間隔で算出する)。
        /// </summary>
        public static TileView Create(Transform parent, int position, Vector3 worldCenter)
        {
            var rootGo = new GameObject("Tile_" + position);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.localPosition = worldCenter;

            GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fillGo.name = "Fill";
            fillGo.transform.SetParent(rootGo.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, TileHeight * 0.5f, 0f);
            fillGo.transform.localScale = new Vector3(Footprint, TileHeight, Footprint);

            Renderer fillRenderer = fillGo.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            fillRenderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));

            var hitTarget = fillGo.AddComponent<TileHitTarget>();
            hitTarget.Position = position;

            float markY = TileHeight + MarkHeightAboveTop;

            // fontSize は TextMeshPro (3D) の world-space 換算の第一稿値 (未検証・要目視調整。
            // 完了報告の flag 参照)。タイル footprint (0.94) に対して概ね読める大きさを狙った推定値。
            TextMeshPro selectedMark = CreateMark(rootGo.transform, "SelectedMark", "▼",
                new Vector3(Footprint * 0.28f, markY, Footprint * 0.28f), UITheme.Available, 2.4f);
            selectedMark.gameObject.SetActive(false);

            TextMeshPro fogMark = CreateMark(rootGo.transform, "FogMark", "?",
                new Vector3(0f, markY, 0f), UITheme.TextColor, 3.6f);

            TextMeshPro xMark = CreateMark(rootGo.transform, "XMark", "x",
                new Vector3(0f, markY, 0f), UITheme.Warning, 4.2f);

            var view = new TileView(position, fillGo.transform, fillRenderer,
                selectedMark, fogMark, xMark);
            view.ApplyBlend();
            return view;
        }

        private static TextMeshPro CreateMark(Transform parent, string name, string text, Vector3 localPos,
            Color color, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // 上向き (地面と水平) を狙った回転。90 vs -90 は未検証の第一稿 (完了報告の flag 参照) —
            // Play モードで上下逆に見える場合は符号を反転すること。
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = UITheme.FontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
        }

        /// <summary>状態を見た目に反映する。GridCellWidget.Render() と同一の優先順位ロジック。</summary>
        public void Render(CellVisual v)
        {
            if (v.IsPlayer) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Player, 0.30f);
            else if (v.IsEnemy) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Enemy, 0.30f);
            else if (v.IsSelected) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Available, 0.25f);
            else _normalFillColor = UITheme.Grid;

            _disappearTarget = v.IsDisappeared ? 1f : 0f;
            _fogTarget = (!v.IsDisappeared && !v.IsVisible) ? 1f : 0f;

            _selectedMark.gameObject.SetActive(v.IsSelected && !v.IsDisappeared && v.IsVisible);

            // Swift/GridCellWidget: 通常マスのみ disabled で 0.5 減光、霧/消失は常時 alpha=1。
            // 3D では背景と同色 (UITheme.Background) への Lerp で同じ「薄暗くなる」印象を近似する。
            bool isSpecial = v.IsDisappeared || !v.IsVisible;
            _disabledDim = isSpecial ? 0f : (v.Disabled ? 0.5f : 0f);

            ApplyBlend();
        }

        /// <summary>霧/消失フェードの時間進行 + 消失時の沈み込みアニメ。BoardStage.Update() から毎フレーム呼ばれる。</summary>
        public void Tick(float deltaTime)
        {
            float step = deltaTime / SpecialFadeDuration;
            _fogAlpha = Mathf.MoveTowards(_fogAlpha, _fogTarget, step);
            _disappearAlpha = Mathf.MoveTowards(_disappearAlpha, _disappearTarget, step);
            ApplyBlend();
        }

        private void ApplyBlend()
        {
            Color fill = Color.Lerp(_normalFillColor, UITheme.Background, _disabledDim);
            fill = Color.Lerp(fill, FogFillColor, _fogAlpha);
            fill = Color.Lerp(fill, DisappearedFillColor, _disappearAlpha);
            SetFillColor(fill);

            _fogMark.color = UITheme.WithAlpha(UITheme.TextColor, FogMarkAlpha * _fogAlpha);
            _xMark.color = UITheme.WithAlpha(UITheme.Warning, XMarkAlpha * _disappearAlpha);

            // 消失マスは沈み込む (design: 「沈み」)。マーク自体の高さは固定 (アルファのみで出現)。
            float sink = MaxSinkOffset * _disappearAlpha;
            _fill.localPosition = new Vector3(0f, TileHeight * 0.5f - sink, 0f);
        }

        private void SetFillColor(Color color)
        {
            _fillRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _fillRenderer.SetPropertyBlock(_mpb);
        }
    }
}
