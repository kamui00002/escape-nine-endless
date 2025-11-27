//
//  BeatEngine.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import AVFoundation
import Combine
import UIKit

class BeatEngine: ObservableObject {
    // MARK: - Published Properties
    @Published var currentBeat: Int = 0
    @Published var isPlaying: Bool = false
    
    // MARK: - Private Properties
    private var audioPlayer: AVAudioPlayer?
    private var bpm: Double = 60
    private var beatInterval: TimeInterval = 1.0
    private var timer: Timer?
    private var lastBeatTime: Date = Date()
    private var isFirstBeat: Bool = true  // 初回ビート判定用
    
    // MARK: - Constants
    private let timingTolerance: Double = Constants.timingTolerance
    private let beatCheckInterval: TimeInterval = Constants.beatCheckInterval
    
    // MARK: - Initialization
    init() {
        setupAudioSession()
    }
    
    // MARK: - Audio Session Setup
    private func setupAudioSession() {
        do {
            let audioSession = AVAudioSession.sharedInstance()
            try audioSession.setCategory(.playback, mode: .default)
            try audioSession.setActive(true)
        } catch {
            print("Audio Session setup failed: \(error)")
        }
    }
    
    // MARK: - Load Music
    func loadMusic(bpm: Double, volume: Double = 0.7) {
        self.bpm = bpm
        self.beatInterval = 60.0 / bpm
        
        // BGMファイル読み込み
        // TODO: 実際のBGMファイルを追加する必要があります
        guard let url = Bundle.main.url(
            forResource: "bgm_\(Int(bpm))",
            withExtension: "mp3"
        ) else {
            print("BGM file not found for BPM \(bpm), continuing without sound")
            // BGMファイルがない場合でもビート検出は動作する
            return
        }
        
        do {
            audioPlayer = try AVAudioPlayer(contentsOf: url)
            audioPlayer?.volume = Float(volume)
            audioPlayer?.prepareToPlay()
            audioPlayer?.numberOfLoops = -1 // 無限ループ
        } catch {
            print("Failed to load BGM: \(error)")
        }
    }
    
    func setVolume(_ volume: Double) {
        audioPlayer?.volume = Float(volume)
    }
    
    // MARK: - Playback Control
    func play() {
        audioPlayer?.play()
        isPlaying = true
        startBeatDetection()
    }
    
    func stop() {
        audioPlayer?.stop()
        isPlaying = false
        timer?.invalidate()
        timer = nil
        currentBeat = 0  // ビートカウンターをリセット
        isFirstBeat = true  // 初回ビートフラグをリセット
    }

    func pause() {
        audioPlayer?.pause()
        isPlaying = false
        timer?.invalidate()
        timer = nil  // メモリリーク防止
    }
    
    func resume() {
        audioPlayer?.play()
        isPlaying = true
        startBeatDetection()
    }
    
    // MARK: - Beat Detection
    private func startBeatDetection() {
        lastBeatTime = Date()
        isFirstBeat = true  // 初回ビートフラグをリセット
        
        // 高精度タイマー(10msごとにチェック)
        timer = Timer.scheduledTimer(
            withTimeInterval: beatCheckInterval,
            repeats: true
        ) { [weak self] _ in
            self?.checkBeat()
        }
    }
    
    private func checkBeat() {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastBeatTime)
        
        // 初回ビートは2倍の時間待つ（プレイヤーに猶予時間を与える）
        let requiredInterval = isFirstBeat ? (beatInterval * 2.0) : beatInterval
        
        if elapsed >= requiredInterval {
            // ビート発火前にフラグを更新（次回から通常インターバル）
            if isFirstBeat {
                print("🎵 初回ビート発火（猶予時間: \(elapsed)秒）")
                isFirstBeat = false
            }
            
            lastBeatTime = now  // ビート時刻を更新
            onBeat()
            
            print("🎵 ビート #\(currentBeat) 発火（経過時間: \(elapsed)秒、間隔: \(requiredInterval)秒）")
        }
    }
    
    private func onBeat() {
        DispatchQueue.main.async {
            self.currentBeat += 1
        }
        
        // 触覚フィードバック
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
    }
    
    // MARK: - Timing Check
    func checkMoveTiming() -> Bool {
        let now = Date()
        let timeDiff = abs(now.timeIntervalSince(lastBeatTime))
        let tolerance = beatInterval * timingTolerance
        
        return timeDiff <= tolerance
    }
    
    /// 次のビートまでの残り時間（0.0〜1.0の割合）
    func timeUntilNextBeat() -> Double {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastBeatTime)
        let requiredInterval = isFirstBeat ? (beatInterval * 2.0) : beatInterval
        let remaining = max(0, requiredInterval - elapsed)
        return remaining / requiredInterval
    }
    
    // MARK: - BPM Change
    func changeBPM(_ newBPM: Double, volume: Double = 0.7) {
        stop()  // stop()内でcurrentBeat = 0にリセット済み
        loadMusic(bpm: newBPM, volume: volume)
        play()
    }
    
    // MARK: - Cleanup
    deinit {
        stop()
    }
}

