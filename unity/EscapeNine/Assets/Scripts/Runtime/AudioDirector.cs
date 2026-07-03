// AudioDirector.cs
// Swift 正本からの忠実移植: Services/AudioManager.swift。
// Swift の二重構成 (BeatEngine=メトロノーム / bgmPlayer=楽曲) を Unity では
//   - 拍タイミングの正 = Conductor (dspTime クロック、楽曲は鳴らさない)
//   - 音の再生 = 本クラス (BGM 用 AudioSource + SFX 用 AudioSource)
// に分離して再現する。メトロノームのチック音は Swift BeatEngine と同じ
// プログラム生成トーン (880Hz / アクセント 1320Hz、指数減衰) を使う。
//
// 音量・ON/OFF は PlayerState に永続化 (Swift: UserDefaults "bgmVolume"/"seVolume" 等)。

using System.Collections.Generic;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    public sealed class AudioDirector : MonoBehaviour
    {
        // MARK: - Resources パス
        private const string BgmPath = "Sounds/BGM/";
        private const string SfxPath = "Sounds/SFX/";

        // MARK: - 内部状態
        private AudioSource _bgmSource;       // 楽曲 (ループ/単発)
        private AudioSource _sfxSource;       // 効果音 + メトロノームチック (PlayOneShot)
        private AudioSource _heartbeatSource; // 心拍ループ (Swift: heartbeatPlayer と同じく独立経路)
        private PlayerState _player;
        private string _currentBgmName;       // Swift: currentBGMType (同一 BGM 再生中の no-op 判定用)

        private readonly Dictionary<string, AudioClip> _sfxCache = new Dictionary<string, AudioClip>();

        // メトロノームチック (Swift BeatEngine: tick=880Hz/0.05s/0.3, accent=1320Hz/0.06s/0.5)
        private AudioClip _tickClip;
        private AudioClip _accentClip;

        private void Awake()
        {
            EnsureSources();
        }

        /// <summary>
        /// AudioSource / 生成クリップの遅延初期化。
        /// 本コンポーネントがシーンに事前配置されている場合、同一 GameObject 上の
        /// App.Awake → Init() が本クラスの Awake より先に走る可能性がある
        /// (Unity はコンポーネント間の Awake 順序を保証しない) ため、
        /// Awake と Init の両方から呼べる冪等な初期化に分離している。
        /// </summary>
        private void EnsureSources()
        {
            if (_bgmSource != null) return; // 初期化済み

            // AudioSource は役割ごとに分離 (BGM の音量変更が SFX に影響しないように)
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = 1f; // 個別音量は PlayOneShot の volumeScale で渡す

            _heartbeatSource = gameObject.AddComponent<AudioSource>();
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.loop = true;

            _tickClip = GenerateTickClip("metronome_tick", 880.0, 0.05, 0.3f);
            _accentClip = GenerateTickClip("metronome_accent", 1320.0, 0.06, 0.5f);
        }

        /// <summary>App が PlayerState を生成した後に呼ぶ初期化。音量設定を反映する。</summary>
        public void Init(PlayerState player)
        {
            EnsureSources(); // Awake より先に呼ばれても安全にする
            _player = player;
            ApplyBgmSourceVolume();
        }

        // MARK: - Volume (固定契約: get/set → PlayerState 永続化)

        public float BgmVolume
        {
            get => _player != null ? _player.BgmVolume : (float)GameConfig.DefaultVolume;
            set
            {
                if (_player == null) return;
                _player.BgmVolume = Mathf.Clamp01(value);
                _player.Save(); // Swift: setBGMVolume → saveUserPreferences
                ApplyBgmSourceVolume();
            }
        }

        public float SfxVolume
        {
            get => _player != null ? _player.SfxVolume : (float)GameConfig.DefaultVolume;
            set
            {
                if (_player == null) return;
                _player.SfxVolume = Mathf.Clamp01(value);
                _player.Save(); // Swift: setSFXVolume → saveUserPreferences
                _heartbeatSource.volume = _player.SfxVolume;
            }
        }

        /// <summary>BGM 有効フラグ。無効でも Conductor (拍) は止めない (Swift と同じ設計)。</summary>
        public bool IsBgmEnabled
        {
            get => _player == null || _player.IsBgmEnabled;
            set
            {
                if (_player == null) return;
                _player.IsBgmEnabled = value;
                _player.Save();
                ApplyBgmSourceVolume();
            }
        }

        public bool IsSfxEnabled
        {
            get => _player == null || _player.IsSfxEnabled;
            set
            {
                if (_player == null) return;
                _player.IsSfxEnabled = value;
                _player.Save();
            }
        }

        private void ApplyBgmSourceVolume()
        {
            // Swift: bgmPlayer.volume = isBGMEnabled ? bgmVolume : 0.0
            _bgmSource.volume = IsBgmEnabled ? BgmVolume : 0f;
        }

        // MARK: - SFX (固定契約)

        /// <summary>
        /// 効果音を再生する。name は拡張子なしのファイル名
        /// (button_tap / countdown / move / skill / game_start / warning / floor_clear / gameover / heartbeat)。
        /// Swift: playSoundEffect(_:)
        /// </summary>
        public void PlaySfx(string name)
        {
            if (!IsSfxEnabled) return;

            var clip = LoadSfxClip(name);
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, SfxVolume);
        }

        private AudioClip LoadSfxClip(string name)
        {
            if (_sfxCache.TryGetValue(name, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>(SfxPath + name);
            if (clip == null)
            {
                // Swift 同様、ファイル欠損は警告のみで落とさない (スケルトン段階でも動くように)
                Debug.LogWarning($"[AudioDirector] 効果音が見つかりません: {SfxPath}{name}");
            }
            _sfxCache[name] = clip; // null もキャッシュして毎フレーム Load を防ぐ
            return clip;
        }

        // MARK: - Metronome Tick (Swift: BeatEngine.playTick(accent:))

        /// <summary>
        /// ターンカウントダウンのチック音。accent=true は締切拍 (turnCountdown==0)。
        /// Swift 同様、メトロノームは「BGM 経路」の音量に従う (BGM 無効時は無音、拍自体は止めない)。
        /// </summary>
        public void PlayCountdownTick(bool accent)
        {
            float volume = IsBgmEnabled ? BgmVolume : 0f;
            if (volume <= 0f) return;

            var clip = accent ? _accentClip : _tickClip;
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>正弦波 + 指数減衰エンベロープのチック音を生成。Swift: generateTickBuffer と同一式。</summary>
        private static AudioClip GenerateTickClip(string name, double frequency, double duration, float amplitude)
        {
            const int sampleRate = 44100;
            int frameCount = (int)(sampleRate * duration);
            var samples = new float[frameCount];

            for (int frame = 0; frame < frameCount; frame++)
            {
                double time = (double)frame / sampleRate;
                float envelope = amplitude * Mathf.Exp((float)(-time * 60.0));
                samples[frame] = envelope * Mathf.Sin((float)(2.0 * Mathf.PI * frequency * time));
            }

            var clip = AudioClip.Create(name, frameCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // MARK: - BGM (固定契約 + 補助)

        /// <summary>
        /// 階層に応じたゲーム BGM。Swift: AudioManager.BGMType.forFloor(_:)
        /// Floor 1-30: early / 31-60: mid / それ以外: late (Swift の default 分岐を踏襲)。
        /// </summary>
        public void PlayBgmForFloor(int floor)
        {
            PlayBgm(BgmNameForFloor(floor), loop: true);
        }

        public void PlayMenuBgm()
        {
            PlayBgm("bgm_menu", loop: true);
        }

        /// <summary>リザルト BGM (クリア/ゲームオーバー、非ループ)。Swift: playBGMMusic(.clear / .gameOver)</summary>
        public void PlayResultBgm(bool won)
        {
            PlayBgm(won ? "bgm_clear" : "bgm_gameover", loop: false);
        }

        public void StopBgm()
        {
            // Swift はフェードアウト付き。フェードは Phase 4 (juice) に送り、ここでは即停止
            _bgmSource.Stop();
            _currentBgmName = null;
        }

        public void PauseBgm() => _bgmSource.Pause();

        public void ResumeBgm()
        {
            ApplyBgmSourceVolume();
            _bgmSource.UnPause();
        }

        private void PlayBgm(string name, bool loop)
        {
            // 同じ BGM が再生中なら何もしない (Swift: playBGMMusic の先頭ガード)
            if (_currentBgmName == name && _bgmSource.isPlaying) return;

            var clip = Resources.Load<AudioClip>(BgmPath + name);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioDirector] BGM が見つかりません: {BgmPath}{name}");
                _currentBgmName = null;
                return;
            }

            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            ApplyBgmSourceVolume();
            _bgmSource.Play();
            _currentBgmName = name;
        }

        /// <summary>階層 → BGM ファイル名。切替判定にも使えるよう公開。</summary>
        public static string BgmNameForFloor(int floor)
        {
            if (floor >= 1 && floor <= 30) return "bgm_early";
            if (floor >= 31 && floor <= 60) return "bgm_mid";
            return "bgm_late";
        }

        // MARK: - Heartbeat Loop (Swift: startHeartbeatLoop / stopHeartbeatLoop、オンボーディング Step 3 用)

        public void StartHeartbeatLoop()
        {
            if (!IsSfxEnabled) return;
            if (_heartbeatSource.isPlaying) return; // 連打安全 (Swift と同じ)

            var clip = LoadSfxClip("heartbeat");
            if (clip == null) return;

            _heartbeatSource.clip = clip;
            _heartbeatSource.volume = SfxVolume;
            _heartbeatSource.Play();
        }

        public void StopHeartbeatLoop()
        {
            _heartbeatSource.Stop();
            _heartbeatSource.clip = null;
        }
    }
}
