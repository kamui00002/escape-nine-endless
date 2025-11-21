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

    init() {
        // TODO: Firebase初期化（Firebase SDKを追加後に有効化）
        // import FirebaseCore
        // FirebaseApp.configure()

        // TODO: AdMob初期化（Google Mobile Ads SDKを追加後に有効化）
        // import GoogleMobileAds
        // GADMobileAds.sharedInstance().start(completionHandler: nil)

        print("[App] アプリ起動 - 収益化サービス準備完了（モック）")
    }

    var body: some Scene {
        WindowGroup {
            HomeView()
                .task {
                    // PurchaseManagerの初期化（StoreKit商品読み込みなど）
                    await purchaseManager.initialize()
                }
        }
    }
}
