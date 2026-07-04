// StagePostFx.cs
// Wave 3 (a): URP Volume (Bloom / Vignette / ColorAdjustments) をコードのみで構築する。
// docs/unity-phase4-5-visual-upgrade-design.md W3 の「シーン非依存の規約」(UrpBootstrap.cs の
// Graphics/Quality 割当と同じ思想: .asset を repo に増やさず実行時生成で完結させる) に従う。
//
// 基準値は public const にまとめ、BeatVolumePulse がここからの倍率で脈動させ、
// Wave 4 の ZoneThemes がゾーン別に上書きできるようにする (本 Wave では上書き経路は未実装)。
//
// Depth of Field は今回は追加しない (D7: モバイル全面オフ)。デスクトップ専用の追加は
// Wave 5 の品質ティアで検討する — ここが「居場所」(profile.Add<DepthOfField>(true) を
// 品質ティア判定の下で足す形になる想定)。

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StagePostFx : MonoBehaviour
    {
        // ---- 基準値 (design 指定)。BeatVolumePulse / 将来の ZoneThemes がここを参照する。 ----
        public const float BloomIntensityBase = 0.8f;
        public const float BloomThresholdBase = 0.9f;
        public const float BloomScatterBase = 0.6f;
        public const float VignetteIntensityBase = 0.25f;
        public const float ColorAdjustmentsPostExposureBase = 0f;

        public Bloom Bloom { get; private set; }
        public Vignette Vignette { get; private set; }
        public ColorAdjustments ColorAdjustments { get; private set; }
        public VolumeProfile Profile { get; private set; }
        public Volume Volume { get; private set; }

        /// <summary>
        /// parent の子として Volume 一式を生成する (design: 「Create(Transform)は不要なら static Create()」
        /// パターンを BoardStage.Create() から踏襲)。camera が非 null ならポストプロセスを有効化する
        /// (StageCameraDirector.EnsureOnMainCamera が Camera.main 不在で null を返すケースに合わせ、
        /// null 許容にして BuildUI 自体はクラッシュさせない)。
        /// </summary>
        public static StagePostFx Create(Transform parent, Camera camera)
        {
            var go = new GameObject("StagePostFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<StagePostFx>();
            fx.Setup(camera);
            return fx;
        }

        private void Setup(Camera camera)
        {
            Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Profile.name = "StagePostFxProfile"; // 実行時生成のみ。.asset 化はしない (design 指定)。

            Bloom = Profile.Add<Bloom>(true);
            Bloom.intensity.value = BloomIntensityBase;
            Bloom.threshold.value = BloomThresholdBase;
            Bloom.scatter.value = BloomScatterBase;

            Vignette = Profile.Add<Vignette>(true);
            Vignette.intensity.value = VignetteIntensityBase;

            ColorAdjustments = Profile.Add<ColorAdjustments>(true);
            ColorAdjustments.postExposure.value = ColorAdjustmentsPostExposureBase;

            // Depth of Field はここでは追加しない (D7: モバイル全面オフ)。
            // デスクトップ専用ティアを足す時は、品質ティア判定の下で
            // `Profile.Add<DepthOfField>(true)` をここに追加する想定。

            Volume = gameObject.AddComponent<Volume>();
            Volume.isGlobal = true;
            Volume.priority = 0f;
            Volume.weight = 1f;
            Volume.sharedProfile = Profile;

            if (camera != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        private void OnDestroy()
        {
            // 実行時生成の ScriptableObject は GC 任せにせず明示的に解放する
            // (StageRenderView.ReleaseRt と同じ「後始末を必ず書く」規約)。
            if (Profile != null)
            {
                Destroy(Profile);
                Profile = null;
            }
        }
    }
}
