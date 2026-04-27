//
//  EscapeNine_endless_App.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI
import AppTrackingTransparency
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

@main
struct EscapeNine_endless_App: App {
    @StateObject private var purchaseManager = PurchaseManager.shared
    @StateObject private var gameCenterService = GameCenterService.shared

    init() {
        #if canImport(FirebaseCore)
        FirebaseApp.configure()
        print("[App] Firebase初期化完了")
        #endif

        #if canImport(FirebaseAnalytics)
        Analytics.setAnalyticsCollectionEnabled(true)
        Analytics.setConsent([
            .analyticsStorage: .granted,
            .adStorage: .granted,
            .adUserData: .granted,
            .adPersonalization: .granted
        ])
        print("[App] Firebase Analytics有効化完了")
        #endif

        #if canImport(FacebookCore)
        ApplicationDelegate.shared.application(
            UIApplication.shared,
            didFinishLaunchingWithOptions: nil
        )
        print("[App] Facebook SDK初期化完了")
        #endif

        print("[App] アプリ起動")
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
        print("[App] ATTステータス: \(status.rawValue)")

        #if canImport(GoogleMobileAds)
        await MobileAds.shared.start()
        print("[App] AdMob初期化完了")
        #endif

        AdMobService.shared.initialize()
    }
}
