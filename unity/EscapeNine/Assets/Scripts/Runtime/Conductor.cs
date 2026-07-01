// Conductor.cs
// Phase 0 の核: リズム同期エンジン。Swift の BeatEngine (AVAudioEngine + Timer) を
// Unity の推奨手法 = AudioSettings.dspTime ベースに置き換える。
//
// ★重要: リズムゲームでは Time.time / Time.deltaTime を拍計算に使ってはならない。
//   フレーム揺れ・音声レイテンシで拍がずれる。オーディオ DSP クロック (dspTime) を
//   唯一の真実の時計として使い、拍位置を「BGM再生開始からの経過DSP秒 ÷ 拍間隔」で逆算する。
//
// Phase 0 ゲート: 本 Conductor を最小プレイアブルに組み込み、iOS 実機で
//   Swift 版と同等以上のタイミング体感が出るかを検証する (docs/unity-migration-plan.md §3)。

using System;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class Conductor : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("ループ再生する BGM。Swift 版の bgm_*.mp3 を流用可。")]
        public AudioClip song;

        [Header("Beat")]
        [Tooltip("現在のBPM。Floor.CalculateBPM(floor) から設定する。")]
        public double bpm = GameConfig.BpmCurveStart;

        [Tooltip("曲の頭〜最初の拍までのオフセット秒 (音源に無音導入がある場合に調整)。")]
        public double firstBeatOffsetSeconds = 0.0;

        [Tooltip("PlayScheduled のスケジュール先行秒 (安定再生のため少し先に予約)。")]
        public double scheduleAheadSeconds = 0.1;

        /// <summary>直近に発火した拍番号 (0-indexed)。未開始は -1。</summary>
        public int CurrentBeat { get; private set; } = -1;

        /// <summary>拍が進むたびに発火 (引数 = 拍番号)。Swift: BeatEngine.$currentBeat 相当。</summary>
        public event Action<int> OnBeat;

        private AudioSource _audioSource;
        private double _dspSongStart = -1.0; // 曲頭の DSP 時刻。未開始は負値。

        private double SecondsPerBeat => 60.0 / bpm;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            if (song != null) _audioSource.clip = song;
        }

        /// <summary>BGM をスケジュール再生し、拍カウントを開始する。Swift: BeatEngine.play()</summary>
        public void StartSong()
        {
            if (_audioSource.clip == null && song != null) _audioSource.clip = song;

            _dspSongStart = AudioSettings.dspTime + scheduleAheadSeconds;
            CurrentBeat = -1;
            _audioSource.PlayScheduled(_dspSongStart);
        }

        /// <summary>停止。Swift: BeatEngine.stop()</summary>
        public void StopSong()
        {
            _audioSource.Stop();
            _dspSongStart = -1.0;
            CurrentBeat = -1;
        }

        /// <summary>BPM を変更して再スタート。Swift: BeatEngine.changeBPM(_:)</summary>
        public void ChangeBPM(double newBpm)
        {
            bpm = newBpm;
            StartSong();
        }

        /// <summary>曲頭からの経過秒 (DSP クロック基準、オフセット補正込み)。</summary>
        public double SongPositionSeconds =>
            _dspSongStart < 0 ? 0.0 : AudioSettings.dspTime - _dspSongStart - firstBeatOffsetSeconds;

        /// <summary>曲頭からの経過拍数 (小数)。整数部が拍番号、小数部が拍内位相。</summary>
        public double SongPositionBeats => SongPositionSeconds / SecondsPerBeat;

        private void Update()
        {
            if (_dspSongStart < 0) return;

            int beat = (int)Math.Floor(SongPositionBeats);
            if (beat > CurrentBeat && beat >= 0)
            {
                CurrentBeat = beat;
                OnBeat?.Invoke(beat);
            }
        }

        /// <summary>
        /// 現在のタイミングが「拍に合っているか」を判定。Swift: BeatEngine.checkMoveTiming()
        /// 最寄りの拍までの位相距離 (拍単位) が許容比率以内なら true。
        /// 許容比率は BPM 連動 (GameConfig.TimingTolerance)。
        /// </summary>
        public bool CheckMoveTiming()
        {
            if (_dspSongStart < 0) return false;

            double beatPos = SongPositionBeats;
            double frac = beatPos - Math.Floor(beatPos);          // 拍内位相 [0,1)
            double distanceToNearestBeat = Math.Min(frac, 1.0 - frac); // 最寄り拍までの位相距離
            double tolerance = GameConfig.TimingTolerance(bpm);   // 拍間隔に対する比率
            return distanceToNearestBeat <= tolerance;
        }
    }
}
