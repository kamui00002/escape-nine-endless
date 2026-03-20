//
//  DailyChallengeService.swift
//  EscapeNine-endless-
//
//  デイリーチャレンジのシード生成・管理・記録サービス
//

import Foundation
import Combine

class DailyChallengeService: ObservableObject {
    static let shared = DailyChallengeService()

    @Published private(set) var todaysChallenge: DailyChallenge

    /// ゲーム開始前に GameViewModel が読み取るための pending チャレンジ
    var pendingChallenge: DailyChallenge?

    private let userDefaults = UserDefaults.standard
    private let storageKey = "dailyChallengeHistory"

    private init() {
        todaysChallenge = Self.buildChallenge(for: Self.todayString())
        Self.loadRecord(into: &todaysChallenge, from: UserDefaults.standard, key: "dailyChallengeHistory")
    }

    // MARK: - Today's Date String

    private static func todayString() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd"
        formatter.timeZone = TimeZone(identifier: "UTC")
        return formatter.string(from: Date())
    }

    // MARK: - Challenge Generation（UTC日付ベースのシード）

    private static func buildChallenge(for dateString: String) -> DailyChallenge {
        // 日付文字列からシードを生成
        let seed = dateString.unicodeScalars.reduce(0) { $0 &+ Int($1.value) }
        var rng = SeededRNG(seed: seed)

        let conditionCount = (rng.nextInt() % 2) + 1  // 1〜2個の条件
        var conditions: [ChallengeCondition] = []
        var usedTypes: Set<Int> = []

        for _ in 0..<conditionCount {
            var typeIndex: Int
            repeat {
                typeIndex = rng.nextInt() % 4
            } while usedTypes.contains(typeIndex)
            usedTypes.insert(typeIndex)

            switch typeIndex {
            case 0:
                let characterIndex = rng.nextInt() % CharacterType.allCases.count
                conditions.append(.characterLock(CharacterType.allCases[characterIndex]))
            case 1:
                conditions.append(.noSkillAllowed)
            case 2:
                // Hard は除外し Easy/Normal のみ（デイリーチャレンジの難易度調整）
                let levels: [AILevel] = [.easy, .normal]
                let levelIndex = rng.nextInt() % levels.count
                conditions.append(.forcedAI(levels[levelIndex]))
            case 3:
                // 開始フロアは5の倍数（5〜40の範囲）
                let floorOptions = [5, 10, 15, 20, 25, 30, 35, 40]
                let floorIndex = rng.nextInt() % floorOptions.count
                conditions.append(.startFloor(floorOptions[floorIndex]))
            default:
                break
            }
        }

        return DailyChallenge(date: dateString, conditions: conditions, isCompleted: false, achievedFloor: nil)
    }

    // MARK: - Record Management

    private static func loadRecord(into challenge: inout DailyChallenge, from defaults: UserDefaults, key: String) {
        guard let data = defaults.data(forKey: key),
              let history = try? JSONDecoder().decode([String: ChallengeRecord].self, from: data),
              let record = history[challenge.date] else { return }
        challenge.isCompleted = record.isCompleted
        challenge.achievedFloor = record.achievedFloor
    }

    /// チャレンジ完了を記録する
    func markCompleted(achievedFloor: Int) {
        todaysChallenge.isCompleted = true
        todaysChallenge.achievedFloor = achievedFloor
        saveRecord()
    }

    private func saveRecord() {
        var history = loadAllHistory()
        history[todaysChallenge.date] = ChallengeRecord(
            isCompleted: todaysChallenge.isCompleted,
            achievedFloor: todaysChallenge.achievedFloor
        )
        // 30日分のみ保持
        if history.count > 30 {
            let oldest = history.keys.sorted().first!
            history.removeValue(forKey: oldest)
        }
        if let data = try? JSONEncoder().encode(history) {
            userDefaults.set(data, forKey: storageKey)
        }
    }

    private func loadAllHistory() -> [String: ChallengeRecord] {
        guard let data = userDefaults.data(forKey: storageKey),
              let history = try? JSONDecoder().decode([String: ChallengeRecord].self, from: data) else {
            return [:]
        }
        return history
    }

    // MARK: - Helpers

    /// 今日まだチャレンジしていないか
    var canPlayToday: Bool { !todaysChallenge.isCompleted }
}

// MARK: - Storage Model

private struct ChallengeRecord: Codable {
    let isCompleted: Bool
    let achievedFloor: Int?
}

// MARK: - Seeded Random Number Generator

private struct SeededRNG {
    private var state: Int

    init(seed: Int) {
        state = seed
    }

    mutating func nextInt() -> Int {
        // 簡易LCG乱数生成器
        state = (state &* 1664525 &+ 1013904223) & 0x7fffffff
        return abs(state)
    }
}
