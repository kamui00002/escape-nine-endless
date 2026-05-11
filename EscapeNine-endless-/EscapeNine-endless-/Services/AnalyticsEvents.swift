//
//  AnalyticsEvents.swift
//  EscapeNine-endless-
//
//  Sprint 1 (2026-05-09): KPI 計測のためのカスタム Analytics イベント定義。
//  関連会議録: 2026-05-09 [会議録] Escape-Nine 戦略会議 統合 (26 名 35 ラウンド)
//  設計書: docs/analytics/sprint-1-events.md
//
//  本ファイルは Firebase Analytics の薄いラッパーで、依存ゼロの util として
//  動作する。Firebase 初期化は EscapeNine_endless_App.swift 側で完結している
//  ため、本ファイルでは初期化を行わない (重複初期化禁止)。
//

import Foundation
import os
#if canImport(FirebaseAnalytics)
import FirebaseAnalytics
#endif

// MARK: - Logger

private let analyticsLogger = Logger(
    subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app",
    category: "Analytics"
)

// MARK: - Event Names

/// Escape-Nine カスタム Analytics イベント (Sprint 1)。
///
/// 命名規則:
/// - prefix `eg_` (Escape-Nine の略。Firebase 予約イベント・第三者 SDK との衝突回避)
/// - スネークケース、過去形動詞
///
/// Sprint 1 で計測する 5 イベント:
/// - ``gameStarted``    ゲーム開始時 (1 階表示フレーム)
/// - ``gameOverShown``  Game Over 画面の表示完了時
/// - ``retryTapped``    Game Over 画面の「もう一回」タップ時
/// - ``homeTapped``     Game Over 画面の「ホームへ」タップ時
/// - ``floorCleared``   階層クリア時 (次階層に進む直前)
///
/// 派生 KPI:
/// - 1 階離脱率 = `gameStarted` 母数のうち `floorCleared (floor>=1)` に到達しなかった割合
/// - Game Over → リトライ率 = `gameOverShown` のうち `retryTapped` に至った割合
enum AnalyticsEvent: String {
    case gameStarted = "eg_game_started"
    case gameOverShown = "eg_game_over_shown"
    case retryTapped = "eg_retry_tapped"
    case homeTapped = "eg_home_tapped"
    case floorCleared = "eg_floor_cleared"
}

// MARK: - Parameter Keys

/// Analytics イベントのパラメータキー定義。
///
/// 命名規則:
/// - スネークケース (Firebase 推奨)
/// - 1 イベント最大 25 個まで (Firebase 制限。現状最大 4 個で十分マージンあり)
enum AnalyticsParam {
    // 共通
    static let floor = "floor"
    static let fromFloor = "from_floor"

    // ゲーム開始時
    static let isDailyChallenge = "is_daily_challenge"
    static let characterId = "character_id"

    // Game Over 時
    static let defeatReason = "defeat_reason"
    static let nearMissDistance = "near_miss_distance"
    static let elapsedSeconds = "elapsed_seconds"

    // リトライ時
    static let secondsUntilTap = "seconds_until_tap"

    // 階層クリア時
    static let clearSeconds = "clear_seconds"
}

// MARK: - Defeat Reason Values

/// `defeat_reason` パラメータの推奨値。
/// 文字列の表記揺れを避けるため、enum 経由で送信することを推奨。
enum AnalyticsDefeatReason: String {
    case trap
    case timeout
    case enemy
    case fall
    case unknown
}

// MARK: - Logger Facade

/// Sprint 1 の Analytics ログ送信ファサード。
///
/// 利用方針:
/// - 呼び出し側 (ViewModel / Game ロジック) はこの struct の便利メソッドを使う
/// - 直接 ``log(_:parameters:)`` を呼んでもよいが、その場合は ``AnalyticsParam`` の定数を使う
/// - Firebase が import できない環境 (テスト等) では no-op として動作する
struct AnalyticsLogger {

    // MARK: - Core

    /// 任意のカスタムイベントを送信する。
    /// - Parameters:
    ///   - event: 送信するイベント
    ///   - parameters: パラメータ辞書 (キーは ``AnalyticsParam`` を使用)
    static func log(_ event: AnalyticsEvent, parameters: [String: Any] = [:]) {
        #if canImport(FirebaseAnalytics)
        Analytics.logEvent(event.rawValue, parameters: parameters.isEmpty ? nil : parameters)
        #endif
        analyticsLogger.debug("Analytics event: \(event.rawValue, privacy: .public) params=\(String(describing: parameters), privacy: .public)")
    }

    // MARK: - Sprint 1 便利メソッド

    /// `eg_game_started` を送信する。ゲーム開始 (1 階表示フレーム) で呼び出す。
    /// - Parameters:
    ///   - floor: 開始階層 (通常 1。コンティニュー復帰時のみ別値)
    ///   - isDailyChallenge: デイリーチャレンジモードかどうか
    ///   - characterId: プレイヤーキャラ識別子 (デフォルトは `"default"`)
    static func logGameStarted(
        floor: Int,
        isDailyChallenge: Bool,
        characterId: String
    ) {
        log(.gameStarted, parameters: [
            AnalyticsParam.floor: floor,
            AnalyticsParam.isDailyChallenge: isDailyChallenge,
            AnalyticsParam.characterId: characterId
        ])
    }

    /// `eg_game_over_shown` を送信する。Game Over 画面の表示完了時に呼び出す。
    /// - Parameters:
    ///   - floor: 死亡階層
    ///   - defeatReason: 死亡原因 (推奨: ``AnalyticsDefeatReason``)
    ///   - nearMissDistance: 「あと N マスで生存」の N 値 (惜しさメーター)
    ///   - elapsedSeconds: このゲームで経過した秒数 (60 秒問題分析用)
    static func logGameOverShown(
        floor: Int,
        defeatReason: String,
        nearMissDistance: Int,
        elapsedSeconds: Double
    ) {
        log(.gameOverShown, parameters: [
            AnalyticsParam.floor: floor,
            AnalyticsParam.defeatReason: defeatReason,
            AnalyticsParam.nearMissDistance: nearMissDistance,
            AnalyticsParam.elapsedSeconds: elapsedSeconds
        ])
    }

    /// `eg_game_over_shown` を ``AnalyticsDefeatReason`` 経由で送信する便利オーバーロード。
    static func logGameOverShown(
        floor: Int,
        defeatReason: AnalyticsDefeatReason,
        nearMissDistance: Int,
        elapsedSeconds: Double
    ) {
        logGameOverShown(
            floor: floor,
            defeatReason: defeatReason.rawValue,
            nearMissDistance: nearMissDistance,
            elapsedSeconds: elapsedSeconds
        )
    }

    /// `eg_retry_tapped` を送信する。Game Over 画面の「もう一回」タップ時に呼び出す。
    /// - Parameters:
    ///   - fromFloor: 直前の死亡階層
    ///   - secondsUntilTap: Game Over 表示〜タップまでの遅延秒数
    static func logRetryTapped(
        fromFloor: Int,
        secondsUntilTap: Double
    ) {
        log(.retryTapped, parameters: [
            AnalyticsParam.fromFloor: fromFloor,
            AnalyticsParam.secondsUntilTap: secondsUntilTap
        ])
    }

    /// `eg_home_tapped` を送信する。Game Over 画面の「ホームへ」タップ時に呼び出す。
    /// - Parameter fromFloor: 直前の死亡階層 (どの階層で諦めたか)
    static func logHomeTapped(fromFloor: Int) {
        log(.homeTapped, parameters: [
            AnalyticsParam.fromFloor: fromFloor
        ])
    }

    /// `eg_floor_cleared` を送信する。階層クリア時 (次階層に進む直前) に呼び出す。
    /// - Parameters:
    ///   - floor: クリアした階層番号
    ///   - clearSeconds: この階層のクリア所要時間
    static func logFloorCleared(
        floor: Int,
        clearSeconds: Double
    ) {
        log(.floorCleared, parameters: [
            AnalyticsParam.floor: floor,
            AnalyticsParam.clearSeconds: clearSeconds
        ])
    }
}
