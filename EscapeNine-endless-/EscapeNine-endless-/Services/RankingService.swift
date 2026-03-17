//
//  RankingService.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation
import Combine

struct RankingEntry: Codable, Identifiable {
    var id: String = UUID().uuidString
    let floor: Int
    let playerName: String
    let characterType: String
    let timestamp: Date

    private static let dateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateStyle = .short
        formatter.timeStyle = .short
        return formatter
    }()

    var formattedDate: String {
        Self.dateFormatter.string(from: timestamp)
    }
}

class RankingService: ObservableObject {
    static let shared = RankingService()

    @Published private(set) var rankings: [RankingEntry] = []

    private let maxEntries = 100
    private let storageKey = "localRankings"

    private init() {
        loadRankings()
    }

    // MARK: - Submit Score

    func submitScore(floor: Int, characterType: String = "hero") {
        let entry = RankingEntry(
            floor: floor,
            playerName: "あなた",
            characterType: characterType,
            timestamp: Date()
        )

        rankings.append(entry)

        // Sort by floor descending, keep top N
        rankings.sort { $0.floor > $1.floor }
        if rankings.count > maxEntries {
            rankings = Array(rankings.prefix(maxEntries))
        }

        saveRankings()
    }

    // MARK: - Get Rankings

    func getRankings() async -> [RankingEntry] {
        return rankings
    }

    func getTopScore() -> Int {
        return rankings.first?.floor ?? 0
    }

    // MARK: - Persistence

    private func loadRankings() {
        guard let data = UserDefaults.standard.data(forKey: storageKey) else { return }

        do {
            rankings = try JSONDecoder().decode([RankingEntry].self, from: data)
            rankings.sort { $0.floor > $1.floor }
        } catch {
            print("[RankingService] Failed to load rankings: \(error)")
        }
    }

    private func saveRankings() {
        do {
            let data = try JSONEncoder().encode(rankings)
            UserDefaults.standard.set(data, forKey: storageKey)
        } catch {
            print("[RankingService] Failed to save rankings: \(error)")
        }
    }

    // MARK: - Clear (Debug)

    func clearRankings() {
        rankings = []
        UserDefaults.standard.removeObject(forKey: storageKey)
    }
}
