//
//  ConversionService.swift
//  EscapeNine-endless-
//
//  Google Ads コンバージョン計測サービス（Firebase Analytics 経由）
//  GA4 → Google Ads 連携によりコンバージョンとしてインポート可能
//

import Foundation
import os
#if canImport(FirebaseAnalytics)
import FirebaseAnalytics
#endif

private let logger = Logger(subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app", category: "ConversionService")
@MainActor
final class ConversionService {
    static let shared = ConversionService()
    private init() {}

    /// アプリ起動時に呼び出し（Firebase Analytics の自動 first_open / session_start に加え、明示イベント送信）
    func trackAppOpen() {
        #if canImport(FirebaseAnalytics)
        Analytics.logEvent("app_open", parameters: nil)
        logger.info("[Conversion] app_open event logged")
        #else
        logger.info("[Conversion] FirebaseAnalytics not available")
        #endif
    }

    /// チュートリアル完了
    func trackTutorialComplete() {
        #if canImport(FirebaseAnalytics)
        Analytics.logEvent(AnalyticsEventTutorialComplete, parameters: nil)
        #endif
    }

    // 階層クリアは AnalyticsLogger.logFloorCleared (= eg_floor_cleared) に一本化済み。
    // Google Ads コンバージョンも eg_floor_cleared を GA4 → Ads でインポートして使う。
    // 旧 trackFloorClear("floor_clear") は呼び出し元ゼロの dead code だったため削除 (2026-05-14)。

    /// 課金完了（Google Ads の Purchase コンバージョン用）
    func trackPurchase(productId: String, value: Double, currency: String = "JPY") {
        #if canImport(FirebaseAnalytics)
        Analytics.logEvent(AnalyticsEventPurchase, parameters: [
            AnalyticsParameterItemID: productId,
            AnalyticsParameterValue: value,
            AnalyticsParameterCurrency: currency
        ])
        #endif
    }
}
