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
    @Published var bgmVolume: Double = 0.7 // BGM音量（0.0-1.0）
    @Published var seVolume: Double = 0.7 // 効果音量（0.0-1.0）
    
    // MARK: - UserDefaults Keys
    private let highestFloorKey = "highestFloor"
    private let unlockedCharactersKey = "unlockedCharacters"
    private let selectedCharacterKey = "selectedCharacter"
    private let adRemovedKey = "adRemoved"
    private let bgmVolumeKey = "bgmVolume"
    private let seVolumeKey = "seVolume"
    
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
            bgmVolume = 0.7 // デフォルト値
        }
        seVolume = UserDefaults.standard.double(forKey: seVolumeKey)
        if seVolume == 0.0 {
            seVolume = 0.7 // デフォルト値
        }
    }
    
    func saveData() {
        UserDefaults.standard.set(highestFloor, forKey: highestFloorKey)
        UserDefaults.standard.set(unlockedCharacters.map { $0.rawValue }, forKey: unlockedCharactersKey)
        UserDefaults.standard.set(selectedCharacter.rawValue, forKey: selectedCharacterKey)
        UserDefaults.standard.set(adRemoved, forKey: adRemovedKey)
        UserDefaults.standard.set(bgmVolume, forKey: bgmVolumeKey)
        UserDefaults.standard.set(seVolume, forKey: seVolumeKey)
    }
    
    // MARK: - Character Management
    func unlockCharacter(_ character: CharacterType) {
        if !unlockedCharacters.contains(character) {
            unlockedCharacters.append(character)
            saveData()
        }
    }
    
    func selectCharacter(_ character: CharacterType) {
        if unlockedCharacters.contains(character) {
            selectedCharacter = character
            saveData()
        }
    }
    
    func updateHighestFloor(_ floor: Int) {
        if floor > highestFloor {
            highestFloor = floor
            saveData()
            
            // 階層10で盗賊解放
            if floor >= 10 && !unlockedCharacters.contains(.thief) {
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

