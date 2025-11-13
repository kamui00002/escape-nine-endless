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
    
    // MARK: - UserDefaults Keys
    private let highestFloorKey = "highestFloor"
    private let unlockedCharactersKey = "unlockedCharacters"
    private let selectedCharacterKey = "selectedCharacter"
    private let adRemovedKey = "adRemoved"
    
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
    }
    
    private func saveData() {
        UserDefaults.standard.set(highestFloor, forKey: highestFloorKey)
        UserDefaults.standard.set(unlockedCharacters.map { $0.rawValue }, forKey: unlockedCharactersKey)
        UserDefaults.standard.set(selectedCharacter.rawValue, forKey: selectedCharacterKey)
        UserDefaults.standard.set(adRemoved, forKey: adRemovedKey)
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

