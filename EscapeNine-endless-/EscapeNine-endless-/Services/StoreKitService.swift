//
//  StoreKitService.swift
//  EscapeNine-endless-
//
//  StoreKit 2 を使用したアプリ内課金サービス
//

import Foundation
import StoreKit
import Combine

// MARK: - Product Identifiers
enum ProductID: String, CaseIterable {
    case characterMage = "com.escapenine.character.mage"      // 魔法使い ¥240
    case characterElf = "com.escapenine.character.elf"        // エルフ ¥240
    case removeAds = "com.escapenine.removeads"               // 広告削除 ¥480
    
    var displayName: String {
        switch self {
        case .characterMage: return "魔法使い"
        case .characterElf: return "エルフ"
        case .removeAds: return "広告削除"
        }
    }
    
    var description: String {
        switch self {
        case .characterMage: return "透明化スキルを持つ魔法使いキャラクター"
        case .characterElf: return "拘束スキルを持つエルフキャラクター"
        case .removeAds: return "すべての広告を非表示にします"
        }
    }
}

// MARK: - Purchase State
enum PurchaseState {
    case idle
    case loading
    case purchased
    case failed(Error)
    case pending
}

// MARK: - StoreKitService
@MainActor
class StoreKitService: ObservableObject {
    static let shared = StoreKitService()
    
    // MARK: - Published Properties
    @Published private(set) var products: [Product] = []
    @Published private(set) var purchasedProductIDs: Set<String> = []
    @Published private(set) var purchaseState: PurchaseState = .idle
    @Published var errorMessage: String?
    
    // MARK: - Private Properties
    private var updateListenerTask: Task<Void, Error>?
    private var cancellables = Set<AnyCancellable>()
    
    private init() {
        // トランザクションリスナーを開始
        updateListenerTask = listenForTransactions()
        
        // 購入状態を読み込み
        loadPurchasedProducts()
    }
    
    deinit {
        updateListenerTask?.cancel()
    }
    
    // MARK: - Product Loading
    
    /// App Store から商品情報を取得
    func loadProducts() async {
        purchaseState = .loading
        
        do {
            let productIDs = ProductID.allCases.map { $0.rawValue }
            products = try await Product.products(for: Set(productIDs))
            products.sort { $0.price < $1.price }
            purchaseState = .idle
            
            print("[StoreKitService] 商品読み込み完了: \(products.count)件")
        } catch {
            print("[StoreKitService] 商品読み込みエラー: \(error.localizedDescription)")
            purchaseState = .failed(error)
            errorMessage = "商品情報の取得に失敗しました"
        }
    }
    
    // MARK: - Purchase
    
    /// 商品を購入
    func purchase(_ product: Product) async throws -> Bool {
        purchaseState = .loading
        
        do {
            let result = try await product.purchase()
            
            switch result {
            case .success(let verification):
                let transaction = try checkVerified(verification)
                
                // 購入処理
                await updatePurchasedProducts(transaction)
                await transaction.finish()
                
                purchaseState = .purchased
                return true
                
            case .userCancelled:
                purchaseState = .idle
                return false
                
            case .pending:
                purchaseState = .pending
                return false
                
            @unknown default:
                purchaseState = .idle
                return false
            }
        } catch {
            purchaseState = .failed(error)
            errorMessage = "購入処理に失敗しました"
            throw error
        }
    }
    
    /// Product ID で購入
    func purchase(productID: ProductID) async throws -> Bool {
        guard let product = products.first(where: { $0.id == productID.rawValue }) else {
            throw StoreKitError.productNotFound
        }
        return try await purchase(product)
    }
    
    // MARK: - Restore Purchases
    
    /// 購入を復元
    func restorePurchases() async {
        purchaseState = .loading
        
        do {
            try await AppStore.sync()
            
            // 復元後の購入状態を更新
            for await result in Transaction.currentEntitlements {
                if case .verified(let transaction) = result {
                    await updatePurchasedProducts(transaction)
                }
            }
            
            purchaseState = .idle
            print("[StoreKitService] 購入復元完了")
        } catch {
            purchaseState = .failed(error)
            errorMessage = "購入の復元に失敗しました"
            print("[StoreKitService] 購入復元エラー: \(error.localizedDescription)")
        }
    }
    
    // MARK: - Check Purchase Status
    
    /// 商品が購入済みかどうか
    func isPurchased(_ productID: ProductID) -> Bool {
        return purchasedProductIDs.contains(productID.rawValue)
    }
    
    /// キャラクターが購入済みかどうか
    func isCharacterPurchased(_ characterType: CharacterType) -> Bool {
        switch characterType {
        case .mage:
            return isPurchased(.characterMage)
        case .elf:
            return isPurchased(.characterElf)
        default:
            return true // hero, thief は無料
        }
    }
    
    /// 広告が削除されているかどうか
    var isAdRemoved: Bool {
        return isPurchased(.removeAds)
    }
    
    // MARK: - Transaction Listener
    
    private func listenForTransactions() -> Task<Void, Error> {
        return Task.detached {
            for await result in Transaction.updates {
                do {
                    let transaction = try await self.checkVerified(result)
                    await self.updatePurchasedProducts(transaction)
                    await transaction.finish()
                } catch {
                    print("[StoreKitService] トランザクション検証エラー: \(error)")
                }
            }
        }
    }
    
    // MARK: - Verification
    
    private func checkVerified<T>(_ result: VerificationResult<T>) throws -> T {
        switch result {
        case .unverified(_, let error):
            throw error
        case .verified(let safe):
            return safe
        }
    }
    
    // MARK: - Update Purchased Products
    
    private func updatePurchasedProducts(_ transaction: Transaction) async {
        if transaction.revocationDate == nil {
            purchasedProductIDs.insert(transaction.productID)
            savePurchasedProducts()
            
            // 広告削除の場合はAdMobServiceに通知
            if transaction.productID == ProductID.removeAds.rawValue {
                AdMobService.shared.setAdRemoved(true)
            }
            
            print("[StoreKitService] 購入完了: \(transaction.productID)")
        } else {
            purchasedProductIDs.remove(transaction.productID)
            savePurchasedProducts()
            
            print("[StoreKitService] 購入取り消し: \(transaction.productID)")
        }
    }
    
    // MARK: - Persistence
    
    private func loadPurchasedProducts() {
        if let savedIDs = UserDefaults.standard.array(forKey: "purchasedProductIDs") as? [String] {
            purchasedProductIDs = Set(savedIDs)
        }
        
        // 起動時に購入状態を検証
        Task {
            for await result in Transaction.currentEntitlements {
                if case .verified(let transaction) = result {
                    await updatePurchasedProducts(transaction)
                }
            }
        }
    }
    
    private func savePurchasedProducts() {
        UserDefaults.standard.set(Array(purchasedProductIDs), forKey: "purchasedProductIDs")
    }
    
    // MARK: - Helper Methods
    
    /// 商品の価格を取得（表示用）
    func priceString(for productID: ProductID) -> String {
        guard let product = products.first(where: { $0.id == productID.rawValue }) else {
            // フォールバック価格
            switch productID {
            case .characterMage, .characterElf:
                return "¥240"
            case .removeAds:
                return "¥480"
            }
        }
        return product.displayPrice
    }
    
    /// 商品を取得
    func product(for productID: ProductID) -> Product? {
        return products.first { $0.id == productID.rawValue }
    }
}

// MARK: - StoreKit Errors
enum StoreKitError: LocalizedError {
    case productNotFound
    case purchaseFailed
    case verificationFailed
    
    var errorDescription: String? {
        switch self {
        case .productNotFound:
            return "商品が見つかりません"
        case .purchaseFailed:
            return "購入処理に失敗しました"
        case .verificationFailed:
            return "購入の検証に失敗しました"
        }
    }
}

// MARK: - StoreKit Configuration Instructions
/*
 StoreKit課金の設定手順:
 
 1. App Store Connect で設定:
    - アプリを登録
    - App 内課金 > 追加 で以下の商品を作成:
      
      a) 魔法使い (消耗型 or 非消耗型)
         - 製品ID: com.escapenine.character.mage
         - 価格: ¥240
         
      b) エルフ (消耗型 or 非消耗型)
         - 製品ID: com.escapenine.character.elf
         - 価格: ¥240
         
      c) 広告削除 (非消耗型)
         - 製品ID: com.escapenine.removeads
         - 価格: ¥480
 
 2. Xcode で StoreKit Configuration File を作成（ローカルテスト用）:
    - File > New > File > StoreKit Configuration File
    - 上記の商品を追加
    - Scheme > Edit Scheme > Run > Options > StoreKit Configuration で選択
 
 3. Sandbox テスト:
    - App Store Connect > Users and Access > Sandbox でテスターを追加
    - 実機でサインアウトしてSandboxアカウントでテスト
 
 4. プライバシーポリシーと利用規約:
    - App Store Connect でリンクを設定
    - 課金に関する説明を含める
 
 5. 注意事項:
    - StoreKit 2 は iOS 15+ が必要（iOS 14 対応には StoreKit 1 も併用）
    - サーバーサイド検証を推奨（不正防止）
    - 購入状態は Keychain に保存することを推奨（より安全）
*/
