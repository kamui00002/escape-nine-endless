#!/usr/bin/env python3
"""
Escape Nine: Endless - 効果音生成スクリプト

このスクリプトは、ゲームに必要な8つの効果音を生成します。
生成される音声ファイル: move.wav, skill.wav, gameover.wav, floor_clear.wav, button_tap.wav, warning.wav, countdown.wav, game_start.wav
"""

import numpy as np
import scipy.io.wavfile as wav
import os

# 音声パラメータ
SAMPLE_RATE = 44100  # 44.1kHz
BIT_DEPTH = np.int16  # 16bit

def normalize_audio(audio, target_amplitude=0.8):
    """音声を正規化して音量を統一"""
    max_val = np.max(np.abs(audio))
    if max_val > 0:
        audio = audio * (target_amplitude / max_val)
    return (audio * 32767).astype(BIT_DEPTH)

def generate_sine_wave(frequency, duration, sample_rate=SAMPLE_RATE):
    """サイン波を生成"""
    t = np.linspace(0, duration, int(sample_rate * duration))
    return np.sin(2 * np.pi * frequency * t)

def apply_envelope(audio, attack=0.01, decay=0.05, sustain_level=0.7, release=0.1):
    """ADSRエンベロープを適用"""
    length = len(audio)
    envelope = np.ones(length)

    # Attack
    attack_samples = int(SAMPLE_RATE * attack)
    if attack_samples > 0:
        envelope[:attack_samples] = np.linspace(0, 1, attack_samples)

    # Decay
    decay_samples = int(SAMPLE_RATE * decay)
    if decay_samples > 0:
        envelope[attack_samples:attack_samples+decay_samples] = np.linspace(1, sustain_level, decay_samples)

    # Sustain (既に設定済み)
    sustain_start = attack_samples + decay_samples
    sustain_end = length - int(SAMPLE_RATE * release)
    if sustain_end > sustain_start:
        envelope[sustain_start:sustain_end] = sustain_level

    # Release
    release_samples = int(SAMPLE_RATE * release)
    if release_samples > 0:
        envelope[-release_samples:] = np.linspace(sustain_level, 0, release_samples)

    return audio * envelope

def generate_move_sound():
    """移動音: 短いポップ音"""
    duration = 0.1
    frequency = 800

    audio = generate_sine_wave(frequency, duration)
    audio = apply_envelope(audio, attack=0.005, decay=0.02, sustain_level=0.5, release=0.073)

    return normalize_audio(audio, 0.6)

def generate_skill_sound():
    """スキル使用音: キラキラした上昇音"""
    duration = 0.8

    # 3つの周波数を組み合わせて魔法的な音を作る
    freq1 = generate_sine_wave(523, duration)  # C5
    freq2 = generate_sine_wave(659, duration)  # E5
    freq3 = generate_sine_wave(784, duration)  # G5

    # 時間経過で周波数が上がるエフェクト
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration))
    pitch_bend = np.sin(2 * np.pi * 2093 * t * (1 + 0.3 * t))  # C7から上昇

    audio = (freq1 + freq2 + freq3 + pitch_bend) / 4
    audio = apply_envelope(audio, attack=0.01, decay=0.1, sustain_level=0.6, release=0.3)

    return normalize_audio(audio, 0.7)

def generate_gameover_sound():
    """ゲームオーバー音: 下降する悲しい音"""
    duration = 1.2

    # 周波数が下がる
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration))
    freq_start = 440  # A4
    freq_end = 220    # A3
    frequency = freq_start + (freq_end - freq_start) * t / duration

    audio = np.sin(2 * np.pi * frequency * t)

    # 低音を追加して重厚感を出す
    bass = generate_sine_wave(110, duration)  # A2
    audio = (audio * 0.7 + bass * 0.3)

    audio = apply_envelope(audio, attack=0.05, decay=0.2, sustain_level=0.6, release=0.5)

    return normalize_audio(audio, 0.8)

def generate_floor_clear_sound():
    """フロアクリア音: 明るい上昇音"""
    duration = 1.0

    # メジャーコードのアルペジオ
    notes = [
        (523, 0.0, 0.2),   # C5
        (659, 0.15, 0.35),  # E5
        (784, 0.3, 0.5),    # G5
        (1047, 0.45, 0.8)   # C6
    ]

    audio = np.zeros(int(SAMPLE_RATE * duration))

    for freq, start_time, note_duration in notes:
        start_sample = int(start_time * SAMPLE_RATE)
        note = generate_sine_wave(freq, note_duration)
        note = apply_envelope(note, attack=0.01, decay=0.05, sustain_level=0.7, release=0.1)

        end_sample = start_sample + len(note)
        if end_sample <= len(audio):
            audio[start_sample:end_sample] += note
        else:
            audio[start_sample:] += note[:len(audio)-start_sample]

    return normalize_audio(audio, 0.75)

def generate_button_tap_sound():
    """ボタンタップ音: シンプルなクリック音"""
    duration = 0.08
    frequency = 1000

    audio = generate_sine_wave(frequency, duration)

    # 非常に短いエンベロープ
    audio = apply_envelope(audio, attack=0.002, decay=0.01, sustain_level=0.3, release=0.068)

    return normalize_audio(audio, 0.5)

def generate_warning_sound():
    """警告音: 緊急感のあるビープ音"""
    duration = 0.6
    beep_freq = 880  # A5

    # 2回ビープ
    beep_duration = 0.15
    pause_duration = 0.1

    beep = generate_sine_wave(beep_freq, beep_duration)
    beep = apply_envelope(beep, attack=0.005, decay=0.02, sustain_level=0.8, release=0.05)

    pause = np.zeros(int(SAMPLE_RATE * pause_duration))

    # 2つのビープを結合
    audio = np.concatenate([beep, pause, beep])

    # 足りない長さを無音で埋める
    if len(audio) < int(SAMPLE_RATE * duration):
        padding = np.zeros(int(SAMPLE_RATE * duration) - len(audio))
        audio = np.concatenate([audio, padding])

    return normalize_audio(audio, 0.7)

def generate_countdown_sound():
    """カウントダウン音: シンプルで聞き取りやすいビープ音"""
    duration = 0.3
    frequency = 880  # A5

    audio = generate_sine_wave(frequency, duration)
    audio = apply_envelope(audio, attack=0.01, decay=0.05, sustain_level=0.7, release=0.1)

    return normalize_audio(audio, 0.75)

def generate_game_start_sound():
    """ゲームスタート音: 力強い上昇音"""
    duration = 1.2

    # 3つの音を重ねて力強さを出す
    t = np.linspace(0, duration, int(SAMPLE_RATE * duration))

    # メインメロディー（上昇）
    freq_start = 392  # G4
    freq_mid = 523    # C5
    freq_end = 784    # G5

    # 前半（G4 -> C5）
    half_duration = duration / 2
    half_samples = int(SAMPLE_RATE * half_duration)

    t1 = np.linspace(0, half_duration, half_samples)
    frequency1 = freq_start + (freq_mid - freq_start) * t1 / half_duration
    audio1 = np.sin(2 * np.pi * frequency1 * t1)

    # 後半（C5 -> G5）
    t2 = np.linspace(0, half_duration, half_samples)
    frequency2 = freq_mid + (freq_end - freq_mid) * t2 / half_duration
    audio2 = np.sin(2 * np.pi * frequency2 * t2)

    # 結合
    melody = np.concatenate([audio1, audio2])

    # ベース音を追加
    bass = generate_sine_wave(196, duration)  # G3

    # コード感を出すための第5音
    fifth = generate_sine_wave(587, duration)  # D5

    # ミックス
    audio = (melody * 0.5 + bass * 0.3 + fifth * 0.2)

    audio = apply_envelope(audio, attack=0.02, decay=0.1, sustain_level=0.8, release=0.3)

    return normalize_audio(audio, 0.85)

def main():
    """すべての効果音を生成してSounds/SFXフォルダに保存"""

    # 出力ディレクトリ
    output_dir = "EscapeNine-endless-/EscapeNine-endless-/Sounds/SFX"
    os.makedirs(output_dir, exist_ok=True)

    print("🎵 効果音生成を開始します...")
    print(f"📁 出力先: {output_dir}")
    print()

    # 効果音を生成
    sounds = {
        "move.wav": generate_move_sound(),
        "skill.wav": generate_skill_sound(),
        "gameover.wav": generate_gameover_sound(),
        "floor_clear.wav": generate_floor_clear_sound(),
        "button_tap.wav": generate_button_tap_sound(),
        "warning.wav": generate_warning_sound(),
        "countdown.wav": generate_countdown_sound(),
        "game_start.wav": generate_game_start_sound()
    }

    # ファイルに保存
    for filename, audio in sounds.items():
        filepath = os.path.join(output_dir, filename)
        wav.write(filepath, SAMPLE_RATE, audio)

        duration = len(audio) / SAMPLE_RATE
        print(f"✅ {filename} を生成しました（{duration:.2f}秒）")

    print()
    print("🎉 すべての効果音を生成しました！")
    print()
    print("次のステップ:")
    print("1. 各効果音を再生して音質を確認してください")
    print("2. Xcodeでプロジェクトにファイルを追加してください")
    print("   (Add Files to 'EscapeNine-endless-'... > Sounds フォルダを選択)")
    print("3. ゲームを実行して動作確認してください")

if __name__ == "__main__":
    main()
