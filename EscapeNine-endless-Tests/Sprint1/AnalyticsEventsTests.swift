//
//  AnalyticsEventsTests.swift
//  EscapeNine-endless-Tests
//
//  Sprint 1 (2026-05-09): AnalyticsEvent / AnalyticsParam / AnalyticsDefeatReason
//  の命名規則 + AnalyticsLogger 便利メソッドの引数組み立てを検証。
//
//  Firebase Analytics への実送信は #if canImport(FirebaseAnalytics) でラップされており、
//  本テストでは log(_:parameters:) は no-op として動作する。検証対象は enum / 定数 / API 形状。
//

import XCTest
@testable import EscapeNine_endless_

final class AnalyticsEventsTests: XCTestCase {

    // MARK: - イベント名 prefix (eg_)

    func testEventNamePrefixes() {
        XCTAssertTrue(AnalyticsEvent.gameStarted.rawValue.hasPrefix("eg_"))
        XCTAssertTrue(AnalyticsEvent.gameOverShown.rawValue.hasPrefix("eg_"))
        XCTAssertTrue(AnalyticsEvent.retryTapped.rawValue.hasPrefix("eg_"))
        XCTAssertTrue(AnalyticsEvent.homeTapped.rawValue.hasPrefix("eg_"))
        XCTAssertTrue(AnalyticsEvent.floorCleared.rawValue.hasPrefix("eg_"))
    }

    // MARK: - イベント名 (snake_case)

    func testEventNamesAreSnakeCase() {
        let allEvents: [AnalyticsEvent] = [
            .gameStarted, .gameOverShown, .retryTapped, .homeTapped, .floorCleared
        ]
        for event in allEvents {
            // 大文字を含まない
            XCTAssertFalse(
                event.rawValue.contains(where: { $0.isUppercase }),
                "Event name '\(event.rawValue)' must not contain uppercase letters."
            )
            // [a-z0-9_] のみで構成 (Firebase の推奨)
            let allowed = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyz0123456789_")
            XCTAssertTrue(
                event.rawValue.unicodeScalars.allSatisfy { allowed.contains($0) },
                "Event name '\(event.rawValue)' contains characters outside [a-z0-9_]."
            )
        }
    }

    func testEventRawValueMappings() {
        // Sprint 1 設計書 (docs/analytics/sprint-1-events.md) と一致していること
        XCTAssertEqual(AnalyticsEvent.gameStarted.rawValue, "eg_game_started")
        XCTAssertEqual(AnalyticsEvent.gameOverShown.rawValue, "eg_game_over_shown")
        XCTAssertEqual(AnalyticsEvent.retryTapped.rawValue, "eg_retry_tapped")
        XCTAssertEqual(AnalyticsEvent.homeTapped.rawValue, "eg_home_tapped")
        XCTAssertEqual(AnalyticsEvent.floorCleared.rawValue, "eg_floor_cleared")
    }

    // MARK: - パラメータキー定義

    func testParamKeys() {
        // 共通
        XCTAssertEqual(AnalyticsParam.floor, "floor")
        XCTAssertEqual(AnalyticsParam.fromFloor, "from_floor")

        // ゲーム開始時
        XCTAssertEqual(AnalyticsParam.isDailyChallenge, "is_daily_challenge")
        XCTAssertEqual(AnalyticsParam.characterId, "character_id")

        // Game Over 時
        XCTAssertEqual(AnalyticsParam.defeatReason, "defeat_reason")
        XCTAssertEqual(AnalyticsParam.nearMissDistance, "near_miss_distance")
        XCTAssertEqual(AnalyticsParam.elapsedSeconds, "elapsed_seconds")

        // リトライ時
        XCTAssertEqual(AnalyticsParam.secondsUntilTap, "seconds_until_tap")

        // 階層クリア時
        XCTAssertEqual(AnalyticsParam.clearSeconds, "clear_seconds")
    }

    func testParamKeysAreSnakeCase() {
        let allKeys: [String] = [
            AnalyticsParam.floor,
            AnalyticsParam.fromFloor,
            AnalyticsParam.isDailyChallenge,
            AnalyticsParam.characterId,
            AnalyticsParam.defeatReason,
            AnalyticsParam.nearMissDistance,
            AnalyticsParam.elapsedSeconds,
            AnalyticsParam.secondsUntilTap,
            AnalyticsParam.clearSeconds
        ]
        let allowed = CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyz0123456789_")
        for key in allKeys {
            XCTAssertFalse(
                key.contains(where: { $0.isUppercase }),
                "Param key '\(key)' must not contain uppercase letters."
            )
            XCTAssertTrue(
                key.unicodeScalars.allSatisfy { allowed.contains($0) },
                "Param key '\(key)' contains characters outside [a-z0-9_]."
            )
            XCTAssertFalse(key.isEmpty, "Param key must not be empty.")
        }
    }

    // MARK: - DefeatReason 値

    func testDefeatReasonRawValues() {
        XCTAssertEqual(AnalyticsDefeatReason.trap.rawValue, "trap")
        XCTAssertEqual(AnalyticsDefeatReason.timeout.rawValue, "timeout")
        XCTAssertEqual(AnalyticsDefeatReason.enemy.rawValue, "enemy")
        XCTAssertEqual(AnalyticsDefeatReason.fall.rawValue, "fall")
        XCTAssertEqual(AnalyticsDefeatReason.unknown.rawValue, "unknown")
    }

    // MARK: - AnalyticsLogger 便利メソッドの API 形状

    /// Firebase が import できない環境では log() は no-op として動作する。
    /// 例外を投げず・クラッシュせず呼び出せること自体を契約として担保する。
    func testLoggerFacadeMethodsDoNotCrash() {
        AnalyticsLogger.logGameStarted(floor: 1, isDailyChallenge: false, characterId: "default")
        AnalyticsLogger.logGameStarted(floor: 9, isDailyChallenge: true, characterId: "warrior")

        AnalyticsLogger.logGameOverShown(
            floor: 5,
            defeatReason: "enemy",
            nearMissDistance: 1,
            elapsedSeconds: 23.4
        )
        AnalyticsLogger.logGameOverShown(
            floor: 5,
            defeatReason: .timeout,
            nearMissDistance: 0,
            elapsedSeconds: 60.0
        )

        AnalyticsLogger.logRetryTapped(fromFloor: 3, secondsUntilTap: 1.2)
        AnalyticsLogger.logHomeTapped(fromFloor: 3)
        AnalyticsLogger.logFloorCleared(floor: 7, clearSeconds: 12.5)

        // 任意の event を直接 log
        AnalyticsLogger.log(.gameStarted)
        AnalyticsLogger.log(.gameStarted, parameters: ["floor": 1, "is_daily_challenge": false])
    }
}
