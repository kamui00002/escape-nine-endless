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
    private var bpm: Double = 60
    private var beatInterval: TimeInterval = 1.0
    private var timer: Timer?
    private var lastBeatTime: Date = Date()
    private var isFirstBeat: Bool = true
    private var volume: Float = 0.7

    // MARK: - Audio Engine (Metronome)
    private var audioEngine: AVAudioEngine?
    private var playerNode: AVAudioPlayerNode?
    private var tickBuffer: AVAudioPCMBuffer?
    private var accentBuffer: AVAudioPCMBuffer?

    // MARK: - Constants
    private let timingTolerance: Double = Constants.timingTolerance
    private let beatCheckInterval: TimeInterval = Constants.beatCheckInterval

    // MARK: - Initialization
    init() {
        setupAudioSession()
        setupAudioEngine()
    }

    // MARK: - Audio Session Setup
    private func setupAudioSession() {
        do {
            let audioSession = AVAudioSession.sharedInstance()
            try audioSession.setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try audioSession.setActive(true)
        } catch {
            print("Audio Session setup failed: \(error)")
        }
    }

    // MARK: - Audio Engine Setup (Programmatic Metronome)
    private func setupAudioEngine() {
        audioEngine = AVAudioEngine()
        playerNode = AVAudioPlayerNode()

        guard let engine = audioEngine, let player = playerNode else { return }

        engine.attach(player)

        let format = AVAudioFormat(standardFormatWithSampleRate: 44100, channels: 1)!
        engine.connect(player, to: engine.mainMixerNode, format: format)

        // Generate tick and accent buffers
        tickBuffer = generateTickBuffer(format: format, frequency: 880, duration: 0.05, amplitude: 0.3)
        accentBuffer = generateTickBuffer(format: format, frequency: 1320, duration: 0.06, amplitude: 0.5)

        do {
            try engine.start()
        } catch {
            print("Audio Engine start failed: \(error)")
        }
    }

    /// Generate a short click/tick sound buffer programmatically
    private func generateTickBuffer(format: AVAudioFormat, frequency: Double, duration: Double, amplitude: Float) -> AVAudioPCMBuffer? {
        let sampleRate = format.sampleRate
        let frameCount = AVAudioFrameCount(sampleRate * duration)

        guard let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: frameCount) else {
            return nil
        }
        buffer.frameLength = frameCount

        guard let channelData = buffer.floatChannelData?[0] else { return nil }

        for frame in 0..<Int(frameCount) {
            let time = Double(frame) / sampleRate
            // Sine wave with exponential decay envelope
            let envelope = amplitude * Float(exp(-time * 60))
            let sample = envelope * sin(Float(2.0 * Double.pi * frequency * time))
            channelData[frame] = sample
        }

        return buffer
    }

    /// Play a metronome tick sound
    private func playTick(accent: Bool = false) {
        guard let player = playerNode,
              let buffer = accent ? accentBuffer : tickBuffer else { return }

        // Apply volume
        player.volume = volume

        // Schedule and play the tick
        player.scheduleBuffer(buffer, at: nil, options: [], completionHandler: nil)
        if !player.isPlaying {
            player.play()
        }
    }

    // MARK: - Load Music
    func loadMusic(bpm: Double, volume: Double = 0.7) {
        self.bpm = bpm
        self.beatInterval = 60.0 / bpm
        self.volume = Float(volume)
    }

    func setVolume(_ volume: Double) {
        self.volume = Float(volume)
        playerNode?.volume = Float(volume)
    }

    // MARK: - Playback Control
    func play() {
        isPlaying = true
        isFirstBeat = true
        startBeatDetection()
    }

    func stop() {
        isPlaying = false
        timer?.invalidate()
        timer = nil
        currentBeat = 0
        isFirstBeat = true
    }

    func pause() {
        isPlaying = false
        timer?.invalidate()
        timer = nil
    }

    func resume() {
        isPlaying = true
        isFirstBeat = false
        startBeatDetection()
    }

    // MARK: - Beat Detection
    private func startBeatDetection() {
        lastBeatTime = Date()

        // Invalidate existing timer
        timer?.invalidate()
        timer = nil

        let newTimer = Timer(
            timeInterval: beatCheckInterval,
            repeats: true
        ) { [weak self] _ in
            self?.checkBeat()
        }
        RunLoop.main.add(newTimer, forMode: .common)
        timer = newTimer
    }

    private func checkBeat() {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastBeatTime)

        let requiredInterval = isFirstBeat ? (beatInterval * 2.0) : beatInterval

        if elapsed >= requiredInterval {
            if isFirstBeat {
                isFirstBeat = false
            }

            lastBeatTime = now
            onBeat()
        }
    }

    private func onBeat() {
        DispatchQueue.main.async {
            self.currentBeat += 1
        }

        // Play metronome tick (accent on beat 1 of every 4)
        let isAccent = (currentBeat % 4 == 0)
        playTick(accent: isAccent)

        // Haptic feedback
        let generator = UIImpactFeedbackGenerator(style: isAccent ? .medium : .light)
        generator.impactOccurred()
    }

    // MARK: - Timing Check
    func checkMoveTiming() -> Bool {
        let now = Date()
        let timeDiff = abs(now.timeIntervalSince(lastBeatTime))
        let tolerance = beatInterval * timingTolerance

        return timeDiff <= tolerance
    }

    /// Progress until next beat (0.0 = just beat, 1.0 = about to beat)
    func timeUntilNextBeat() -> Double {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastBeatTime)
        let requiredInterval = isFirstBeat ? (beatInterval * 2.0) : beatInterval
        let remaining = max(0, requiredInterval - elapsed)
        return remaining / requiredInterval
    }

    // MARK: - BPM Change
    func changeBPM(_ newBPM: Double, volume: Double = 0.7) {
        stop()
        loadMusic(bpm: newBPM, volume: volume)
        play()
    }

    // MARK: - Cleanup
    deinit {
        stop()
        audioEngine?.stop()
        if let player = playerNode {
            audioEngine?.detach(player)
        }
    }
}
