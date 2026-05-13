//
//  EscapeNine_endless_App.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import AppTrackingTransparency
import os
#if canImport(FirebaseCore)
import FirebaseCore
#endif
#if canImport(FirebaseAnalytics)
import FirebaseAnalytics
#endif
#if canImport(GoogleMobileAds)
import GoogleMobileAds
#endif
#if canImport(FacebookCore)
import FacebookCore
#endif

private let logger = Logger(subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app", category: "App")
@main
struct EscapeNine_endless_App: App {
    @StateObject private var purchaseManager = PurchaseManager.shared
    @StateObject private var gameCenterService = GameCenterService.shared

    init() {
        #if canImport(FirebaseCore)
        FirebaseApp.configure()
        logger.info("[App] Firebase初期化完了")
        #endif

        #if canImport(FirebaseAnalytics)
        Analytics.setAnalyticsCollectionEnabled(true)
        Analytics.setConsent([
            .analyticsStorage: .granted,
            .adStorage: .granted,
            .adUserData: .granted,
            .adPersonalization: .granted
        ])
        logger.info("[App] Firebase Analytics有効化完了")
        #endif

        #if canImport(FacebookCore)
        ApplicationDelegate.shared.application(
            UIApplication.shared,
            didFinishLaunchingWithOptions: nil
        )
        logger.info("[App] Facebook SDK初期化完了")
        #endif

        logger.info("[App] アプリ起動")

        #if canImport(FirebaseAnalytics)
        // 起動 sentinel event。Firebase Analytics が無音故障した場合 (5/13 まで Conv 0 だった事案) の
        // 再発検知用に、起動毎に必ず 1 件 logEvent + appInstanceID 確認を行う。
        let appVersion = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "unknown"
        Analytics.logEvent("eg_app_init_ok", parameters: ["version": appVersion])
        if Analytics.appInstanceID() == nil {
            logger.fault("[App] Firebase Analytics not initialized (appInstanceID is nil)")
        } else {
            logger.info("[App] Firebase Analytics appInstanceID verified")
        }
        #endif
    }

    var body: some Scene {
        WindowGroup {
            HomeView()
                .task {
                    // ATTダイアログ表示後にAdMobを初期化
                    await requestTrackingAndInitializeAds()
                    // Google Ads コンバージョン計測
                    ConversionService.shared.trackAppOpen()
                    // Firebase匿名認証
                    try? await FirebaseService.shared.signInAnonymously()
                    // PurchaseManagerの初期化
                    await purchaseManager.initialize()
                    // Game Center認証
                    gameCenterService.authenticate()
                }
        }
    }

    private func requestTrackingAndInitializeAds() async {
        // ATTダイアログはアプリがアクティブな状態で表示する必要がある
        // 少し待ってUIが表示された後にリクエスト
        try? await Task.sleep(for: .seconds(1))

        let status = await ATTrackingManager.requestTrackingAuthorization()
        logger.info("[App] ATTステータス: \(status.rawValue)")

        #if canImport(GoogleMobileAds)
        await MobileAds.shared.start()
        logger.info("[App] AdMob初期化完了")
        #endif

        AdMobService.shared.initialize()
    }
}
