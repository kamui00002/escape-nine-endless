// TileView.cs
// Wave 2 (3D BoardStage): 旧 uGUI 盤面セル GridCellWidget.cs のワールド空間版。
// (旧 uGUI 盤面 GridBoardWidget/GridCellWidget は W5 で削除済み (D4)。
//  以下の GridCellWidget への言及は移植元 = Swift GridCellView.swift 相当の記録)
// 1 マス = Cube プリミティブ (scale (1, 0.18, 1))。上面色でセル状態を表現し、
// 霧/消失/選択のマークはタイル直上に浮かべた世界空間 TMP (TextMeshPro 3D) で表す。
//
// 情報パリティ: 受け取る CellVisual (Stage/CellVisual.cs。W5 で旧 GridCellWidget.cs から
// 移設) の組み立ては BoardStage.Render() が GameSession.IsCellVisible / IsCellDisappeared /
// GetAvailableMoves / PendingPlayerMove を参照して行うため、可視性の判定ロジックは
// GameSession に一元化されたまま変わらない。
//
// 色のブレンド定数 (FogFillColor 等) は旧 GridCellWidget.ApplyBlend() の値を引き継いだもの。
// W5 の uGUI 盤面削除により、現在はここが唯一の定義 (Wave 2 時点の意図的複製は解消済み)。
//
// マテリアルは URP Lit ("Universal Render Pipeline/Lit") を使い、色替えは
// MaterialPropertyBlock 経由 (SRP Batcher が個体別マテリアルインスタンスでも
// 同一シェーダー・同一プロパティレイアウトならバッチ対象にできるため)。
//
// 旧 GridCellWidget との意図的な差分:
//   - 霧マークの文字は「霧」ではなく「?」にしている (このタスクの設計指定に従う)。
//     情報としては「視界が制限されている」ことを示す点で等価。
//   - 枠線 (border) の 4 本描画は省略し、状態色を単色ブレンドのみで表現する
//     (このタスクの設計指定が Cube 1 個の「上面色」のみを挙げているため)。
//
// Wave 4 追加: 消失マスの「沈み」を「崩落」(下降+傾き+暗転、0.5s) へ強化。
// 実装は既存の Tick(deltaTime) 駆動の状態遷移 (MoveTowards ベース) を拡張したもので、
// 文字どおりの C# コルーチンではない — TileView は MonoBehaviour ではなく
// (ファイル冒頭コメント参照)、既存の霧フェードも同じ Tick 駆動パターンのため、
// アーキテクチャの一貫性を優先した (BoardStage.Update() が毎フレーム Tick を呼ぶ)。
// 復活 (消失解除) は _disappearTarget が 0 に戻ることで同じ MoveTowards が
// 自然に逆再生する。Reduce Motion 時は MoveTowards のステップ計算を飛ばして
// 即座に目標値へスナップする。

using UnityEngine;
using TMPro;
using EscapeNine.Runtime.UI; // UITheme (色パレット / FontAsset)
using EscapeNine.Runtime.UI.Fx; // FxKit.MotionEnabled (Reduce Motion)

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
    /// 盤面 1 マスのワールド表現。旧 GridCellWidget 同様 MonoBehaviour ではない素のクラス
    /// (毎フレーム処理は BoardStage.Update() から Tick(deltaTime) を呼んでもらう)。
    /// </summary>
    public sealed class TileView
    {
        /// <summary>
        /// Phase 5c ボステレグラフ (docs/unity-phase5-roguelike-design.md §1.5/§5)。
        /// Foresight = 予告 (青白く明滅) / Intimidation = 進入不可の赤熱。恒久消失 (崩落) とは
        /// 「存在の有無」で区別する (威圧はタイルが存在したまま赤熱、消失は崩落して消える)。
        /// </summary>
        public enum BossTelegraphKind { None, Foresight, Intimidation }

        public readonly int Position;

        /// <summary>タイル 1 枚分の footprint (中心間隔と同じにすると隙間なく並ぶ)。</summary>
        public const float Footprint = 0.94f;

        /// <summary>Cube の高さ (design 指定: scale (1, 0.18, 1))。</summary>
        private const float TileHeight = 0.18f;

        /// <summary>マークを浮かべる高さ (タイル上面 + 0.15、design 指定)。</summary>
        private const float MarkHeightAboveTop = 0.15f;

        /// <summary>消失時に沈み込む最大量 (World 単位、design 指定: 下降 0.4)。</summary>
        private const float CollapseSinkOffset = 0.4f;

        /// <summary>消失時に傾く最大角度 (度、design 指定: 傾き 8°)。</summary>
        private const float CollapseTiltDegrees = 8f;

        /// <summary>消失の崩落アニメ所要秒 (design 指定: 0.5s)。霧フェード (SpecialFadeDuration) とは別軸。</summary>
        private const float CollapseDuration = 0.5f;

        /// <summary>霧フェード所要秒 (旧 GridCellWidget.SpecialFadeDuration と同一値)。</summary>
        private const float SpecialFadeDuration = 0.28f;

        // 旧 GridCellWidget.ApplyBlend() から引き継いだ事前計算色 (ファイル冒頭コメント参照)。
        private static readonly Color FogFillColor = Color.Lerp(UITheme.Background, UITheme.Fog, 0.4f);
        private static readonly Color DisappearedFillColor = UITheme.Disappeared;
        private const float FogMarkAlpha = 0.6f;   // 3D は文字が小さく見えるため 2D 版 (0.15) より強めに視認性確保
        private const float XMarkAlpha = 0.85f;    // 同上 (2D 版は 0.3)

        /// <summary>
        /// 通常マスの基調色ゾーンティント (Wave 4)。BoardStage がゾーン変化時に書き換える
        /// static フィールド (GameSession/CellVisual を経由しないゾーン専用の見た目情報のため、
        /// CellVisual 構造体 (Stage/CellVisual.cs) を拡張せずここに持たせる)。
        /// 既定値は UITheme.Grid (旧来と同じ見た目) で、BoardStage が未初期化でも安全。
        /// </summary>
        public static Color ZoneGridTint = UITheme.Grid;

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
        private bool _isSelectedGlow; // 予約済み移動先マスの拍同期パルス発光 (オーナー: 矢印だと分かりづらい→マスを光らせる)
        private bool _bossAura;       // ボス階で鬼が乗るマスの赤い拍発光 (足元アオーラ。B案・縦に伸びない演出)

        // ---- Phase 5c ボステレグラフ ----
        private BossTelegraphKind _telegraph = BossTelegraphKind.None;
        private float _telegraphPhase; // 明滅の位相 (拍単位。Conductor.SongPositionBeats を Tick で受ける)。

        /// <summary>威圧マスの赤熱色 (消失=崩落と区別するため、赤系だが崩落の暗転色とは別)。</summary>
        private static readonly Color IntimidationHotColor = new Color(1f, 0.30f, 0.12f);

        /// <summary>先読み予告の青白い発光色 (§5.1②のフォールバック: ボス隣接を青白く明滅)。</summary>
        private static readonly Color ForesightGlowColor = new Color(0.62f, 0.80f, 1f);

        /// <summary>予約済み移動先マスの発光色 (暖色ゴールド。明るくして URP Bloom で"光る"→移動先が一目で分かる)。</summary>
        private static readonly Color SelectedGlowColor = new Color(1f, 0.82f, 0.35f);

        /// <summary>ボス足元アオーラの発光色 (深紅。鬼が乗るマスを拍で赤熱させ威圧感を出す。B案)。</summary>
        private static readonly Color BossAuraColor = new Color(1f, 0.18f, 0.06f);

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
            // フォールバックは URP/Unlit (Standard は URP 下でマゼンタになるため使わない。両方 AlwaysIncludedShaders 登録済み)。
            fillRenderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));

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

        /// <summary>状態を見た目に反映する。旧 GridCellWidget.Render() (Swift: GridCellView.body) と同一の優先順位ロジック。</summary>
        public void Render(CellVisual v)
        {
            if (v.IsPlayer) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Player, 0.30f);
            else if (v.IsEnemy) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Enemy, 0.30f);
            else if (v.IsSelected) _normalFillColor = Color.Lerp(UITheme.Background, UITheme.Available, 0.40f);
            else _normalFillColor = ZoneGridTint;

            _disappearTarget = v.IsDisappeared ? 1f : 0f;
            _fogTarget = (!v.IsDisappeared && !v.IsVisible) ? 1f : 0f;

            _selectedMark.gameObject.SetActive(v.IsSelected && !v.IsDisappeared && v.IsVisible);
            // 予約済み移動先 (プレイヤー/敵が乗っていない純粋な移動先マス) を拍同期で発光させる。
            _isSelectedGlow = v.IsSelected && !v.IsPlayer && !v.IsEnemy && !v.IsDisappeared && v.IsVisible;

            // Swift/GridCellWidget: 通常マスのみ disabled で 0.5 減光、霧/消失は常時 alpha=1。
            // 3D では背景と同色 (UITheme.Background) への Lerp で同じ「薄暗くなる」印象を近似する。
            bool isSpecial = v.IsDisappeared || !v.IsVisible;
            _disabledDim = isSpecial ? 0f : (v.Disabled ? 0.5f : 0f);

            ApplyBlend();
        }

        /// <summary>
        /// Phase 5c: ボステレグラフの種別を設定する (BoardStage.Render がボスパターン/威圧ゾーンから決定)。
        /// 毎 Render 呼ばれ、非該当タイルは None にリセットされる。
        /// </summary>
        public void SetBossTelegraph(BossTelegraphKind kind)
        {
            _telegraph = kind;
        }

        /// <summary>ボス階で鬼が乗るマスの足元アオーラ (赤い拍発光) の on/off。BoardStage.Render が毎回設定する。</summary>
        public void SetBossAura(bool active)
        {
            _bossAura = active;
        }

        /// <summary>霧フェード + 消失の崩落アニメの時間進行。BoardStage.Update() から毎フレーム呼ばれる。</summary>
        public void Tick(float deltaTime, float beatPhase)
        {
            float fogStep = deltaTime / SpecialFadeDuration;
            _fogAlpha = Mathf.MoveTowards(_fogAlpha, _fogTarget, fogStep);

            if (FxKit.MotionEnabled)
            {
                float collapseStep = deltaTime / CollapseDuration;
                _disappearAlpha = Mathf.MoveTowards(_disappearAlpha, _disappearTarget, collapseStep);
            }
            else
            {
                _disappearAlpha = _disappearTarget; // Reduce Motion: 即時反映 (design 指定)
            }

            // Phase 5c 修正: テレグラフの明滅位相は拍 (Conductor.SongPositionBeats) を正とする。
            // 旧実装は deltaTime を秒累積しており拍とズレていた (§1 拍駆動規約違反)。
            // Reduce Motion 時は明滅せず定常表示にするため位相を固定する。
            _telegraphPhase = FxKit.MotionEnabled ? beatPhase : 0f;

            ApplyBlend();
        }

        private void ApplyBlend()
        {
            Color fill = Color.Lerp(_normalFillColor, UITheme.Background, _disabledDim);
            fill = Color.Lerp(fill, FogFillColor, _fogAlpha);
            fill = Color.Lerp(fill, DisappearedFillColor, _disappearAlpha);

            // Phase 5c: ボステレグラフの上塗り (消失=崩落の暗転より後に乗せ、赤熱/青白が見えるように)。
            // _telegraphPhase は拍単位 (Conductor.SongPositionBeats)。威圧=1拍に1回、先読み=1拍に2回の
            // 明滅にすることで曲のビートに吸着させる。Reduce Motion 時 (MotionEnabled==false) は定常強度で表示。
            if (_telegraph == BossTelegraphKind.Intimidation)
            {
                float pulse = 0.45f + 0.25f * (FxKit.MotionEnabled ? Mathf.Abs(Mathf.Sin(_telegraphPhase * Mathf.PI)) : 1f);
                fill = Color.Lerp(fill, IntimidationHotColor, pulse);
            }
            else if (_telegraph == BossTelegraphKind.Foresight)
            {
                float pulse = 0.25f + 0.25f * (FxKit.MotionEnabled ? Mathf.Abs(Mathf.Sin(_telegraphPhase * Mathf.PI * 2f)) : 1f);
                fill = Color.Lerp(fill, ForesightGlowColor, pulse);
            }

            // 予約済み移動先マスの拍同期パルス発光 (テレグラフと同じ _telegraphPhase 拍位相を再利用。
            // 明るいゴールドへ寄せることで URP Bloom が拾い"光る"。Reduce Motion 時は定常発光)。
            if (_isSelectedGlow)
            {
                float pulse = 0.35f + 0.30f * (FxKit.MotionEnabled ? Mathf.Abs(Mathf.Sin(_telegraphPhase * Mathf.PI)) : 1f);
                fill = Color.Lerp(fill, SelectedGlowColor, pulse);
            }

            // ボス階: 鬼が乗るマスを赤く拍で発光させ「ボスの足元アオーラ」を表現 (B案・縦に伸びずはみ出さない)。
            // 拍位相 (_telegraphPhase) 同期。Reduce Motion 時は定常発光。URP Bloom で赤熱がにじむ。
            if (_bossAura)
            {
                float pulse = 0.40f + 0.35f * (FxKit.MotionEnabled ? Mathf.Abs(Mathf.Sin(_telegraphPhase * Mathf.PI)) : 1f);
                fill = Color.Lerp(fill, BossAuraColor, pulse);
            }

            SetFillColor(fill);

            _fogMark.color = UITheme.WithAlpha(UITheme.TextColor, FogMarkAlpha * _fogAlpha);
            _xMark.color = UITheme.WithAlpha(UITheme.Warning, XMarkAlpha * _disappearAlpha);

            // 消失マスの崩落 (design: 下降0.4 + 傾き8° + 暗転)。ease-in (二乗) で「崩れ落ちる」
            // 加速感を出す。暗転は上の DisappearedFillColor ブレンドが担う。復活は _disappearTarget
            // が 0 に戻ることで Tick() の MoveTowards が自然に逆再生する (design: 「逆再生で戻す」)。
            float eased = _disappearAlpha * _disappearAlpha;
            float sink = CollapseSinkOffset * eased;
            _fill.localPosition = new Vector3(0f, TileHeight * 0.5f - sink, 0f);
            _fill.localRotation = Quaternion.Euler(CollapseTiltDegrees * eased, 0f, 0f);
        }

        private void SetFillColor(Color color)
        {
            _fillRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _fillRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 実行時生成したタイル素材 (fill の Material) を破棄する。TileView は MonoBehaviour ではないため
        /// 親の BoardStage.OnDestroy から呼ぶ (Editor の "leaked material instance" 警告防止、2026-07-04 C6)。
        /// </summary>
        public void DestroyMaterials()
        {
            if (_fillRenderer != null && _fillRenderer.sharedMaterial != null)
            {
                UnityEngine.Object.Destroy(_fillRenderer.sharedMaterial);
            }
        }
    }
}
