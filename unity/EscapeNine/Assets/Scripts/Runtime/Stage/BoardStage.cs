// BoardStage.cs
// Wave 2: 旧 uGUI 盤面 GridBoardWidget.cs のワールド空間版 (旧 uGUI 盤面は W5 で削除済み
// (D4)。以下の GridBoardWidget への言及は移植元 = Swift GridBoardView.swift 相当の記録)。
// ワールド原点に 3x3 タイルを配置し、GameScreen からは IBoardView 経由の描画契約
// (Render/SnapNextRender/FlashPlayer/BurstAtPlayer/Shake/ResetFxState + OnCellTapped/
// OnEnemyTapped) で操作される。現在は本クラスが IBoardView の唯一の実装。
//
// 情報パリティ: Render() の CellVisual 組み立ては旧 GridBoardWidget.Render() と
// 1 行単位で同一の手順 (GameSession.PlayerPosition/EnemyPosition/GetAvailableMoves/
// PendingPlayerMove/IsCellVisible/IsCellDisappeared を参照)。可視性判定ロジックの
// 実体は GameSession (Core) に一元化されたままで、本クラスは結果を化粧するだけ。
//
// 旧 GridBoardWidget との意図的な差分:
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

        private const float MoveDuration = 0.1f; // 旧 GridBoardWidget.MoveDuration と同一値

        private readonly TileView[] _tiles = new TileView[GameConfig.GridSize + 1]; // 1-indexed
        private PawnView _player;
        private PawnView _enemy;

        private int _enemyPosition = -1;
        private bool _snapNext = true;

        private Vector3 _playerFrom, _playerTo; private float _playerT = 1f;
        private Vector3 _enemyFrom, _enemyTo; private float _enemyT = 1f;

        private Coroutine _shakeRoutine;
        private ParticleSystem _burstParticles;

        // ---- Wave 4: ゾーンテーマ統合 ----
        private StageLights _stageLights;

        // Phase 5c 修正: ボステレグラフの明滅は拍 (Conductor.SongPositionBeats = dspTime 由来) で駆動する。
        // 旧実装は TileView が Time.deltaTime を秒累積して明滅しており、設計 §1「演出は全て Conductor から
        // 駆動・Time.time 禁止」に反していた (低フレームレート機で音とズレる)。GameScreen が Configure で注入する。
        private Conductor _conductor;

        /// <summary>拍同期演出 (テレグラフ) 用に Conductor を注入する (GameScreen から)。</summary>
        public void SetConductor(Conductor conductor) => _conductor = conductor;
        private StageParticles _stageParticles;
        private StagePostFx _postFx; // 遅延取得 (GameScreen が BoardStage の子として生成するため)
        private int _currentZoneIndex = -1;

        /// <summary>Wave 5: GameScreen が StageQuality.Apply へ渡すための公開アクセサ。</summary>
        public StageParticles Particles => _stageParticles;

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

            // Wave 4: W2 の暫定 Directional Light (旧 CreatePlaceholderLight) は StageLights へ移管。
            stage._stageLights = StageLights.Create(go.transform);
            stage._stageParticles = StageParticles.Create(go.transform);

            return stage;
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

            ApplyZoneAndFog(session);
            ApplyBossTelegraph(session); // Phase 5c: ボスパターンのテレグラフ (タイル Render 前に確定させる)

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

        /// <summary>
        /// Phase 5c: ボス階のパターンに応じてタイルへテレグラフ (予告/赤熱) を設定する (§1.5/§5)。
        /// - Intimidation: TemporaryBossZone のマスを赤熱 (進入不可の予告)。
        /// - Foresight: GameSession に「予測先マス」を出す公開手段が無いため、正直な縮退として
        ///   ボス (敵) 隣接マスを青白く明滅させるに留める (§5.1② 設計注記どおり)。
        /// - Pursuit: テレグラフなし。
        /// 情報パリティ (§2 絶対制約): 霧で見えないマスにはテレグラフを出さない (敵位置の間接漏洩防止)。
        /// </summary>
        private void ApplyBossTelegraph(GameSession session)
        {
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos].SetBossTelegraph(TileView.BossTelegraphKind.None);
            }

            if (!session.IsBossFloor) return;

            switch (session.CurrentBossPattern)
            {
                case BossPattern.Intimidation:
                    foreach (int p in session.TemporaryBossZone)
                    {
                        if (p >= 1 && p <= GameConfig.GridSize && session.IsCellVisible(p))
                        {
                            _tiles[p].SetBossTelegraph(TileView.BossTelegraphKind.Intimidation);
                        }
                    }
                    break;

                case BossPattern.Foresight:
                    foreach (int p in GameEngine.GetAvailableMoves(session.EnemyPosition))
                    {
                        if (p >= 1 && p <= GameConfig.GridSize && session.IsCellVisible(p))
                        {
                            _tiles[p].SetBossTelegraph(TileView.BossTelegraphKind.Foresight);
                        }
                    }
                    break;

                // Pursuit: テレグラフなし
            }
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

            // Wave 4: 霧のポイントライトをプレイヤーの実際の (スライド補間中の) 接地位置へ
            // 毎フレーム追従させる (design: 「プレイヤー移動に追従」)。フォグ非活性時は
            // StageLights.SetFogLightGroundPosition が内部で無効光源チェックにより早期 return する。
            Vector3 playerGround = _player.transform.localPosition;
            playerGround.y = 0f;
            _stageLights.SetFogLightGroundPosition(playerGround);

            float dt = Time.deltaTime;
            // テレグラフ明滅は拍位相で駆動 (dt は崩落/フォグの MoveTowards 用に別途渡す)。
            // 未再生時 (SongPositionBeats==0) やポーズ中は位相が進まず定常表示になる。
            float beatPhase = (float)(_conductor != null ? _conductor.SongPositionBeats : 0.0);
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos].Tick(dt, beatPhase);
            }
        }

        /// <summary>
        /// ゾーン (階層帯) の見た目を一括適用する。ゾーン自体の変更 (主光色/環境光ベース/
        /// パーティクル種別/カメラ背景/Bloom ティント) はゾーンが実際に変わった時だけ行う
        /// (design 指定: 「フロア毎に毎回でなくゾーン変化時のみ適用」)。
        /// 一方、霧の on/off は階層境界 (Fog開始=Floor21 等) がゾーン境界 (25/50/75) と
        /// 一致しないため、ゾーン変化とは独立に毎 Render 反映する
        /// (StageLights.SetFog は内部で無変化なら早期 return するため、無駄な RenderSettings
        /// 書き込みは発生しない)。
        /// </summary>
        private void ApplyZoneAndFog(GameSession session)
        {
            ZoneTheme theme = ZoneThemes.ForFloor(session.CurrentFloor);
            bool fogActive = session.CurrentSpecialRule == SpecialRule.Fog
                || session.CurrentSpecialRule == SpecialRule.FogDisappear;

            if (theme.ZoneIndex != _currentZoneIndex)
            {
                _currentZoneIndex = theme.ZoneIndex;

                TileView.ZoneGridTint = theme.TileTint;
                _stageLights.ApplyZone(theme);
                _stageParticles.SetZone(theme.Particle);
                StageCameraDirector.ZoneBackgroundOverride = theme.CameraBackgroundColor;

                // StagePostFx は GameScreen.BuildWorldBoard() が BoardStage の子として生成する
                // (GameScreen.cs は変更禁止のため、注入ではなくここで遅延取得する。design 指定)。
                if (_postFx == null) _postFx = GetComponentInChildren<StagePostFx>();
                if (_postFx != null && _postFx.Bloom != null) _postFx.Bloom.tint.value = theme.BloomTint;
            }

            _stageLights.SetFog(fogActive);
        }

        private static void Advance(ref Vector3 from, ref Vector3 to, ref float t, PawnView pawn)
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / MoveDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            pawn.SetGroundPosition(Vector3.Lerp(from, to, eased));
        }

        private void OnDestroy()
        {
            // 実行時生成した Material を明示破棄する (Editor の leaked material 警告防止、2026-07-04 C6/C7)。
            for (int pos = 1; pos <= GameConfig.GridSize; pos++)
            {
                _tiles[pos]?.DestroyMaterials();
            }
            if (_burstParticles != null)
            {
                var r = _burstParticles.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.material != null) Destroy(r.material);
            }
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
