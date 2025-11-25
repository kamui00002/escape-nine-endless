//
//  AudioManager.swift
//  EscapeNine-endless-
//
//  BGMと効果音を統合管理するサービス
//

import AVFoundation
import Combine

class AudioManager: ObservableObject {
    static let shared = AudioManager()
    
    // MARK: - Published Properties
    @Published var isBGMEnabled: Bool = true {
        didSet {
            if isBGMEnabled {
                resumeBGM()
            } else {
                pauseBGM()
            }
        }
    }
    
    @Published var isSFXEnabled: Bool = true
    @Published var bgmVolume: Double = 0.7 {
        didSet {
            beatEngine.setVolume(bgmVolume)
        }
    }
    
    @Published var sfxVolume: Double = 0.8
    
    // MARK: - Private Properties
    private let beatEngine: BeatEngine
    private var soundEffects: [SoundEffect: AVAudioPlayer] = [:]
    private var cancellables = Set<AnyCancellable>()
    
    // MARK: - Sound Effect Types
    enum SoundEffect: String {
        case move = "move"
        case skill = "skill"
        case gameOver = "gameover"
        case floorClear = "floor_clear"
        case buttonTap = "button_tap"
        case warning = "warning"
    }
    
    // MARK: - Initialization
    private init() {
        self.beatEngine = BeatEngine()
        setupAudioSession()
        loadUserPreferences()
        preloadSoundEffects()
    }
    
    // MARK: - Audio Session Setup
    private func setupAudioSession() {
        do {
            let audioSession = AVAudioSession.sharedInstance()
            try audioSession.setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try audioSession.setActive(true)
        } catch {
            print("❌ Audio Session setup failed: \(error)")
        }
    }
    
    // MARK: - User Preferences
    private func loadUserPreferences() {
        isBGMEnabled = UserDefaults.standard.bool(forKey: "isBGMEnabled")
        isSFXEnabled = UserDefaults.standard.bool(forKey: "isSFXEnabled")
        bgmVolume = UserDefaults.standard.double(forKey: "bgmVolume")
        sfxVolume = UserDefaults.standard.double(forKey: "sfxVolume")
        
        // デフォルト値設定（初回起動時）
        if bgmVolume == 0 && sfxVolume == 0 {
            bgmVolume = 0.7
            sfxVolume = 0.8
            isBGMEnabled = true
            isSFXEnabled = true
        }
    }
    
    func saveUserPreferences() {
        UserDefaults.standard.set(isBGMEnabled, forKey: "isBGMEnabled")
        UserDefaults.standard.set(isSFXEnabled, forKey: "isSFXEnabled")
        UserDefaults.standard.set(bgmVolume, forKey: "bgmVolume")
        UserDefaults.standard.set(sfxVolume, forKey: "sfxVolume")
    }
    
    // MARK: - Preload Sound Effects
    private func preloadSoundEffects() {
        let effects: [SoundEffect] = [.move, .skill, .gameOver, .floorClear, .buttonTap, .warning]
        
        for effect in effects {
            // 効果音ファイルを探す（wav, mp3, m4a）
            if let url = findSoundFile(named: effect.rawValue) {
                do {
                    let player = try AVAudioPlayer(contentsOf: url)
                    player.prepareToPlay()
                    player.volume = Float(sfxVolume)
                    soundEffects[effect] = player
                } catch {
                    print("⚠️ Failed to load sound effect \(effect.rawValue): \(error)")
                }
            } else {
                print("ℹ️ Sound effect file not found: \(effect.rawValue)")
            }
        }
    }
    
    private func findSoundFile(named name: String) -> URL? {
        let extensions = ["wav", "mp3", "m4a"]
        for ext in extensions {
            if let url = Bundle.main.url(forResource: name, withExtension: ext) {
                return url
            }
        }
        return nil
    }
    
    // MARK: - BGM Control
    func startBGM(bpm: Double) {
        guard isBGMEnabled else { return }
        beatEngine.loadMusic(bpm: bpm, volume: bgmVolume)
        beatEngine.play()
    }
    
    func stopBGM() {
        beatEngine.stop()
    }
    
    func pauseBGM() {
        beatEngine.pause()
    }
    
    func resumeBGM() {
        guard isBGMEnabled else { return }
        beatEngine.resume()
    }
    
    func changeBPM(_ newBPM: Double) {
        guard isBGMEnabled else { return }
        beatEngine.changeBPM(newBPM, volume: bgmVolume)
    }
    
    func setBGMVolume(_ volume: Double) {
        bgmVolume = volume
        saveUserPreferences()
    }
    
    // MARK: - Sound Effects
    func playSoundEffect(_ effect: SoundEffect) {
        guard isSFXEnabled else { return }
        
        if let player = soundEffects[effect] {
            player.volume = Float(sfxVolume)
            player.currentTime = 0
            player.play()
        }
    }
    
    func setSFXVolume(_ volume: Double) {
        sfxVolume = volume
        
        // 既存の効果音プレイヤーの音量を更新
        for (_, player) in soundEffects {
            player.volume = Float(volume)
        }
        
        saveUserPreferences()
    }
    
    // MARK: - Beat Engine Access
    var currentBeat: Int {
        beatEngine.currentBeat
    }
    
    var isPlaying: Bool {
        beatEngine.isPlaying
    }
    
    func checkMoveTiming() -> Bool {
        beatEngine.checkMoveTiming()
    }
    
    // BeatEngineのPublisherをそのまま公開
    var beatPublisher: AnyPublisher<Int, Never> {
        beatEngine.$currentBeat.eraseToAnyPublisher()
    }
    
    var playingPublisher: AnyPublisher<Bool, Never> {
        beatEngine.$isPlaying.eraseToAnyPublisher()
    }
}
