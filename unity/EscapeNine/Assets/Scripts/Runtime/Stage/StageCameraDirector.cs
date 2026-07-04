// StageCameraDirector.cs
// Wave 2: Camera.main をランタイムで perspective 化し、BoardStage (原点中心の 3x3 タイル)
// が現在の camera.aspect (= StageViewportSync が同期した camera.rect から自動導出される
// ビューポート比率) に収まるよう距離・角度を毎フレーム再計算する。
//
// MainSceneBuilder が生成するシーンの Camera は元々 orthographic + SolidColor クリア
// (UI 用の背景色クリアのみが目的、docs コメント参照)。他画面表示中もこのカメラを
// perspective のまま残してよい (design 指定): Canvas が全画面を覆うため見た目には
// 影響しない。camera.rect のフル復帰だけは GameScreen.OnHide が担う。
//
// カメラ距離の算出 (数式の説明は完了報告に記載。要点):
//   1. 垂直画角の上端/下端レイが接地面 (y=0) と交わる 2 点の深度 (forward 軸上) を求め、
//      盤面半径 (BoardHalfExtent × 余白係数) を覆うために必要な「見下ろし距離」を出す
//      (チルトによる遠近非対称を考慮: 遠方エッジと近傍エッジで別々に距離条件を立て、
//      厳しい方 (=より大きい距離を要求する方) を採用する)。
//   2. 別途、水平画角 (aspect から導出) だけで必要な距離も求める。
//   3. 1 と 2 の大きい方を最終距離として採用する (両条件を満たす最小の余裕距離)。
// この式は「接地面が水平で、盤面が原点中心の正方形」という前提の近似であり、
// 極端なアスペクト比や tilt では余白が均一にならない可能性がある
// (完了報告の不確実点 flag 参照)。

using UnityEngine;
using EscapeNine.Runtime.UI;

namespace EscapeNine.Runtime.Stage
{
    public sealed class StageCameraDirector : MonoBehaviour
    {
        /// <summary>俯瞰角 (水平からの見下ろし角度、度)。オーケストレータが 30/35/40 を比較できるよう public static。</summary>
        public static float TiltDegrees = 35f;

        /// <summary>
        /// Wave 4: ゾーン別カメラ背景色の上書き (BoardStage.ApplyZoneAndFog がゾーン変化時に設定)。
        /// null の間は従来どおり UITheme.Background を使う (後方互換、既定 null)。
        /// 本フィールドが必要な理由: このクラスが毎 LateUpdate で Cam.backgroundColor を
        /// 無条件に上書きするため、他の場所 (BoardStage の Update 等) から一度設定しても
        /// 同一フレームの LateUpdate で必ず巻き戻ってしまう。ここに「現在のゾーン背景色」を
        /// 持たせることで、ApplyFraming() が読む値そのものを差し替える。
        /// </summary>
        public static Color? ZoneBackgroundOverride;

        /// <summary>垂直画角 (度、design 指定)。</summary>
        public const float FieldOfViewDegrees = 30f;

        /// <summary>盤面ぴったりではなく余白を持たせる係数。</summary>
        private const float MarginFactor = 1.15f;

        /// <summary>チルト角が画角の半分未満にならないようにする安全下限 (度)。</summary>
        private const float MinTopAngleDegrees = 1f;

        public Camera Cam { get; private set; }

        /// <summary>Camera.main に本コンポーネントを 1 個だけ付与する。既存があればそれを返す。</summary>
        public static StageCameraDirector EnsureOnMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[StageCameraDirector] Camera.main が見つからない (MainSceneBuilder でシーンを生成したか確認すること)");
                return null;
            }

            var existing = cam.GetComponent<StageCameraDirector>();
            if (existing != null) return existing;

            var director = cam.gameObject.AddComponent<StageCameraDirector>();
            director.Cam = cam;
            return director;
        }

        private void Awake()
        {
            if (Cam == null) Cam = GetComponent<Camera>();
        }

        // StageViewportSync.Update() (camera.rect / aspect 更新) の後に読むため LateUpdate。
        // Unity は全 Update() 完了後に全 LateUpdate() を呼ぶため、コンポーネント間の
        // 実行順設定なしでも本フレーム内の最新 aspect を安全に参照できる。
        private void LateUpdate()
        {
            ApplyFraming();
        }

        /// <summary>カメラの姿勢を即座に再計算する (OnShow 直後の 1 フレーム目のズレ防止用に公開)。</summary>
        public void ApplyFraming()
        {
            if (Cam == null) return;

            Cam.orthographic = false;
            Cam.fieldOfView = FieldOfViewDegrees;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = ZoneBackgroundOverride ?? UITheme.Background;

            float tiltDeg = Mathf.Clamp(TiltDegrees, MinTopAngleDegrees + FieldOfViewDegrees * 0.5f + 0.01f, 89f);
            float tiltRad = tiltDeg * Mathf.Deg2Rad;
            float halfFovV = (FieldOfViewDegrees * 0.5f) * Mathf.Deg2Rad;
            float halfExtent = BoardStage.BoardHalfExtent * MarginFactor;

            float topAngle = Mathf.Max(tiltRad - halfFovV, MinTopAngleDegrees * Mathf.Deg2Rad);
            float botAngle = tiltRad + halfFovV;

            float rTop = Cot(topAngle) - Cot(tiltRad);   // 遠方エッジ (画面奥) の比率
            float rBot = Cot(tiltRad) - Cot(botAngle);   // 近傍エッジ (画面手前) の比率
            float minRatio = Mathf.Min(rTop, rBot);
            float dVertical = minRatio > 0.0001f ? halfExtent / minRatio : halfExtent * 4f;

            float aspect = Cam.aspect > 0f ? Cam.aspect : 1f;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * aspect);
            float dHorizontal = halfFovH > 0.0001f ? halfExtent / Mathf.Tan(halfFovH) : dVertical;

            float distance = Mathf.Max(dVertical, dHorizontal);

            float h = distance * Mathf.Sin(tiltRad);
            float z = distance * Mathf.Cos(tiltRad);

            Cam.transform.position = new Vector3(0f, h, -z);
            Cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -h, z).normalized, Vector3.up);
        }

        private static float Cot(float angleRad) => 1f / Mathf.Tan(angleRad);
    }
}
