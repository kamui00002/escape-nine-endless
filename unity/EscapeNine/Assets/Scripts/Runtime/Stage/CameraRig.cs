// CameraRig.cs
// Wave 3: 自作カメラワーク (docs/unity-phase4-5-visual-upgrade-design.md W3, D2 決定:
// Cinemachine 不使用・自作 ~150 行)。StageCameraDirector が毎 LateUpdate で確定する
// カメラの基本姿勢 (Cam.transform.position / rotation / fieldOfView) に対し、本コンポーネントは
// 「加算オフセット」を後段で乗せるだけの薄いレイヤー:
//   fieldOfView += 圧迫ズームのデルタ (負値、高階層ほど寄る)
//   position    += 衝突/被弾インパルスのランダム減衰オフセット (ワールド単位)
//   position/rotation を Y 軸回りに回転 = 階層クリア時の盤の周りの回り込み
// StageCameraDirector 自体は 1 行も変更しない (読み取り専用の後乗せ)。
//
// 実行順序の担保: StageCameraDirector には ExecutionOrder 指定が無い (既定 0)。
// 本コンポーネントに [DefaultExecutionOrder] で正の値を明示することで、Unity の
// 「同フェーズ内は ExecutionOrder の小さい順に呼ぶ」仕様により、GameObject 階層や
// コンポーネント追加順に関わらず毎フレーム確実に StageCameraDirector.LateUpdate() の
// "後" に本コンポーネントの LateUpdate() が走る (StageCameraDirector 側の変更は不要)。
//
// アタッチ位置: GameScreen.BuildWorldBoard() で StageRenderView/StageInput と同様に
// _boardStage.gameObject へ AddComponent する想定 (BoardStage と生死・表示/非表示を共にする
// ことで、他画面表示中に演出が残留しない)。カメラ自体は Camera.main 上の別 GameObject に
// 存在するため、Configure(Camera) で参照を受け取る (StageRenderView.Configure と同じ形)。
//
// Time.timeScale=0 (FxKit.HitStop 中) でも破綻しないよう、全ての経過時間計算に
// Time.unscaledDeltaTime / Time.unscaledTime を使う (FxKit と同じ流儀)。
//
// Reduce Motion: FxKit.MotionEnabled が false の間は 3 機能とも「即座に 0 オフセット」
// (LateUpdate 側の最終ゲートで一括保証。設定がアニメーション途中で切り替わっても安全)。
// 圧迫ズームのみ、階層に紐づく「一時的でない」状態として OnDisable でも値を保持する
// (Impulse/Orbit は一過性の演出なので OnDisable で必ず 0 に戻す) — 詳細は各メソッド参照。

using System.Collections;
using UnityEngine;
using EscapeNine.Core;
using EscapeNine.Runtime.UI.Fx;

namespace EscapeNine.Runtime.Stage
{
    [DefaultExecutionOrder(100)] // StageCameraDirector (既定 0) より後に LateUpdate させる
    public sealed class CameraRig : MonoBehaviour
    {
        // ---- 圧迫ズーム (floor 1→100 で線形、最大 -4°、1 秒かけて滑らかに) ----
        private const float MaxPressureZoomDegrees = 4f;
        private const float PressureZoomLerpDuration = 1f;

        // ---- インパルス (指数減衰 ~0.35s で実質ゼロ) ----
        private const float ImpulseDecayDuration = 0.35f;
        private const float ImpulseDecayRate = 5f; // exp(-5) ≈ 0.0067 を ImpulseDecayDuration 時点で迎える

        // ---- 階層クリアの回り込み (20°、0.8 秒でイージング付き往復) ----
        private const float OrbitDegrees = 20f;
        private const float OrbitDuration = 0.8f;

        // ---- デバイス傾きパララックス (2026-07-06 オーナー案: iPhone を左右に傾けると盤面ジオラマが
        // 盤面中心まわりに回り、"箱を横から覗き込む" 3D 効果。前後傾きは端末の持ち角の個人差で
        // 基準がブレるため v1 は左右(accel.x)のみ。MotionEnabled=false なら LateUpdate 最終ゲートで無効) ----
        // 2026-07-06 ±6°→±16°→±24°→±34°。2026-07-07 第5版: 重ねての「もっと派手に」で、ヨーを更に上げると
        // マスが画面端で切れるため、代わりに前後傾き(ピッチ)を足して2軸化=「箱を覗き込む」立体感を増強。
        private const float TiltMaxYawDegrees = 38f;   // 左右傾きで盤面中心まわりに最大 ±38° ヨー
        private const float TiltMaxSway = 1.7f;         // 併せてカメラを右方向へ最大 ±1.7 ユニット平行移動 (視差)
        private const float TiltMaxPitchDegrees = 18f; // 前後傾きで盤面中心まわりに最大 ±18° ピッチ (上下の覗き込み)
        private const float PitchGain = 1.5f;           // accel.z の変化に対するピッチ感度 (基準からの差分に乗算)
        private const float TiltSmoothing = 9f;         // 目標へ寄せる速さ (大=機敏 / 小=ゆったり)。フレームレート非依存
        // 2026-07-09 オーナー「揺れが斜めで気持ち悪い」→ 優勢軸ブレンド。ヨーとピッチが同時に効くと合成が
        // 斜めになるため、片軸が優勢な時はもう片方を最大 AxisDominance ぶん減衰させ、水平 or 垂直へ寄せる
        // (両軸が同程度=意図的な斜めの時だけ両方効く)。0=無効(常に両軸) / 1=弱い方を完全に殺す。
        private const float AxisDominance = 0.8f;

        private Camera _cam;

        // 傾き: 平滑化した現在のヨー角 (度、Input.acceleration.x 由来、毎フレーム再構築で drift しない)
        private float _tiltYaw;

        private float _tiltPitch;

        // 傾きの neutral 基準 (accel の x=左右 / z=前後)。「縦持ち直立なら accel.x=0」を前提にすると、
        // 実際は皆スマホを少し傾けて持つため常時オフセット (= 右にずっと傾く) が出る。そこで盤面表示開始時の
        // 「その人の持ち角」を一度だけ取り込んで 0 とし、以後その差分で駆動する (yaw/pitch 共通、hold-to-peek)。
        private float _yawBaseline;
        private float _pitchBaseline;
        private bool _tiltCalibrated;

        // 圧迫ズーム: 現在値/目標値と補間進捗 (階層に紐づく持続状態、OnDisable でも保持する)
        private float _zoomCurrentDelta;
        private float _zoomTargetDelta;
        private float _zoomLerpFrom;
        private float _zoomLerpT = 1f;

        // インパルス: 現在のワールド空間オフセット (一過性、OnDisable で 0 に戻す)
        private Vector3 _impulseOffset;
        private Coroutine _impulseRoutine;

        // 回り込み: 現在の追加ヨー角 (度、一過性、OnDisable で 0 に戻す)
        private float _orbitYawDelta;
        private Coroutine _orbitRoutine;

        /// <summary>後乗せ対象のカメラを渡す (StageRenderView.Configure と同じパターン)。</summary>
        public void Configure(Camera cam)
        {
            _cam = cam;
        }

        private void OnDisable()
        {
            // BoardStage が非活性化される (画面切替) 際、一過性の演出を持ち越さない。
            // 圧迫ズームは「現在の階層に紐づく持続状態」なので意図的に保持する
            // (再表示時に GameScreen が PressureZoom を呼び直さなくても同じ圧迫具合が続く)。
            if (_impulseRoutine != null) { StopCoroutine(_impulseRoutine); _impulseRoutine = null; }
            _impulseOffset = Vector3.zero;

            if (_orbitRoutine != null) { StopCoroutine(_orbitRoutine); _orbitRoutine = null; }
            _orbitYawDelta = 0f;

            // 傾き基準を取り直す (次に盤面を表示した時の持ち角を新たな neutral にする)。
            _tiltCalibrated = false;
            _tiltYaw = 0f;
            _tiltPitch = 0f;
        }

        // ---- 公開 API (3 機能) ----

        /// <summary>衝突/被弾時の減衰付きランダム揺れ (ワールド単位)。</summary>
        public void Impulse(float strength)
        {
            if (!isActiveAndEnabled) return; // StartCoroutine は非活性時に例外を投げるため防御
            if (!FxKit.MotionEnabled) return; // 即 0 オフセット (呼び出し自体を無視)
            if (_impulseRoutine != null) StopCoroutine(_impulseRoutine);
            _impulseRoutine = StartCoroutine(ImpulseRoutine(strength));
        }

        private IEnumerator ImpulseRoutine(float strength)
        {
            float t = 0f;
            while (t < ImpulseDecayDuration)
            {
                t += Time.unscaledDeltaTime;
                float decay = Mathf.Exp(-ImpulseDecayRate * t / ImpulseDecayDuration);
                _impulseOffset = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f)) * (strength * decay);
                yield return null;
            }
            _impulseOffset = Vector3.zero;
            _impulseRoutine = null;
        }

        /// <summary>階層クリア時に盤の周りを Y 軸回りに 20° 回り込み、イージング付きで戻る。</summary>
        public void OrbitOnFloorClear()
        {
            if (!isActiveAndEnabled) return; // StartCoroutine は非活性時に例外を投げるため防御
            if (!FxKit.MotionEnabled) return; // 即 0 オフセット (呼び出し自体を無視)
            if (_orbitRoutine != null) StopCoroutine(_orbitRoutine);
            _orbitRoutine = StartCoroutine(OrbitRoutine());
        }

        private IEnumerator OrbitRoutine()
        {
            float half = OrbitDuration * 0.5f;

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                _orbitYawDelta = Mathf.LerpUnclamped(0f, OrbitDegrees, EaseOutQuad(Mathf.Clamp01(t / half)));
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                _orbitYawDelta = Mathf.LerpUnclamped(OrbitDegrees, 0f, EaseOutQuad(Mathf.Clamp01(t / half)));
                yield return null;
            }
            _orbitYawDelta = 0f;
            _orbitRoutine = null;
        }

        /// <summary>高階層ほど fov を最大 -4° まで詰める圧迫感 (floor 1→100 で線形、1s かけて滑らかに)。</summary>
        public void PressureZoom(int floor)
        {
            float ratio = Mathf.Clamp01((float)(floor - 1) / (GameConfig.MaxFloors - 1));
            float target = -MaxPressureZoomDegrees * ratio;
            if (Mathf.Approximately(target, _zoomTargetDelta)) return;

            _zoomLerpFrom = _zoomCurrentDelta;
            _zoomTargetDelta = target;
            _zoomLerpT = 0f;
        }

        // ---- LateUpdate: StageCameraDirector が確定した基本姿勢に加算オフセットを乗せる ----

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (!FxKit.MotionEnabled) return; // 最終ゲート: 3 機能とも 0 オフセット (Director の基準姿勢のまま)

            // 圧迫ズーム (fov に加算)
            if (_zoomLerpT < 1f)
            {
                _zoomLerpT = Mathf.Min(1f, _zoomLerpT + Time.unscaledDeltaTime / PressureZoomLerpDuration);
                _zoomCurrentDelta = Mathf.Lerp(_zoomLerpFrom, _zoomTargetDelta, EaseOutQuad(_zoomLerpT));
            }
            _cam.fieldOfView = Mathf.Max(1f, _cam.fieldOfView + _zoomCurrentDelta);

            // 階層クリアの回り込み (position/rotation を原点中心に Y 軸回転)
            if (Mathf.Abs(_orbitYawDelta) > 0.0001f)
            {
                var orbit = Quaternion.AngleAxis(_orbitYawDelta, Vector3.up);
                _cam.transform.position = orbit * _cam.transform.position;
                _cam.transform.rotation = orbit * _cam.transform.rotation;
            }

            // デバイス傾きパララックス: 左右傾き(accel.x)を盤面中心まわりのヨーへ平滑化して適用。
            // 盤面表示開始時の accel を neutral として一度取り込み (yaw=x / pitch=z を同時)、以後その差分で駆動。
            // これで「持ち角ぶんの常時オフセット (= 右にずっと傾く)」を打ち消す。符号は「傾けた側から覗く」感。
            // エディタ(accel=0)では無反応=実機専用の演出。毎フレーム基準姿勢から作り直すので drift しない。
            if (!_tiltCalibrated)
            {
                _yawBaseline = Input.acceleration.x;
                _pitchBaseline = Input.acceleration.z;
                _tiltCalibrated = true;
            }
            // 生の傾き入力を正規化 (-1..1) してから優勢軸ブレンド。片軸が優勢な時にもう片方を減衰させ、
            // ヨー+ピッチの同時発火で生じる「斜め揺れ」(オーナー: 気持ち悪い) を抑えて水平 or 垂直へ寄せる。
            float rawYaw = Mathf.Clamp(-(Input.acceleration.x - _yawBaseline), -1f, 1f);
            float rawPitch = Mathf.Clamp((Input.acceleration.z - _pitchBaseline) * PitchGain, -1f, 1f);
            float yawMag = Mathf.Abs(rawYaw);
            float pitchMag = Mathf.Abs(rawPitch);
            rawYaw *= 1f - AxisDominance * Mathf.Clamp01(pitchMag - yawMag);   // ピッチ優勢時はヨーをフェード
            rawPitch *= 1f - AxisDominance * Mathf.Clamp01(yawMag - pitchMag); // ヨー優勢時はピッチをフェード

            float targetYaw = rawYaw * TiltMaxYawDegrees;
            _tiltYaw = Mathf.Lerp(_tiltYaw, targetYaw, 1f - Mathf.Exp(-TiltSmoothing * Time.unscaledDeltaTime));
            if (Mathf.Abs(_tiltYaw) > 0.0001f)
            {
                var tilt = Quaternion.AngleAxis(_tiltYaw, Vector3.up);
                _cam.transform.position = tilt * _cam.transform.position;
                _cam.transform.rotation = tilt * _cam.transform.rotation;
                // 視差: 回転に加えカメラをローカル右方向へ平行移動して「覗き込み」感を強める
                float swayFrac = _tiltYaw / TiltMaxYawDegrees; // [-1,1]
                _cam.transform.position += _cam.transform.right * (swayFrac * TiltMaxSway);
            }

            // 前後傾き(ピッチ): 盤面表示開始時の accel.z を neutral として一度取り込み、以後その差分で
            // 上下の覗き込み。ヨーの後に (post-yaw の) カメラ右軸まわりに原点中心で回すことで、
            // 「箱を上/下から覗き込む」2軸目の立体感を出す。
            float targetPitch = rawPitch * TiltMaxPitchDegrees;
            _tiltPitch = Mathf.Lerp(_tiltPitch, targetPitch, 1f - Mathf.Exp(-TiltSmoothing * Time.unscaledDeltaTime));
            if (Mathf.Abs(_tiltPitch) > 0.0001f)
            {
                var pitch = Quaternion.AngleAxis(_tiltPitch, _cam.transform.right);
                _cam.transform.position = pitch * _cam.transform.position;
                _cam.transform.rotation = pitch * _cam.transform.rotation;
            }

            // インパルス (position に加算)
            _cam.transform.position += _impulseOffset;
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    }
}
