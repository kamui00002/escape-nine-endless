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
            // ビートエンジン音量
            if isBGMEnabled {
                beatEngine.setVolume(bgmVolume)
            } else {
                beatEngine.setVolume(0.0)
            }
            // BGMプレイヤー音量
            bgmPlayer?.volume = isBGMEnabled ? Float(bgmVolume) : 0.0
        }
    }

    @Published var isSFXEnabled: Bool = true
    @Published var bgmVolume: Double = 0.7 {
        didSet {
            if isBGMEnabled {
                beatEngine.setVolume(bgmVolume)
                bgmPlayer?.volume = Float(bgmVolume)
            }
        }
    }

    @Published var sfxVolume: Double = 0.8

    // MARK: - BGM Types
    enum BGMType: String {
        case menu = "bgm_menu"
        case early = "bgm_early"    // Floor 1-30
        case mid = "bgm_mid"        // Floor 31-60
        case late = "bgm_late"      // Floor 61-100
        case clear = "bgm_clear"    // クリアリザルト
        case gameOver = "bgm_gameover" // ゲームオーバー

        var loops: Bool {
            switch self {
            case .menu, .early, .mid, .late: return true
            case .clear, .gameOver: return false
            }
        }

        /// 階層に応じたゲームBGMを返す
        static func forFloor(_ floor: Int) -> BGMType {
            switch floor {
            case 1...30: return .early
            case 31...60: return .mid
            default: return .late
            }
        }
    }

    // MARK: - Private Properties
    private let beatEngine: BeatEngine
    private var soundEffects: [SoundEffect: AVAudioPlayer] = [:]
    private var cancellables = Set<AnyCancellable>()
    private var bgmPlayer: AVAudioPlayer?
    private var fadeTimer: Timer?
    private(set) var currentBGMType: BGMType?
    
    // MARK: - Sound Effect Types
    enum SoundEffect: String {
        case move = "move"
        case skill = "skill"
        case gameOver = "gameover"
        case floorClear = "floor_clear"
        case buttonTap = "button_tap"
        case warning = "warning"
        case countdown = "countdown"
        case gameStart = "game_start"
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
        // 初回起動チェック
        let isFirstLaunch = !UserDefaults.standard.bool(forKey: "hasLaunchedBefore")
        
        if isFirstLaunch {
            // デフォルト値を設定（初回起動時）
            isBGMEnabled = true
            isSFXEnabled = true
            bgmVolume = 0.7
            sfxVolume = 0.8
            
            // 初回起動フラグを保存
            UserDefaults.standard.set(true, forKey: "hasLaunchedBefore")
            saveUserPreferences()
        } else {
            // 保存された設定を読み込み
            isBGMEnabled = UserDefaults.standard.bool(forKey: "isBGMEnabled")
            isSFXEnabled = UserDefaults.standard.bool(forKey: "isSFXEnabled")
            if UserDefaults.standard.object(forKey: "bgmVolume") != nil {
                bgmVolume = UserDefaults.standard.double(forKey: "bgmVolume")
            }
            if UserDefaults.standard.object(forKey: "sfxVolume") != nil {
                sfxVolume = UserDefaults.standard.double(forKey: "sfxVolume")
            }
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
        let effects: [SoundEffect] = [.move, .skill, .gameOver, .floorClear, .buttonTap, .warning, .countdown, .gameStart]
        
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
    
    // MARK: - BGM Music Player
    func playBGMMusic(_ type: BGMType, fadeDuration: TimeInterval = 0.5) {
        // 同じBGMが再生中なら何もしない
        if currentBGMType == type, bgmPlayer?.isPlaying == true { return }

        // 現在のBGMをフェードアウトして切り替え
        fadeOutBGMPlayer(duration: fadeDuration) { [weak self] in
            self?.startBGMPlayer(type)
        }
    }

    func stopBGMMusic(fadeDuration: TimeInterval = 0.3) {
        fadeOutBGMPlayer(duration: fadeDuration) { [weak self] in
            self?.currentBGMType = nil
        }
    }

    private func startBGMPlayer(_ type: BGMType) {
        guard let url = Bundle.main.url(forResource: type.rawValue, withExtension: "mp3") else {
            print("BGM file not found: \(type.rawValue).mp3")
            currentBGMType = nil
            return
        }
        do {
            bgmPlayer = try AVAudioPlayer(contentsOf: url)
            bgmPlayer?.numberOfLoops = type.loops ? -1 : 0
            bgmPlayer?.volume = isBGMEnabled ? Float(bgmVolume) : 0.0
            bgmPlayer?.prepareToPlay()
            bgmPlayer?.play()
            currentBGMType = type
        } catch {
            print("BGM playback failed: \(error)")
            currentBGMType = nil
        }
    }

    private func fadeOutBGMPlayer(duration: TimeInterval, completion: @escaping () -> Void) {
        fadeTimer?.invalidate()
        fadeTimer = nil

        guard let player = bgmPlayer, player.isPlaying else {
            completion()
            return
        }
        let steps = 10
        let interval = duration / Double(steps)
        let volumeStep = player.volume / Float(steps)
        var currentStep = 0

        fadeTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] timer in
            currentStep += 1
            player.volume -= volumeStep
            if currentStep >= steps {
                timer.invalidate()
                self?.fadeTimer = nil
                player.stop()
                player.volume = 0
                completion()
            }
        }
    }

    func pauseBGMMusic() {
        bgmPlayer?.pause()
    }

    func resumeBGMMusic() {
        bgmPlayer?.volume = isBGMEnabled ? Float(bgmVolume) : 0.0
        bgmPlayer?.play()
    }

    // MARK: - Beat Engine Control (メトロノーム + ターンカウントダウン)
    func startBGM(bpm: Double) {
        // BGMが無効でもビートエンジンは動かす（ゲームロジックのため）
        let volume = isBGMEnabled ? bgmVolume : 0.0
        beatEngine.loadMusic(bpm: bpm, volume: volume)
        beatEngine.play()
    }

    func stopBGM() {
        beatEngine.stop()
    }

    func pauseBGM() {
        beatEngine.pause()
    }

    func resumeBGM() {
        // BGMが無効でもビートエンジンは再開（ゲームロジックのため）
        beatEngine.resume()
        // 音量だけ調整
        if isBGMEnabled {
            beatEngine.setVolume(bgmVolume)
        } else {
            beatEngine.setVolume(0.0)
        }
    }

    func changeBPM(_ newBPM: Double) {
        // BGMが無効でもビートエンジンのBPMは変更（ゲームロジックのため）
        let volume = isBGMEnabled ? bgmVolume : 0.0
        beatEngine.changeBPM(newBPM, volume: volume)
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
    
    // MARK: - Turn Countdown
    var turnCountdown: Int {
        beatEngine.turnCountdown
    }

    var turnCountdownPublisher: AnyPublisher<Int, Never> {
        beatEngine.$turnCountdown.eraseToAnyPublisher()
    }

    func resetTurnCountdown() {
        beatEngine.resetTurnCountdown()
    }

    func setTurnCountdownBeats(_ count: Int) {
        beatEngine.setTurnCountdownBeats(count)
    }

    // MARK: - Turn Deadline Callback
    var onTurnDeadline: (() -> Void)? {
        get { beatEngine.onTurnDeadline }
        set { beatEngine.onTurnDeadline = newValue }
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
    
    /// 次のビートまでの残り時間（0.0〜1.0の割合）
    func timeUntilNextBeat() -> Double {
        beatEngine.timeUntilNextBeat()
    }
    
    // BeatEngineのPublisherをそのまま公開
    var beatPublisher: AnyPublisher<Int, Never> {
        beatEngine.$currentBeat.eraseToAnyPublisher()
    }
    
    var playingPublisher: AnyPublisher<Bool, Never> {
        beatEngine.$isPlaying.eraseToAnyPublisher()
    }
}
