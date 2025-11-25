//
//  PlayerViewModel.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import Combine

class PlayerViewModel: ObservableObject {
    // MARK: - Published Properties
    @Published var highestFloor: Int = 0
    @Published var unlockedCharacters: [CharacterType] = [.hero]
    @Published var selectedCharacter: CharacterType = .hero
    @Published var adRemoved: Bool = false
    @Published var bgmVolume: Double = Constants.defaultVolume {
        didSet {
            AudioManager.shared.setBGMVolume(bgmVolume)
        }
    }
    @Published var seVolume: Double = Constants.defaultVolume {
        didSet {
            AudioManager.shared.setSFXVolume(seVolume)
        }
    }
    
    // MARK: - Debug/Admin Properties (管理者用 - 後で削除可能)
    @Published var debugStartFloor: Int = 1 // デバッグ用開始階層
    @Published var debugAILevel: AILevel = .normal // デバッグ用AI難易度
    @Published var debugUnlockAllCharacters: Bool = false // 全キャラクターアンロック
    
    // MARK: - UserDefaults Keys
    private let highestFloorKey = "highestFloor"
    private let unlockedCharactersKey = "unlockedCharacters"
    private let selectedCharacterKey = "selectedCharacter"
    private let adRemovedKey = "adRemoved"
    private let bgmVolumeKey = "bgmVolume"
    private let seVolumeKey = "seVolume"
    private let debugStartFloorKey = "debugStartFloor"
    private let debugAILevelKey = "debugAILevel"
    private let debugUnlockAllCharactersKey = "debugUnlockAllCharacters"
    
    // MARK: - Initialization
    init() {
        loadData()
    }
    
    // MARK: - Data Persistence
    private func loadData() {
        highestFloor = UserDefaults.standard.integer(forKey: highestFloorKey)
        
        if let unlockedData = UserDefaults.standard.array(forKey: unlockedCharactersKey) as? [String] {
            unlockedCharacters = unlockedData.compactMap { CharacterType(rawValue: $0) }
        }
        
        if let selectedData = UserDefaults.standard.string(forKey: selectedCharacterKey),
           let character = CharacterType(rawValue: selectedData) {
            selectedCharacter = character
        }
        
        adRemoved = UserDefaults.standard.bool(forKey: adRemovedKey)
        bgmVolume = UserDefaults.standard.double(forKey: bgmVolumeKey)
        if bgmVolume == 0.0 {
            bgmVolume = Constants.defaultVolume // デフォルト値
        }
        seVolume = UserDefaults.standard.double(forKey: seVolumeKey)
        if seVolume == 0.0 {
            seVolume = Constants.defaultVolume // デフォルト値
        }
        
        // デバッグ設定の読み込み
        debugStartFloor = UserDefaults.standard.integer(forKey: debugStartFloorKey)
        if debugStartFloor == 0 {
            debugStartFloor = 1 // デフォルト値
        }
        
        if let aiLevelString = UserDefaults.standard.string(forKey: debugAILevelKey),
           let aiLevel = AILevel(rawValue: aiLevelString) {
            debugAILevel = aiLevel
        }
        
        debugUnlockAllCharacters = UserDefaults.standard.bool(forKey: debugUnlockAllCharactersKey)
        
        // 全キャラクターアンロックが有効な場合
        if debugUnlockAllCharacters {
            unlockedCharacters = CharacterType.allCases
        }
    }
    
    func saveData() {
        UserDefaults.standard.set(highestFloor, forKey: highestFloorKey)
        UserDefaults.standard.set(unlockedCharacters.map { $0.rawValue }, forKey: unlockedCharactersKey)
        UserDefaults.standard.set(selectedCharacter.rawValue, forKey: selectedCharacterKey)
        UserDefaults.standard.set(adRemoved, forKey: adRemovedKey)
        UserDefaults.standard.set(bgmVolume, forKey: bgmVolumeKey)
        UserDefaults.standard.set(seVolume, forKey: seVolumeKey)
        UserDefaults.standard.set(debugStartFloor, forKey: debugStartFloorKey)
        UserDefaults.standard.set(debugAILevel.rawValue, forKey: debugAILevelKey)
        UserDefaults.standard.set(debugUnlockAllCharacters, forKey: debugUnlockAllCharactersKey)
    }
    
    // MARK: - Debug/Admin Functions (管理者用 - 後で削除可能)
    func toggleUnlockAllCharacters() {
        debugUnlockAllCharacters.toggle()
        if debugUnlockAllCharacters {
            unlockedCharacters = CharacterType.allCases
        } else {
            // 通常のロック状態に戻す（ヒーローは常にアンロック）
            unlockedCharacters = [.hero]
            // 10階層クリアで盗賊がアンロックされている場合は追加
            if highestFloor >= Constants.thiefUnlockFloor {
                unlockedCharacters.append(.thief)
            }
        }
        saveData()
    }
    
    // MARK: - Character Management
    func unlockCharacter(_ character: CharacterType) {
        if !unlockedCharacters.contains(character) {
            unlockedCharacters.append(character)
            saveData()
        }
    }
    
    func selectCharacter(_ character: CharacterType) {
        // 管理者用設定が有効な場合、またはアンロック済みの場合は選択可能
        if debugUnlockAllCharacters || unlockedCharacters.contains(character) {
            selectedCharacter = character
            saveData()
        }
    }
    
    func updateHighestFloor(_ floor: Int) {
        if floor > highestFloor {
            highestFloor = floor
            saveData()

            // 盗賊解放
            if floor >= Constants.thiefUnlockFloor && !unlockedCharacters.contains(.thief) {
                unlockCharacter(.thief)
            }
            // 階層30で魔法使い解放（通常は有料）
            // 階層50でエルフ解放（通常は有料）
        }
    }
    
    func removeAds() {
        adRemoved = true
        saveData()
    }
}

