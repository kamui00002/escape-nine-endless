//
//  Achievement.swift
//  EscapeNine-endless-
//
//  Created by Claude Code on 2025/11/28.
//

import Foundation
import Combine

enum Achievement: String, CaseIterable, Codable {
    case firstWin = "初勝利"
    case floor10 = "階層10到達"
    case floor25 = "階層25到達"
    case floor50 = "階層50到達"
    case floor75 = "階層75到達"
    case floor100 = "階層100到達"
    case noSkillWin = "素手の達人"
    case speedRunner = "スピードランナー"
    case survivor = "生存者"
    
    var description: String {
        switch self {
        case .firstWin:
            return "初めてゲームをクリアした"
        case .floor10:
            return "階層10に到達した"
        case .floor25:
            return "階層25に到達した"
        case .floor50:
            return "階層50に到達した"
        case .floor75:
            return "階層75に到達した"
        case .floor100:
            return "階層100に到達した"
        case .noSkillWin:
            return "スキルを使わずに階層10をクリアした"
        case .speedRunner:
            return "BPM180以上で階層20に到達した"
        case .survivor:
            return "階層30をノーミスでクリアした"
        }
    }
    
    var icon: String {
        switch self {
        case .firstWin:
            return "trophy.fill"
        case .floor10:
            return "10.circle.fill"
        case .floor25:
            return "25.circle.fill"
        case .floor50:
            return "50.circle.fill"
        case .floor75:
            return "75.circle.fill"
        case .floor100:
            return "100.circle.fill"
        case .noSkillWin:
            return "hand.raised.fill"
        case .speedRunner:
            return "hare.fill"
        case .survivor:
            return "star.fill"
        }
    }
    
    var color: String {
        switch self {
        case .firstWin:
            return GameColors.accent
        case .floor10:
            return GameColors.available
        case .floor25:
            return GameColors.main
        case .floor50:
            return GameColors.accent
        case .floor75:
            return "#FFD700" // ゴールド
        case .floor100:
            return "#FF4500" // オレンジレッド
        case .noSkillWin:
            return GameColors.available
        case .speedRunner:
            return GameColors.main
        case .survivor:
            return "#FFD700" // ゴールド
        }
    }
}

@MainActor
class AchievementManager: ObservableObject {
    static let shared = AchievementManager()
    
    @Published var unlockedAchievements: Set<Achievement> = []
    @Published var newlyUnlockedAchievement: Achievement?
    
    private let achievementsKey = "unlockedAchievements"
    
    private init() {
        loadAchievements()
    }
    
    func unlock(_ achievement: Achievement) {
        guard !unlockedAchievements.contains(achievement) else { return }
        
        unlockedAchievements.insert(achievement)
        newlyUnlockedAchievement = achievement
        saveAchievements()
        
        // 効果音
        AudioManager.shared.playSoundEffect(.skill)
        
        // 3秒後に通知を非表示
        Task { [weak self] in
            try? await Task.sleep(for: .seconds(3))
            self?.newlyUnlockedAchievement = nil
        }
    }
    
    func checkAchievements(
        floor: Int,
        skillUsed: Bool,
        currentBPM: Double,
        gameWon: Bool
    ) {
        guard gameWon else { return }
        
        // 階層到達実績
        if floor >= 1 { unlock(.firstWin) }
        if floor >= 10 { unlock(.floor10) }
        if floor >= 25 { unlock(.floor25) }
        if floor >= 50 { unlock(.floor50) }
        if floor >= 75 { unlock(.floor75) }
        if floor >= 100 { unlock(.floor100) }
        
        // スキル未使用実績
        if floor >= 10 && !skillUsed {
            unlock(.noSkillWin)
        }
        
        // 高速BPM実績
        if floor >= 20 && currentBPM >= 180 {
            unlock(.speedRunner)
        }
    }
    
    var progress: Double {
        guard !Achievement.allCases.isEmpty else { return 0 }
        return Double(unlockedAchievements.count) / Double(Achievement.allCases.count)
    }
    
    private func loadAchievements() {
        if let data = UserDefaults.standard.data(forKey: achievementsKey),
           let decoded = try? JSONDecoder().decode(Set<Achievement>.self, from: data) {
            unlockedAchievements = decoded
        }
    }
    
    private func saveAchievements() {
        if let encoded = try? JSONEncoder().encode(unlockedAchievements) {
            UserDefaults.standard.set(encoded, forKey: achievementsKey)
        }
    }
}
