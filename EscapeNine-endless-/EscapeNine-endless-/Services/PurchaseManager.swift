//
//  PurchaseManager.swift
//  EscapeNine-endless-
//
//  課金状態を統合管理し、ViewModelとの連携を提供
//

import Foundation
import SwiftUI
import Combine

// MARK: - PurchaseManager
@MainActor
class PurchaseManager: ObservableObject {
    static let shared = PurchaseManager()
    
    // MARK: - Published Properties
    @Published var isLoading: Bool = false
    @Published var showPurchaseAlert: Bool = false
    @Published var alertTitle: String = ""
    @Published var alertMessage: String = ""
    
    // MARK: - Dependencies
    private let storeKitService = StoreKitService.shared
    private let adMobService = AdMobService.shared
    private var cancellables = Set<AnyCancellable>()
    
    private init() {
        setupBindings()
    }
    
    // MARK: - Setup
    
    private func setupBindings() {
        // StoreKitServiceの状態変更を監視
        storeKitService.$purchaseState
            .receive(on: DispatchQueue.main)
            .sink { [weak self] state in
                switch state {
                case .loading:
                    self?.isLoading = true
                case .purchased:
                    self?.isLoading = false
                    self?.showSuccessAlert()
                case .failed(let error):
                    self?.isLoading = false
                    self?.showErrorAlert(error)
                default:
                    self?.isLoading = false
                }
            }
            .store(in: &cancellables)
    }
    
    // MARK: - Initialization
    
    /// アプリ起動時の初期化
    func initialize() async {
        // 商品情報を読み込み
        await storeKitService.loadProducts()
        
        // AdMobを初期化
        adMobService.initialize()
        
        // 広告削除状態を同期
        if storeKitService.isAdRemoved {
            adMobService.setAdRemoved(true)
        }
        
        print("[PurchaseManager] 初期化完了")
    }
    
    // MARK: - Character Purchase
    
    /// キャラクターを購入
    func purchaseCharacter(_ characterType: CharacterType) async -> Bool {
        let productID: ProductID
        
        switch characterType {
        case .wizard:
            productID = .characterMage
        case .elf:
            productID = .characterElf
        default:
            print("[PurchaseManager] このキャラクターは購入対象外です: \(characterType.rawValue)")
            return false
        }
        
        do {
            let success = try await storeKitService.purchase(productID: productID)
            
            if success {
                // PlayerViewModelに通知してキャラクターをアンロック
                NotificationCenter.default.post(
                    name: .characterPurchased,
                    object: characterType
                )
            }
            
            return success
        } catch {
            print("[PurchaseManager] キャラクター購入エラー: \(error.localizedDescription)")
            return false
        }
    }
    
    /// キャラクターが購入済みかどうか
    func isCharacterPurchased(_ characterType: CharacterType) -> Bool {
        return storeKitService.isCharacterPurchased(characterType)
    }
    
    /// キャラクターの価格を取得
    func characterPrice(_ characterType: CharacterType) -> String {
        switch characterType {
        case .wizard:
            return storeKitService.priceString(for: .characterMage)
        case .elf:
            return storeKitService.priceString(for: .characterElf)
        default:
            return "無料"
        }
    }
    
    // MARK: - Ad Removal Purchase
    
    /// 広告削除を購入
    func purchaseAdRemoval() async -> Bool {
        do {
            let success = try await storeKitService.purchase(productID: .removeAds)
            
            if success {
                adMobService.setAdRemoved(true)
                
                // PlayerViewModelに通知
                NotificationCenter.default.post(name: .adRemovalPurchased, object: nil)
            }
            
            return success
        } catch {
            print("[PurchaseManager] 広告削除購入エラー: \(error.localizedDescription)")
            return false
        }
    }
    
    /// 広告が削除されているかどうか
    var isAdRemoved: Bool {
        return storeKitService.isAdRemoved || adMobService.isAdRemoved
    }
    
    /// 広告削除の価格を取得
    var adRemovalPrice: String {
        return storeKitService.priceString(for: .removeAds)
    }
    
    // MARK: - Restore Purchases
    
    /// 購入を復元
    func restorePurchases() async {
        isLoading = true
        await storeKitService.restorePurchases()
        
        // 復元後の状態を同期
        if storeKitService.isAdRemoved {
            adMobService.setAdRemoved(true)
            NotificationCenter.default.post(name: .adRemovalPurchased, object: nil)
        }
        
        // キャラクターの復元を通知
        if storeKitService.isCharacterPurchased(.wizard) {
            NotificationCenter.default.post(name: .characterPurchased, object: CharacterType.wizard)
        }
        if storeKitService.isCharacterPurchased(.elf) {
            NotificationCenter.default.post(name: .characterPurchased, object: CharacterType.elf)
        }
        
        isLoading = false
        
        showAlert(title: "復元完了", message: "購入の復元が完了しました")
    }
    
    // MARK: - Alert Helpers
    
    private func showSuccessAlert() {
        showAlert(title: "購入完了", message: "ご購入ありがとうございます！")
    }
    
    private func showErrorAlert(_ error: Error) {
        showAlert(title: "エラー", message: error.localizedDescription)
    }
    
    private func showAlert(title: String, message: String) {
        alertTitle = title
        alertMessage = message
        showPurchaseAlert = true
    }
}

// MARK: - Notification Names
extension Notification.Name {
    static let characterPurchased = Notification.Name("characterPurchased")
    static let adRemovalPurchased = Notification.Name("adRemovalPurchased")
}

// MARK: - SwiftUI Purchase Button View
struct PurchaseButton: View {
    let title: String
    let price: String
    let isPurchased: Bool
    let action: () async -> Void
    
    @State private var isProcessing = false
    
    var body: some View {
        Button(action: {
            guard !isPurchased && !isProcessing else { return }
            
            isProcessing = true
            Task {
                await action()
                isProcessing = false
            }
        }) {
            HStack {
                Text(isPurchased ? "購入済み" : title)
                    .font(.fantasyBody())
                
                if !isPurchased {
                    Spacer()
                    
                    if isProcessing {
                        ProgressView()
                            .progressViewStyle(CircularProgressViewStyle(tint: .white))
                    } else {
                        Text(price)
                            .font(.fantasyNumber())
                    }
                }
            }
            .foregroundColor(.white)
            .frame(maxWidth: .infinity)
            .padding()
            .background(
                LinearGradient(
                    colors: isPurchased ? [
                        Color(hex: GameColors.available),
                        Color(hex: GameColors.main)
                    ] : [
                        Color(hex: GameColors.warning),
                        Color(hex: GameColors.enemy)
                    ],
                    startPoint: .leading,
                    endPoint: .trailing
                )
            )
            .cornerRadius(12)
        }
        .disabled(isPurchased || isProcessing)
    }
}

// MARK: - Purchase Alert Modifier
struct PurchaseAlertModifier: ViewModifier {
    @ObservedObject var purchaseManager = PurchaseManager.shared
    
    func body(content: Content) -> some View {
        content
            .alert(purchaseManager.alertTitle, isPresented: $purchaseManager.showPurchaseAlert) {
                Button("OK", role: .cancel) {}
            } message: {
                Text(purchaseManager.alertMessage)
            }
    }
}

extension View {
    func purchaseAlert() -> some View {
        modifier(PurchaseAlertModifier())
    }
}

// MARK: - Loading Overlay Modifier
struct LoadingOverlayModifier: ViewModifier {
    @ObservedObject var purchaseManager = PurchaseManager.shared
    
    func body(content: Content) -> some View {
        ZStack {
            content
            
            if purchaseManager.isLoading {
                Color.black.opacity(0.4)
                    .ignoresSafeArea()
                
                VStack(spacing: 16) {
                    ProgressView()
                        .progressViewStyle(CircularProgressViewStyle(tint: .white))
                        .scaleEffect(1.5)
                    
                    Text("処理中...")
                        .font(.fantasyBody())
                        .foregroundColor(.white)
                }
                .padding(32)
                .background(
                    RoundedRectangle(cornerRadius: 16)
                        .fill(Color(hex: GameColors.backgroundSecondary))
                )
            }
        }
    }
}

extension View {
    func purchaseLoadingOverlay() -> some View {
        modifier(LoadingOverlayModifier())
    }
}
