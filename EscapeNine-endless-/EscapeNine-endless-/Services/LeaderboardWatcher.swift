//
//  LeaderboardWatcher.swift
//  EscapeNine-endless-
//
//  Sprint 2 Feature 2: 抜かれ検出ロジック。
//  GameCenter リーダーボードで自分の順位を取得し、前回値との差分で「抜かれ」を判定。
//
//  設計典拠: docs/sprint-2-status.md §4 A 案
//

import Foundation
import GameKit
import os

private let watcherLogger = Logger(
    subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app",
    category: "LeaderboardWatcher"
)

/// 抜かれ検出のための GameCenter リーダーボード監視ロジック。
///
/// 利用方針:
/// - `HomeView.onAppear` で ``checkAndNotify()`` を 1 回呼ぶ
/// - 内部で GameCenter API → 順位取得 → 前回値比較 → 必要なら通知発火まで完結
/// - レート制限を避けるため最終チェックから ``minCheckInterval`` 秒以内は skip
@MainActor
final class LeaderboardWatcher {

    static let shared = LeaderboardWatcher()

    // MARK: - UserDefaults Keys

    private enum Keys {
        static let lastKnownRank = "leaderboardWatcher.lastKnownRank"
        static let lastCheckedAt = "leaderboardWatcher.lastCheckedAt"
    }

    // MARK: - Tuning

    /// 連続チェックを抑制する最小間隔 (秒)。1 時間に 1 回まで。
    private let minCheckInterval: TimeInterval = 60 * 60

    private let userDefaults = UserDefaults.standard

    private init() {}

    // MARK: - Public

    /// 自分のリーダーボード順位を確認し、前回値より下がっていれば抜かれ通知を発火する。
    ///
    /// 以下のケースでは silently skip する:
    /// - GameCenter 未認証
    /// - 前回チェックから ``minCheckInterval`` 秒以内
    /// - 自分の順位が取得できない (リーダーボード上に存在しない、ネットワーク失敗 等)
    /// - 前回値が未保存 (初回チェック) → 今回値だけ保存して終了
    /// - 順位下落なし (同じ or 上昇)
    func checkAndNotify() async {
        guard GKLocalPlayer.local.isAuthenticated else {
            watcherLogger.info("[LeaderboardWatcher] GameCenter not authenticated, skip")
            return
        }

        if let lastCheckedAt = userDefaults.object(forKey: Keys.lastCheckedAt) as? Date,
           Date().timeIntervalSince(lastCheckedAt) < minCheckInterval {
            watcherLogger.info("[LeaderboardWatcher] Skip — within rate limit window")
            return
        }

        guard let currentRank = await fetchLocalPlayerRank() else {
            watcherLogger.info("[LeaderboardWatcher] Cannot fetch local rank, skip")
            return
        }

        userDefaults.set(Date(), forKey: Keys.lastCheckedAt)

        let previousRank = userDefaults.object(forKey: Keys.lastKnownRank) as? Int

        guard let previousRank else {
            // 初回チェック: 通知なしで現在値だけ保存
            userDefaults.set(currentRank, forKey: Keys.lastKnownRank)
            watcherLogger.info("[LeaderboardWatcher] First check, saved rank=\(currentRank, privacy: .public)")
            return
        }

        if currentRank > previousRank {
            // 順位下落 = 抜かれた
            watcherLogger.info("[LeaderboardWatcher] Overtaken! \(previousRank, privacy: .public) → \(currentRank, privacy: .public)")
            await NotificationService.shared.presentOvertakenNotification(newRank: currentRank)
        } else {
            watcherLogger.info("[LeaderboardWatcher] No overtake (prev=\(previousRank, privacy: .public), now=\(currentRank, privacy: .public))")
        }

        userDefaults.set(currentRank, forKey: Keys.lastKnownRank)
    }

    /// テスト用: 保存された順位履歴をリセットする。
    func resetHistory() {
        userDefaults.removeObject(forKey: Keys.lastKnownRank)
        userDefaults.removeObject(forKey: Keys.lastCheckedAt)
        watcherLogger.info("[LeaderboardWatcher] History reset")
    }

    // MARK: - Private

    /// 自分のローカルプレイヤーの全世界順位を取得する。
    /// - Returns: 順位 (1-indexed)。取得失敗 / リーダーボードに未登録時は nil。
    private func fetchLocalPlayerRank() async -> Int? {
        do {
            let leaderboards = try await GKLeaderboard.loadLeaderboards(
                IDs: [GameCenterService.leaderboardID]
            )
            guard let leaderboard = leaderboards.first else { return nil }

            // range: 1 件だけ (entries 配列は使わない、目的は localPlayerEntry)
            let result = try await leaderboard.loadEntries(
                for: .global,
                timeScope: .allTime,
                range: NSRange(location: 1, length: 1)
            )

            // result.0 = ローカルプレイヤーのエントリ (nil の場合あり)
            guard let localEntry = result.0 else { return nil }
            return localEntry.rank
        } catch {
            watcherLogger.error("[LeaderboardWatcher] Fetch rank error: \(error.localizedDescription, privacy: .public)")
            return nil
        }
    }
}
