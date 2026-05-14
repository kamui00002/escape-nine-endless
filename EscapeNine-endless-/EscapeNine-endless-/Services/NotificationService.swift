//
//  NotificationService.swift
//  EscapeNine-endless-
//
//  Sprint 2 Feature 2: 抜かれ通知のためのローカル通知サービス。
//  UNUserNotificationCenter のラッパー + 通知許可リクエスト + 即時通知発火。
//
//  設計典拠: docs/sprint-2-status.md §4 A 案
//

import Foundation
import UserNotifications
import os

private let notificationLogger = Logger(
    subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app",
    category: "Notification"
)

/// ローカル通知サービスのファサード。
///
/// 利用方針:
/// - 起動時に ``requestPermission()`` を 1 回呼ぶ (許可状態は OS が永続化、二度目以降は no-op)
/// - 抜かれ検出時に ``presentOvertakenNotification(newRank:)`` を呼ぶと即時通知が発火
/// - 通知 OFF / 拒否済みのユーザーには no-op (silently fail、強制再要求しない)
@MainActor
final class NotificationService {

    static let shared = NotificationService()

    private let center = UNUserNotificationCenter.current()

    private init() {}

    // MARK: - Permission

    /// 通知許可をリクエストする。初回起動時に 1 回呼ぶ。
    /// 既に許可済 / 拒否済の場合は OS 側で no-op (ダイアログ非表示)。
    func requestPermission() async {
        do {
            let granted = try await center.requestAuthorization(options: [.alert, .badge, .sound])
            notificationLogger.info("[Notification] Permission requested, granted=\(granted, privacy: .public)")
        } catch {
            notificationLogger.error("[Notification] Permission request failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    /// 現在の通知許可ステータスを取得する。
    /// - Returns: ``UNAuthorizationStatus`` (``.authorized`` / ``.denied`` / ``.notDetermined`` 等)
    func authorizationStatus() async -> UNAuthorizationStatus {
        let settings = await center.notificationSettings()
        return settings.authorizationStatus
    }

    // MARK: - Overtaken Notification

    /// 抜かれ通知を即時発火する。
    /// - Parameter newRank: 抜かれた後の新しい順位 (例: 38 位)。
    ///
    /// 通知 OFF / 拒否済のユーザーには何もしない。文面はテンプレート固定:
    /// 「順位が下がりました! 現在 N 位」
    func presentOvertakenNotification(newRank: Int) async {
        let status = await authorizationStatus()
        guard status == .authorized else {
            notificationLogger.info("[Notification] Skipping overtaken notification — auth status: \(status.rawValue, privacy: .public)")
            return
        }

        let content = UNMutableNotificationContent()
        content.title = "順位が下がりました"
        content.body = "誰かに抜かれました! 現在 \(newRank) 位"
        content.sound = .default
        content.userInfo = ["type": "overtaken", "new_rank": newRank]

        // 即時発火 (trigger: nil) でフォアグラウンドでもバナー表示
        let request = UNNotificationRequest(
            identifier: "overtaken-\(Int(Date().timeIntervalSince1970))",
            content: content,
            trigger: nil
        )

        do {
            try await center.add(request)
            notificationLogger.info("[Notification] Overtaken notification scheduled, newRank=\(newRank, privacy: .public)")
            AnalyticsLogger.logOvertakenNotificationShown(newRank: newRank)
        } catch {
            notificationLogger.error("[Notification] Schedule failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    /// テスト用: 全通知をクリアする。
    func clearAllNotifications() {
        center.removeAllDeliveredNotifications()
        center.removeAllPendingNotificationRequests()
        notificationLogger.info("[Notification] All notifications cleared")
    }
}
