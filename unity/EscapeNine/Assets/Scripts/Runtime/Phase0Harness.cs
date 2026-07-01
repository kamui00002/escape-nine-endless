// Phase0Harness.cs
// Phase 0 (リズム精度ゲート) 用の最小プレイアブル・ハーネス。
// タップ / スペースキーで Conductor.CheckMoveTiming() を判定し、HIT/MISS を Console にログ。
// UI パッケージ不要 (Debug.Log のみ) にして、新規プロジェクトで即実行できるようにしている。

using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    public sealed class Phase0Harness : MonoBehaviour
    {
        [Tooltip("拍計測を行う Conductor。未設定ならシーンから自動検索。")]
        public Conductor conductor;

        [Tooltip("Start 時に自動で BGM 再生を開始する。")]
        public bool autoStart = true;

        private int _hits;
        private int _misses;

        private void Start()
        {
            if (conductor == null)
            {
                conductor = FindObjectOfType<Conductor>();
            }

            if (conductor == null)
            {
                Debug.LogError("[Phase0] Conductor が見つかりません。シーンに Conductor を配置してください。");
                return;
            }

            conductor.OnBeat += beat => Debug.Log($"[Phase0] beat {beat}");

            if (autoStart)
            {
                conductor.StartSong();
                Debug.Log("[Phase0] 再生開始。拍に合わせて SPACE / 画面クリックで HIT を狙ってください。");
            }
        }

        private void Update()
        {
            if (conductor == null) return;

#if ENABLE_LEGACY_INPUT_MANAGER
            bool tapped = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#else
            // Active Input Handling が「Input System (New)」単独の場合はここに新Input対応を追加。
            // RUNBOOK §トラブルシュート参照 (推奨: Player Settings で "Both" に設定)。
            bool tapped = false;
#endif
            if (tapped)
            {
                bool ok = conductor.CheckMoveTiming();
                if (ok) _hits++; else _misses++;
                Debug.Log(
                    $"[Phase0] {(ok ? "HIT " : "MISS")}  beat={conductor.CurrentBeat} " +
                    $"phase={conductor.SongPositionBeats:F3}  (HIT {_hits} / MISS {_misses})"
                );
            }
        }
    }
}
