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
    func loadMusic(bpm: Double) {
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
            audioPlayer?.prepareToPlay()
            audioPlayer?.numberOfLoops = -1 // 無限ループ
        } catch {
            print("Failed to load BGM: \(error)")
        }
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
    }
    
    func pause() {
        audioPlayer?.pause()
        isPlaying = false
        timer?.invalidate()
    }
    
    func resume() {
        audioPlayer?.play()
        isPlaying = true
        startBeatDetection()
    }
    
    // MARK: - Beat Detection
    private func startBeatDetection() {
        lastBeatTime = Date()
        
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
        
        if elapsed >= beatInterval {
            onBeat()
            lastBeatTime = now
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
    
    // MARK: - BPM Change
    func changeBPM(_ newBPM: Double) {
        stop()
        loadMusic(bpm: newBPM)
        play()
    }
    
    // MARK: - Cleanup
    deinit {
        stop()
    }
}

