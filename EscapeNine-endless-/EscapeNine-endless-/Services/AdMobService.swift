//
//  AdMobService.swift
//  EscapeNine-endless-
//
//  Google AdMob 広告管理サービス
//  Google Mobile Ads SDK未導入時はモック実装で動作する
//

import Foundation
import UIKit
import SwiftUI
import Combine
import os

private let logger = Logger(subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app", category: "AdMobService")
#if canImport(GoogleMobileAds)
import GoogleMobileAds
#endif

// MARK: - AdMob Configuration
struct AdMobConfig {
    // テスト用広告ユニットID（開発時に使用）
    static let testBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716"
    static let testInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910"

    // 本番用広告ユニットID（リリース時に置き換え）
    static var bannerAdUnitId: String {
        #if DEBUG
        return testBannerAdUnitId
        #else
        return "ca-app-pub-5237930968754753/3156438181"
        #endif
    }

    static var interstitialAdUnitId: String {
        #if DEBUG
        return testInterstitialAdUnitId
        #else
        return "ca-app-pub-5237930968754753/7861969950"
        #endif
    }
}

// MARK: - AdMobService
@MainActor
class AdMobService: ObservableObject {
    static let shared = AdMobService()

    // MARK: - Published Properties
    @Published var isBannerAdReady: Bool = false
    @Published var isInterstitialAdReady: Bool = false
    @Published var isAdRemoved: Bool = false

    // MARK: - Private Properties
    private var interstitialLoadAttempts = 0
    private let maxLoadAttempts = 3

    #if canImport(GoogleMobileAds)
    private var interstitialAd: InterstitialAd?
    #endif

    private init() {
        loadAdRemovalState()
    }

    // MARK: - Initialization

    func initialize() {
        guard !isAdRemoved else {
            logger.warning("[AdMobService] 広告削除済み - 初期化スキップ")
            return
        }

        #if canImport(GoogleMobileAds)
        logger.info("[AdMobService] 初期化完了")
        loadInterstitialAd()
        isBannerAdReady = true
        #else
        logger.info("[AdMobService] 初期化 (mock)")
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(1))
            self?.isInterstitialAdReady = true
            self?.isBannerAdReady = true
        }
        #endif
    }

    // MARK: - Banner Ad

    var shouldShowBannerAd: Bool {
        return !isAdRemoved
    }

    // MARK: - Interstitial Ad

    func loadInterstitialAd() {
        guard !isAdRemoved else { return }
        guard interstitialLoadAttempts < maxLoadAttempts else {
            logger.warning("[AdMobService] インタースティシャル広告の読み込み試行回数上限")
            return
        }

        interstitialLoadAttempts += 1

        #if canImport(GoogleMobileAds)
        let request = Request()
        InterstitialAd.load(
            with: AdMobConfig.interstitialAdUnitId,
            request: request
        ) { [weak self] ad, error in
            Task { @MainActor in
                if let error = error {
                    logger.error("[AdMobService] インタースティシャル広告読み込みエラー: \(error.localizedDescription)")
                    return
                }
                self?.interstitialAd = ad
                self?.isInterstitialAdReady = true
                self?.interstitialLoadAttempts = 0
            }
        }
        #else
        logger.info("[AdMobService] インタースティシャル広告を読み込み (mock)")
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .milliseconds(500))
            self?.isInterstitialAdReady = true
            self?.interstitialLoadAttempts = 0
        }
        #endif
    }

    func showInterstitialAd(from viewController: UIViewController? = nil, completion: @escaping (Bool) -> Void) {
        guard !isAdRemoved else {
            completion(true)
            return
        }

        guard isInterstitialAdReady else {
            logger.warning("[AdMobService] インタースティシャル広告が準備できていません")
            completion(false)
            return
        }

        #if canImport(GoogleMobileAds)
        guard let ad = interstitialAd else {
            logger.warning("[AdMobService] インタースティシャル広告オブジェクトがnil")
            isInterstitialAdReady = false
            completion(false)
            loadInterstitialAd()
            return
        }

        guard let rootViewController = viewController ?? getRootViewController() else {
            logger.warning("[AdMobService] rootViewControllerが取得できません")
            completion(false)
            return
        }

        interstitialAd = nil
        isInterstitialAdReady = false

        ad.present(from: rootViewController)
        completion(true)

        // 次の広告を事前読み込み
        loadInterstitialAd()
        #else
        logger.info("[AdMobService] インタースティシャル広告を表示 (mock)")
        isInterstitialAdReady = false
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(1))
            completion(true)
            self?.loadInterstitialAd()
        }
        #endif
    }

    // MARK: - Ad Removal

    func setAdRemoved(_ removed: Bool) {
        isAdRemoved = removed
        UserDefaults.standard.set(removed, forKey: "adRemoved")
        if removed {
            logger.info("[AdMobService] 広告削除が適用されました")
        }
    }

    private func loadAdRemovalState() {
        isAdRemoved = UserDefaults.standard.bool(forKey: "adRemoved")
    }

    // MARK: - Helpers

    func getRootViewController() -> UIViewController? {
        guard let windowScene = UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene })
            .first(where: { $0.activationState == .foregroundActive }) ?? UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene })
            .first else {
            logger.warning("[AdMobService] UIWindowSceneが取得できません")
            return nil
        }

        guard let rootVC = windowScene.windows
            .first(where: { $0.isKeyWindow })?.rootViewController ?? windowScene.windows.first?.rootViewController else {
            logger.warning("[AdMobService] rootViewControllerが取得できません")
            return nil
        }

        // 最前面のViewControllerを取得（presented VCがある場合）
        var topVC = rootVC
        while let presented = topVC.presentedViewController {
            topVC = presented
        }
        return topVC
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
        #if canImport(GoogleMobileAds)
        let bannerView = BannerView(adSize: AdSizeBanner)
        bannerView.adUnitID = AdMobConfig.bannerAdUnitId
        bannerView.rootViewController = AdMobService.shared.getRootViewController()
        bannerView.load(Request())
        return bannerView
        #else
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
        #endif
    }

    func updateUIView(_ uiView: UIView, context: Context) {}
}

// MARK: - Interstitial Ad Presenter (SwiftUI Helper)
struct InterstitialAdPresenter {
    static func show(completion: @escaping (Bool) -> Void) {
        AdMobService.shared.showInterstitialAd(completion: completion)
    }
}
