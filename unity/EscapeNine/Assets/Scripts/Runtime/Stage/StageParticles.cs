// StageParticles.cs
// Wave 4: ゾーン別のアンビエントパーティクル (盤面全体に漂う雰囲気演出)。
// BoardStage.BurstAtPlayer (W2) と同じ「ParticleSystem をコードのみで構築する」様式を踏襲する。
//
// 溶岩=火の粉(上昇・橙) / 氷洞=雪片(下降・白青) / 紫闇=霊魂(漂う・紫) / 劫火=火の粉大(速い・赤橙)。
// 粒子数は控えめ (maxParticles=40、モバイル配慮)。ParticleSystem は 1 個だけ使い回し、
// ゾーンが変わるたびに main/emission/shape/velocityOverLifetime を再設定する
// (9 タイル + ポーン2 + 背景 + パーティクルでドローコール ≤60 の性能予算に収めるため、
// ゾーンごとに別オブジェクトを生成/破棄しない)。
//
// 情報パリティへの関与なし: 見た目の装飾のみで、可視性判定 (GameSession) には一切触れない。
//
// Reduce Motion: FxKit.MotionEnabled を Update() で毎フレーム監視し、無効化時は
// StopEmittingAndClear で即座に消す (他の Fx と同じ規約)。

using UnityEngine;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StageParticles : MonoBehaviour
    {
        private const int MaxParticles = 40;

        /// <summary>盤面全体を覆うための余白係数 (StageCameraDirector.MarginFactor と同じ考え方)。</summary>
        private const float BoardMargin = 1.15f;

        private ParticleSystem _system;
        private ZoneParticleKind? _currentKind;

        public static StageParticles Create(Transform parent)
        {
            var go = new GameObject("StageParticles");
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<StageParticles>();
            particles.Setup();
            return particles;
        }

        private void Setup()
        {
            _system = gameObject.AddComponent<ParticleSystem>();

            var main = _system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 盤シェイクに自然に追従させる

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            var renderer = _system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // シェーダー未検出時は Standard へフォールバック (BoardStage.EnsureBurstParticles と同じ保険)。
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            // フォールバックは URP/Unlit (Standard は URP 下でマゼンタ。両方 AlwaysIncludedShaders 登録済み)。
            renderer.material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
        }

        private void OnDestroy()
        {
            // 実行時生成した Material を明示破棄する (Editor の leaked material 警告防止、2026-07-04 C7)。
            if (_system != null)
            {
                var r = _system.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial);
            }
        }

        /// <summary>
        /// Wave 5: 品質ティアによる上限粒子数の適用 (StageQuality.Apply から呼ばれる)。
        /// 0 を指定すると emission モジュール自体を無効化し (Low ティア: 「パーティクル 0」を
        /// maxParticles=0 のクランプだけに頼らず確実にする)、SetZone() が後から
        /// rateOverTime を再設定しても emission.enabled は変えないため無効のまま維持される。
        /// </summary>
        public void ApplyQuality(int maxParticles)
        {
            int clamped = Mathf.Max(0, maxParticles);
            var main = _system.main;
            main.maxParticles = clamped;
            var emission = _system.emission;
            emission.enabled = clamped > 0;
        }

        /// <summary>ゾーンのパーティクル種別を切り替える (BoardStage: ゾーン変化時のみ呼ぶ)。</summary>
        public void SetZone(ZoneParticleKind kind)
        {
            if (_currentKind == kind) return;
            _currentKind = kind;
            ConfigureForKind(kind);
        }

        private void ConfigureForKind(ZoneParticleKind kind)
        {
            float halfExtent = BoardStage.BoardHalfExtent * BoardMargin;
            float boxSize = halfExtent * 2f;

            var main = _system.main;
            var emission = _system.emission;
            var shape = _system.shape;
            var vel = _system.velocityOverLifetime;
            var noise = _system.noise;

            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            noise.enabled = false; // 既定は無効。霊魂 (Spirits) のみ有効化する。

            switch (kind)
            {
                case ZoneParticleKind.Embers: // 溶岩ゾーン: 火の粉、地面付近から上昇、橙
                    transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    shape.scale = new Vector3(boxSize, 0.05f, boxSize);
                    main.startColor = new Color(1f, 0.55f, 0.2f, 0.75f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                    emission.rateOverTime = 6f;
                    vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
                    vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
                    break;

                case ZoneParticleKind.Snow: // 氷洞ゾーン: 雪片、盤面上空から降下、白青
                    transform.localPosition = new Vector3(0f, 2.6f, 0f);
                    shape.scale = new Vector3(boxSize, 0.05f, boxSize);
                    main.startColor = new Color(0.88f, 0.94f, 1f, 0.85f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 4.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
                    emission.rateOverTime = 8f;
                    vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                    vel.y = new ParticleSystem.MinMaxCurve(-0.35f, -0.18f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                    break;

                case ZoneParticleKind.Spirits: // 骸骨ゾーン: 霊魂、盤面中空を漂う、紫
                    transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    shape.scale = new Vector3(boxSize, 1.2f, boxSize);
                    main.startColor = new Color(0.72f, 0.45f, 0.95f, 0.45f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                    emission.rateOverTime = 4f;
                    vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
                    vel.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.05f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
                    noise.enabled = true;
                    noise.strength = 0.12f;
                    noise.frequency = 0.25f;
                    break;

                case ZoneParticleKind.BigEmbers: // ドラゴンゾーン: 火の粉大、速く上昇、赤橙
                    transform.localPosition = new Vector3(0f, 0.05f, 0f);
                    shape.scale = new Vector3(boxSize, 0.05f, boxSize);
                    main.startColor = new Color(1f, 0.35f, 0.12f, 0.9f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                    emission.rateOverTime = 10f;
                    vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                    vel.y = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                    break;
            }

            _system.Clear(true);
            if (FxKit.MotionEnabled) _system.Play();
        }

        private void Update()
        {
            if (!FxKit.MotionEnabled)
            {
                if (_system.isPlaying) _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            if (_currentKind.HasValue && !_system.isPlaying) _system.Play();
        }
    }
}
