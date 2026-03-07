//
//  EscapeNine_endless_App.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

@main
struct EscapeNine_endless_App: App {
    @StateObject private var purchaseManager = PurchaseManager.shared
    @StateObject private var gameCenterService = GameCenterService.shared

    init() {
        // TODO: Firebase初期化（Firebase SDKを追加後に有効化）
        // import FirebaseCore
        // FirebaseApp.configure()

        // TODO: AdMob初期化（Google Mobile Ads SDKを追加後に有効化）
        // import GoogleMobileAds
        // GADMobileAds.sharedInstance().start(completionHandler: nil)

        print("[App] アプリ起動")
    }

    var body: some Scene {
        WindowGroup {
            HomeView()
                .task {
                    // PurchaseManagerの初期化
                    await purchaseManager.initialize()
                    // Game Center認証
                    gameCenterService.authenticate()
                }
        }
    }
}
