// BoardStage.cs
// Wave 2: GridBoardWidget.cs (uGUI 盤面) のワールド空間版。ワールド原点に 3x3 タイルを
// 配置し、GameScreen からは IBoardView 経由で GridBoardWidget と同じ手順
// (Render/SnapNextRender/FlashPlayer/BurstAtPlayer/Shake/ResetFxState + OnCellTapped/
// OnEnemyTapped) で操作される。GridBoardWidget.cs は変更禁止のため、契約は
// IBoardView.cs 側で吸収する。
//
// 情報パリティ: Render() の CellVisual 組み立ては GridBoardWidget.Render() と
// 1 行単位で同一の手順 (GameSession.PlayerPosition/EnemyPosition/GetAvailableMoves/
// PendingPlayerMove/IsCellVisible/IsCellDisappeared を参照)。可視性判定ロジックの
// 実体は GameSession (Core) に一元化されたままで、本クラスは結果を化粧するだけ。
//
// GridBoardWidget との意図的な差分:
//   - 移動スライド補間・移動ホップ・霧/消失フェードの秒数/イージングは全て同一値を維持。
//   - Shake は画面 XY ではなくワールド接地面 (X, Z) 上の振動に置き換える
//     (盤面がワールド空間の水平面上にあるため)。振幅は px→World 換算 (12px ≒ 0.06)。
//   - BurstAtPlayer は uGUI 疑似パーティクル (FxLayer) の代わりにワールド空間
//     ParticleSystem を使う。speed/count は同じ引数意味 (px/s, 個数) を維持しつつ、
//     内部で px→World 換算してから ParticleSystem に渡す。

using System;
using System.Collections;
using UnityEngine;
using EscapeNine.Core;
using EscapeNine.Runtime.UI;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.Stage
{
    public sealed class BoardStage : MonoBehaviour, IBoardView
    {
        public event Action<int> OnCellTapped;
        public event Action OnEnemyTapped;

        /// <summary>タイル中心間隔 (design 指定)。</summary>
        public const float TileSpacing = 1.1f;

        /// <summary>盤面の中心から外周タイルの端までの距離 (カメラ距離算出用)。</summary>
        public static float BoardHalfExtent =>
            (GameConfig.GridColumns - 1) * TileSpacing * 0.5f + TileView.Footprint * 0.5f;

        /// <summary>px → World 単位換算係数 (design 指定: 12px ≒ 0.06 World 単位)。</summary>
        private const float PxToWorld = 0.06f / 12f;

        private const float MoveDuration = 0.1f; // GridBoardWidget.MoveDuration と同一

        private readonly TileView[] _tiles = new TileView[GameConfig.GridSize + 1]; // 1-indexed
        private PawnView _player;
        private PawnView _enemy;

        private int _enemyPosition = -1;
        private bool _snapNext = true;

        private Vector3 _playerFrom, _playerTo; private float _playerT = 1f;
        private Vector3 _enemyFrom, _enemyTo; private float _enemyT = 1f;

        private Coroutine _shakeRoutine;
        private ParticleSystem _burstParticles;

        /// <summary>盤面座標 (1..9) → ワールド接地座標 (x, 0, z) 中心。</summary>
        public static Vector3 WorldCenterOf(int position)
        {
            int row = GameConfig.RowFromPosition(position);    // 0 = 上段 (2D と同じ)
            int col = GameConfig.ColumnFromPosition(position); // 0 = 左列
            float x = (col - (GameConfig.GridColumns - 1) * 0.5f) * TileSpacing;
            // カメラは +Z を奥 (画面奥/上方向) に見るため、row=0 (上段) を +Z へ、
            // row=最終行 (下段) を -Z (カメラ手前) へ割り当てる。
            float z = ((GameConfig.GridRows - 1) * 0.5f - row) * TileSpacing;
            return new Vector3(x, 0f, z);
        }

        /// <summary>シーンルートに BoardStage を生成する (design: 「Create(Transform)は不要なら static Create()」)。</summary>
        public static BoardStage Create()
        {
            var go = new GameObject("BoardStage");
            var stage = go.AddComponent<BoardStage>();

            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                stage._tiles[pos] = TileView.Create(go.transform, pos, WorldCenterOf(pos));
            }

            stage._player = PawnView.Create(go.transform, "PlayerPawn");
            stage._enemy = PawnView.Create(go.transform, "EnemyPawn");

            CreatePlaceholderLight(go.transform);

            return stage;
        }

        /// <summary>
        /// W2 時点の暫定ライト。design doc の「StageLights (ゾーン別ライト)」は Wave 4 の
        /// 正式デリバラブル (プロジェクトの Environment Lighting 次第では URP Lit マテリアルが
        /// 光源ゼロで真っ黒になり得るため、W2 ゲート (色分けされたタイルの目視確認) が成立しない
        /// リスクを避ける最小限の置き場)。Wave 4 で StageLights に置き換えられる前提の暫定物。
        /// </summary>
        private static void CreatePlaceholderLight(Transform parent)
        {
            var go = new GameObject("PlaceholderDirectionalLight");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.0f;
            light.shadows = LightShadows.None; // W2 時点はシャドウ演出まで踏み込まない
        }

        // ---- IBoardView ----

        public void SnapNextRender()
        {
            _snapNext = true;
        }

        public void Render(GameSession session, bool disabled, Sprite playerSprite, Sprite enemySprite)
        {
            if (session == null)
            {
                RenderEmpty();
                return;
            }

            _enemyPosition = session.EnemyPosition;
            var available = session.GetAvailableMoves();
            int? selected = session.PendingPlayerMove;

            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos].Render(new CellVisual
                {
                    IsPlayer = session.PlayerPosition == pos,
                    IsEnemy = session.EnemyPosition == pos,
                    IsAvailable = available.Contains(pos),
                    IsSelected = selected == pos,
                    IsVisible = session.IsCellVisible(pos),
                    IsDisappeared = session.IsCellDisappeared(pos),
                    Disabled = disabled,
                });
            }

            // プレイヤー: 自分のマスは霧でも常に可視 (IsCellVisible は距離 0 で必ず true)。
            _player.Render(playerSprite, true);
            SetPawnTarget(ref _playerFrom, ref _playerTo, ref _playerT, _player, WorldCenterOf(session.PlayerPosition));

            // 鬼: 霧で見えない位置なら非表示 (Swift/GridBoardWidget: isEnemy && isVisible)。
            bool enemyVisible = session.IsCellVisible(session.EnemyPosition);
            _enemy.Render(enemySprite, enemyVisible);
            SetPawnTarget(ref _enemyFrom, ref _enemyTo, ref _enemyT, _enemy, WorldCenterOf(session.EnemyPosition));

            _snapNext = false;
        }

        private void RenderEmpty()
        {
            _enemyPosition = -1;
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos].Render(new CellVisual { IsVisible = true, Disabled = true });
            }
            _player.Render(null, false);
            _enemy.Render(null, false);
        }

        public void FlashPlayer(Color color, float duration = 0.2f)
        {
            _player.Flash(color, duration);
        }

        public void BurstAtPlayer(Color color, int count = 12, float speed = 600f)
        {
            if (!FxKit.MotionEnabled) return; // FxLayer.BurstAt と同じ Reduce Motion ガード
            EnsureBurstParticles();

            _burstParticles.transform.position = _player.transform.position;

            float worldSpeed = speed * PxToWorld;
            var main = _burstParticles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(worldSpeed * 0.6f, worldSpeed);
            main.startColor = color;
            float gravity = 1600f * PxToWorld; // FxLayer.Gravity (px/s^2) を World 換算
            main.gravityModifier = gravity / Mathf.Max(Mathf.Abs(Physics.gravity.y), 0.01f);

            var emission = _burstParticles.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, 200)) });

            _burstParticles.Clear(true);
            _burstParticles.Play();
        }

        public void Shake(float amplitude = 12f, float duration = 0.3f)
        {
            if (!FxKit.MotionEnabled) return;
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(amplitude * PxToWorld, duration));
        }

        private IEnumerator ShakeRoutine(float worldAmplitude, float duration)
        {
            Vector3 basePos = transform.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float decay = 1f - Mathf.Clamp01(t / duration);
                float ox = UnityEngine.Random.Range(-1f, 1f) * worldAmplitude * decay;
                float oz = UnityEngine.Random.Range(-1f, 1f) * worldAmplitude * decay;
                transform.localPosition = basePos + new Vector3(ox, 0f, oz);
                yield return null;
            }
            transform.localPosition = basePos;
        }

        public void ResetFxState()
        {
            if (_shakeRoutine != null) { StopCoroutine(_shakeRoutine); _shakeRoutine = null; }
            transform.localPosition = Vector3.zero;
            if (_player != null) _player.ResetFx();
            if (_enemy != null) _enemy.ResetFx();
        }

        // ---- タップ振り分け (StageInput から呼ばれる。GridBoardWidget.HandleCellTap と同一ロジック) ----

        public int EnemyPosition => _enemyPosition;

        public bool IsEnemyCollider(Collider collider) => _enemy != null && collider == _enemy.Collider;

        public void HandleCellTap(int position)
        {
            if (position == _enemyPosition) OnEnemyTapped?.Invoke();
            else OnCellTapped?.Invoke(position);
        }

        // ---- 内部実装 ----

        private void SetPawnTarget(ref Vector3 from, ref Vector3 to, ref float t, PawnView pawn, Vector3 target)
        {
            if (_snapNext)
            {
                from = target;
                to = target;
                t = 1f;
                pawn.SetGroundPosition(target);
                return;
            }

            if (target != to)
            {
                from = Vector3.Lerp(from, to, Mathf.Clamp01(t));
                to = target;
                t = 0f;
                pawn.SetGroundPosition(from);
                pawn.PunchHop();
            }
        }

        private void Update()
        {
            Advance(ref _playerFrom, ref _playerTo, ref _playerT, _player);
            Advance(ref _enemyFrom, ref _enemyTo, ref _enemyT, _enemy);

            float dt = Time.deltaTime;
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos].Tick(dt);
            }
        }

        private static void Advance(ref Vector3 from, ref Vector3 to, ref float t, PawnView pawn)
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / MoveDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            pawn.SetGroundPosition(Vector3.Lerp(from, to, eased));
        }

        private void EnsureBurstParticles()
        {
            if (_burstParticles != null) return;

            var go = new GameObject("BurstParticles");
            go.transform.SetParent(transform, false);
            _burstParticles = go.AddComponent<ParticleSystem>();

            var main = _burstParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.55f; // FxLayer.Lifetime と同一
            main.startSize = 22f * PxToWorld; // FxLayer.ShardSize と同一換算
            main.loop = false;
            main.playOnAwake = false;

            var emission = _burstParticles.emission;
            emission.rateOverTime = 0f;

            var shape = _burstParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            var colorOverLifetime = _burstParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = _burstParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // シェーダー未検出時は Standard へフォールバック (完了報告の flag 参照: URP 未セットアップ環境向け保険)。
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            renderer.material = new Material(shader != null ? shader : Shader.Find("Standard"));
        }
    }
}
