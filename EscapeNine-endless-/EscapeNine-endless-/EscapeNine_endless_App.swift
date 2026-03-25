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
#if canImport(GoogleMobileAds)
import GoogleMobileAds
#endif

@main
struct EscapeNine_endless_App: App {
    @StateObject private var purchaseManager = PurchaseManager.shared
    @StateObject private var gameCenterService = GameCenterService.shared

    init() {
        // #if canImport(FirebaseCore) は firebase-ios-sdk SPMパッケージがリンクされると有効になる
        // FirebaseApp.configure() は GoogleService-Info.plist が存在しないとクラッシュするため
        // plist を配置した後にビルドすること
        #if canImport(FirebaseCore)
        FirebaseApp.configure()
        print("[App] Firebase初期化完了")
        #endif

        print("[App] アプリ起動")
    }

    var body: some Scene {
        WindowGroup {
            HomeView()
                .task {
                    // ATT許可リクエスト → AdMob初期化の順序を保証
                    await requestTrackingAuthorizationAndInitializeAds()
                    // Firebase匿名認証（GoogleService-Info.plist未配置時はモック動作）
                    try? await FirebaseService.shared.signInAnonymously()
                    // PurchaseManagerの初期化
                    await purchaseManager.initialize()
                    // Game Center認証
                    gameCenterService.authenticate()
                }
        }
    }

    /// ATTの許可ダイアログを表示した後にAdMobを初期化する
    @MainActor
    private func requestTrackingAuthorizationAndInitializeAds() async {
        // iOS 14+ でATTダイアログを表示
        if #available(iOS 14, *) {
            let status = await ATTrackingManager.requestTrackingAuthorization()
            print("[App] ATT許可ステータス: \(status.rawValue)")
        }

        // ATT結果に関わらずAdMobを初期化（非パーソナライズ広告にフォールバック）
        #if canImport(GoogleMobileAds)
        MobileAds.shared.start(completionHandler: nil)
        print("[App] AdMob初期化完了")
        #endif

        // AdMobService の広告ユニット事前ロード（SDK の有無に関わらず実行）
        AdMobService.shared.initialize()
    }
}
