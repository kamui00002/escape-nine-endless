// StageLights.cs
// Wave 4: ゾーン別のライティングを一括管理する BoardStage の子コンポーネント。
// W2 で BoardStage.Create() が生成していた暫定 Directional Light (旧 CreatePlaceholderLight)
// はここへ移管し、BoardStage 側の生成コードは削除した。
//
// 情報パリティ (絶対制約 §2): 鬼の表示可否は従来どおり Core の IsCellVisible が決定し、
// PawnView.Render(sprite, visible) の enabled 切替のみで隠す。本クラスが変更するのは
// 「見えている物の照らし方」(色温度・明るさ・環境光) だけで、どのマス/駒が見えるかという
// 判定には一切関与しない。霧時のポイントライトはプレイヤー自身のタイル付近を照らすだけであり、
// 鬼が霧で非表示の間はそもそも PawnView.enabled=false のため、ライトが強くても鬼のスプライト
// 自体が描画されない (見える情報が増えることはない)。タイルの色 (TileView.ApplyBlend) も
// Core の IsCellVisible/IsCellDisappeared から決まる値のブレンドであり、シーンの明るさには
// 依存しない。
//
// 霧時の減光は「環境光」と「ゾーン主光 (Directional Light)」の両方に掛ける。環境光だけ
// 落として主光を放置すると、URP Lit タイルは主光の直接光だけで十分明るく見えてしまい、
// 「プレイヤータイル上だけ灯る」効果が実質見えない (advisor 指摘の是正)。
//
// ★ dspTime 駆動ルール対象外: 本クラスの発光はビートに同期する演出ではなく、ゾーン/霧という
//   「状態」に紐づく持続的なライティングのため、BeatVolumePulse とは独立した扱いでよい。

using UnityEngine;
using UnityEngine.Rendering;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StageLights : MonoBehaviour
    {
        /// <summary>霧時にプレイヤータイル上へ灯すポイントライトの高さ (World 単位、design 指定: ~2)。</summary>
        private const float FogPointLightHeight = 2f;

        /// <summary>
        /// 霧時のポイントライト到達距離。design 指定の「~2.2」は height=2 と組み合わせると
        /// 接地面の被照射半径が sqrt(2.2²-2²)≈0.92 world 単位しかなく、タイル間隔 (1.1) 未満で
        /// 隣接マス (Chebyshev 距離1、対角 1.556) さえ十分照らせない (advisor 指摘)。
        /// height=2 を維持したまま、対角マスまである程度届くよう range を 2.7 に調整する
        /// (被照射半径 ≈ sqrt(2.7²-2²) ≈ 1.8)。第一稿の推定値であり、実機目視での微調整を要する。
        /// </summary>
        private const float FogPointLightRange = 2.7f;

        private const float FogPointLightIntensity = 3.5f;

        private Light _zoneLight;
        private Light _fogPointLight;

        private ZoneTheme _currentTheme;
        private bool _hasTheme;
        private bool _fogActive;

        public static StageLights Create(Transform parent)
        {
            var go = new GameObject("StageLights");
            go.transform.SetParent(parent, false);
            var lights = go.AddComponent<StageLights>();
            lights.Setup();
            return lights;
        }

        private void Setup()
        {
            var zoneGo = new GameObject("ZoneMainLight");
            zoneGo.transform.SetParent(transform, false);
            zoneGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            _zoneLight = zoneGo.AddComponent<Light>();
            _zoneLight.type = LightType.Directional;
            _zoneLight.shadows = LightShadows.None; // W2 のプレースホルダを踏襲 (シャドウ演出は対象外)
            _zoneLight.color = Color.white;
            _zoneLight.intensity = 1f;

            var fogGo = new GameObject("FogPlayerPointLight");
            fogGo.transform.SetParent(transform, false);
            _fogPointLight = fogGo.AddComponent<Light>();
            _fogPointLight.type = LightType.Point;
            _fogPointLight.range = FogPointLightRange;
            _fogPointLight.intensity = FogPointLightIntensity;
            _fogPointLight.shadows = LightShadows.None;
            _fogPointLight.enabled = false; // 霧でない時は常時無効 (design 指定)
        }

        /// <summary>ゾーンの主光色/強度と基準環境光を適用する (BoardStage: ゾーン変化時のみ呼ぶ)。</summary>
        public void ApplyZone(ZoneTheme theme)
        {
            _currentTheme = theme;
            _hasTheme = true;

            _zoneLight.color = theme.LightColor;
            _fogPointLight.color = theme.LightColor;
            RefreshDirectionalIntensity();
            RefreshAmbient();
        }

        /// <summary>
        /// 霧ルールの on/off を反映する。階層境界 (Fog開始=21 等) はゾーン境界と一致しないため、
        /// BoardStage はゾーン変化に関わらず毎 Render 呼ぶ想定 (内部で無変化なら早期 return)。
        /// </summary>
        public void SetFog(bool active)
        {
            if (_fogActive == active) return;
            _fogActive = active;

            _fogPointLight.enabled = active;
            RefreshDirectionalIntensity();
            RefreshAmbient();
        }

        /// <summary>霧のポイントライトをプレイヤーの現在地 (BoardStage ローカル座標の接地点) へ追従させる。</summary>
        public void SetFogLightGroundPosition(Vector3 groundPos)
        {
            if (_fogPointLight == null || !_fogPointLight.enabled) return;
            _fogPointLight.transform.localPosition = groundPos + new Vector3(0f, FogPointLightHeight, 0f);
        }

        private void RefreshDirectionalIntensity()
        {
            if (!_hasTheme) return;
            _zoneLight.intensity = _fogActive
                ? _currentTheme.LightIntensity * ZoneThemes.FogDirectionalDimFactor
                : _currentTheme.LightIntensity;
        }

        private void RefreshAmbient()
        {
            if (!_hasTheme) return;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _fogActive
                ? _currentTheme.AmbientColor * ZoneThemes.FogAmbientDimFactor
                : _currentTheme.AmbientColor;
        }
    }
}
