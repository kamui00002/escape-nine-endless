//
//  AdMobService.swift
//  EscapeNine-endless-
//
//  Google AdMob 広告管理サービス
//

import Foundation
import SwiftUI
import Combine

// MARK: - AdMob Configuration
struct AdMobConfig {
    // TODO: 本番用の広告ユニットIDに置き換え
    
    // テスト用広告ユニットID（開発時に使用）
    static let testBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716"
    static let testInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910"
    
    // 本番用広告ユニットID（リリース時に置き換え）
    static var bannerAdUnitId: String {
        #if DEBUG
        return testBannerAdUnitId
        #else
        return "ca-app-pub-XXXXXXXXXXXXX/YYYYYYYYYY" // TODO: 本番IDに置き換え
        #endif
    }
    
    static var interstitialAdUnitId: String {
        #if DEBUG
        return testInterstitialAdUnitId
        #else
        return "ca-app-pub-XXXXXXXXXXXXX/ZZZZZZZZZZ" // TODO: 本番IDに置き換え
        #endif
    }
    
    // AdMobアプリID（Info.plistに設定）
    // GADApplicationIdentifier: ca-app-pub-XXXXXXXXXXXXX~YYYYYYYYYY
}

// MARK: - AdMobService
class AdMobService: ObservableObject {
    static let shared = AdMobService()
    
    // MARK: - Published Properties
    @Published var isBannerAdReady: Bool = false
    @Published var isInterstitialAdReady: Bool = false
    @Published var isAdRemoved: Bool = false
    
    // MARK: - Private Properties
    private var interstitialLoadAttempts = 0
    private let maxLoadAttempts = 3
    private var cancellables = Set<AnyCancellable>()
    
    private init() {
        loadAdRemovalState()
    }
    
    // MARK: - Initialization
    
    /// AdMobを初期化
    func initialize() {
        guard !isAdRemoved else {
            print("[AdMobService] 広告削除済み - 初期化スキップ")
            return
        }
        
        // TODO: Google Mobile Ads SDK 初期化
        // GADMobileAds.sharedInstance().start { status in
        //     print("[AdMobService] 初期化完了")
        //     self.loadInterstitialAd()
        // }
        
        print("[AdMobService] 初期化 (mock)")
        
        // モック: インタースティシャル広告を準備完了状態に
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) {
            self.isInterstitialAdReady = true
            self.isBannerAdReady = true
        }
    }
    
    // MARK: - Banner Ad
    
    /// バナー広告を表示すべきかどうか
    var shouldShowBannerAd: Bool {
        return !isAdRemoved
    }
    
    // MARK: - Interstitial Ad
    
    /// インタースティシャル広告を読み込み
    func loadInterstitialAd() {
        guard !isAdRemoved else { return }
        guard interstitialLoadAttempts < maxLoadAttempts else {
            print("[AdMobService] インタースティシャル広告の読み込み試行回数上限")
            return
        }
        
        interstitialLoadAttempts += 1
        
        // TODO: Google Mobile Ads SDK 実装
        // let request = GADRequest()
        // GADInterstitialAd.load(
        //     withAdUnitID: AdMobConfig.interstitialAdUnitId,
        //     request: request
        // ) { [weak self] ad, error in
        //     if let error = error {
        //         print("[AdMobService] インタースティシャル広告読み込みエラー: \(error.localizedDescription)")
        //         return
        //     }
        //     self?.interstitialAd = ad
        //     self?.isInterstitialAdReady = true
        //     self?.interstitialLoadAttempts = 0
        // }
        
        print("[AdMobService] インタースティシャル広告を読み込み (mock)")
        
        // モック実装
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            self.isInterstitialAdReady = true
            self.interstitialLoadAttempts = 0
        }
    }
    
    /// インタースティシャル広告を表示
    func showInterstitialAd(from viewController: UIViewController? = nil, completion: @escaping (Bool) -> Void) {
        guard !isAdRemoved else {
            print("[AdMobService] 広告削除済み - 表示スキップ")
            completion(true)
            return
        }
        
        guard isInterstitialAdReady else {
            print("[AdMobService] インタースティシャル広告が準備できていません")
            completion(false)
            return
        }
        
        // TODO: Google Mobile Ads SDK 実装
        // guard let ad = interstitialAd,
        //       let rootViewController = viewController ?? UIApplication.shared.windows.first?.rootViewController else {
        //     completion(false)
        //     return
        // }
        //
        // ad.fullScreenContentDelegate = self
        // ad.present(fromRootViewController: rootViewController)
        
        print("[AdMobService] インタースティシャル広告を表示 (mock)")
        
        // モック実装: 広告表示をシミュレート
        isInterstitialAdReady = false
        
        // 1秒後に広告終了をシミュレート
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) {
            completion(true)
            // 次の広告を読み込み
            self.loadInterstitialAd()
        }
    }
    
    // MARK: - Ad Removal
    
    /// 広告削除状態を設定
    func setAdRemoved(_ removed: Bool) {
        isAdRemoved = removed
        UserDefaults.standard.set(removed, forKey: "adRemoved")
        
        if removed {
            print("[AdMobService] 広告削除が適用されました")
        }
    }
    
    /// 広告削除状態を読み込み
    private func loadAdRemovalState() {
        isAdRemoved = UserDefaults.standard.bool(forKey: "adRemoved")
    }
}

// MARK: - SwiftUI Banner Ad View
struct BannerAdView: View {
    @ObservedObject private var adService = AdMobService.shared
    
    var body: some View {
        if adService.shouldShowBannerAd {
            BannerAdRepresentable()
                .frame(height: 50)
        }
    }
}

// MARK: - UIViewRepresentable for Banner Ad
struct BannerAdRepresentable: UIViewRepresentable {
    func makeUIView(context: Context) -> UIView {
        // TODO: Google Mobile Ads SDK 実装
        // let bannerView = GADBannerView(adSize: GADAdSizeBanner)
        // bannerView.adUnitID = AdMobConfig.bannerAdUnitId
        // bannerView.rootViewController = UIApplication.shared.windows.first?.rootViewController
        // bannerView.load(GADRequest())
        // return bannerView
        
        // モック実装: プレースホルダービュー
        let placeholderView = UIView()
        placeholderView.backgroundColor = UIColor.systemGray5
        
        let label = UILabel()
        label.text = "Banner Ad (Mock)"
        label.textAlignment = .center
        label.textColor = .secondaryLabel
        label.font = .systemFont(ofSize: 12)
        label.translatesAutoresizingMaskIntoConstraints = false
        
        placeholderView.addSubview(label)
        NSLayoutConstraint.activate([
            label.centerXAnchor.constraint(equalTo: placeholderView.centerXAnchor),
            label.centerYAnchor.constraint(equalTo: placeholderView.centerYAnchor)
        ])
        
        return placeholderView
    }
    
    func updateUIView(_ uiView: UIView, context: Context) {}
}

// MARK: - Interstitial Ad Presenter (SwiftUI Helper)
struct InterstitialAdPresenter {
    static func show(completion: @escaping (Bool) -> Void) {
        // ルートビューコントローラを取得
        guard let windowScene = UIApplication.shared.connectedScenes.first as? UIWindowScene,
              let rootVC = windowScene.windows.first?.rootViewController else {
            completion(false)
            return
        }
        
        AdMobService.shared.showInterstitialAd(from: rootVC, completion: completion)
    }
}

// MARK: - AdMob Setup Instructions
/*
 AdMob広告の設定手順:
 
 1. Google AdMob (https://admob.google.com) でアカウントを作成
 
 2. アプリを登録:
    - iOSアプリを追加
    - アプリIDをメモ (ca-app-pub-XXXXXXXXXXXXX~YYYYYYYYYY)
 
 3. 広告ユニットを作成:
    - バナー広告ユニットを作成 → AdMobConfig.bannerAdUnitId に設定
    - インタースティシャル広告ユニットを作成 → AdMobConfig.interstitialAdUnitId に設定
 
 4. SPMで依存関係を追加:
    File > Add Package Dependencies... で以下を追加:
    - https://github.com/googleads/swift-package-manager-google-mobile-ads
    - Product: GoogleMobileAds
 
 5. Info.plist に設定を追加:
    <key>GADApplicationIdentifier</key>
    <string>ca-app-pub-XXXXXXXXXXXXX~YYYYYYYYYY</string>
    
    <key>SKAdNetworkItems</key>
    <array>
      <dict>
        <key>SKAdNetworkIdentifier</key>
        <string>cstr6suwn9.skadnetwork</string>
      </dict>
      <!-- 他のSKAdNetworkIdentifierも追加 -->
    </array>
 
 6. AppDelegate.swift または App.swift で初期化:
    import GoogleMobileAds
    
    @main
    struct EscapeNineEndlessApp: App {
        init() {
            GADMobileAds.sharedInstance().start(completionHandler: nil)
        }
        ...
    }
 
 7. App Tracking Transparency (iOS 14.5+):
    Info.plist に追加:
    <key>NSUserTrackingUsageDescription</key>
    <string>パーソナライズされた広告を表示するために使用します</string>
    
    ATTrackingManager.requestTrackingAuthorization を呼び出し
*/
