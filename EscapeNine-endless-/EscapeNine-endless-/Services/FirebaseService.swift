//
//  FirebaseService.swift
//  EscapeNine-endless-
//
//  Firebase Authentication と Cloud Firestore の連携サービス
//  Firebase SDK未導入時はモック実装で動作する
//

import Foundation
import Combine
import os

private let logger = Logger(subsystem: Bundle.main.bundleIdentifier ?? "com.escapenine.app", category: "FirebaseService")
#if canImport(FirebaseAuth)
import FirebaseAuth
import FirebaseFirestore
#endif

// MARK: - Firebase認証状態
enum AuthState: Equatable {
    case signedOut
    case signedIn(userId: String)

    static func == (lhs: AuthState, rhs: AuthState) -> Bool {
        switch (lhs, rhs) {
        case (.signedOut, .signedOut): return true
        case (.signedIn(let a), .signedIn(let b)): return a == b
        default: return false
        }
    }
}

// MARK: - ランキングエントリ（Firebase用）
struct FirebaseRankingEntry: Codable, Identifiable {
    var id: String?
    let userId: String
    let displayName: String
    let floor: Int
    let characterType: String
    let timestamp: Date
}

// MARK: - FirebaseService
@MainActor
class FirebaseService: ObservableObject {
    static let shared = FirebaseService()

    // MARK: - Published Properties
    @Published var authState: AuthState = .signedOut
    @Published var currentUserId: String?
    @Published var isLoading: Bool = false

    // MARK: - Private Properties
    #if canImport(FirebaseAuth)
    private var authStateHandle: AuthStateDidChangeListenerHandle?
    #endif

    private init() {
        #if canImport(FirebaseAuth)
        setupAuthStateListener()
        #endif
    }

    #if canImport(FirebaseAuth)
    deinit {
        if let handle = authStateHandle {
            Auth.auth().removeStateDidChangeListener(handle)
        }
    }
    #endif

    // MARK: - Authentication

    #if canImport(FirebaseAuth)

    // ---- Real Firebase Implementation ----

    private func setupAuthStateListener() {
        authStateHandle = Auth.auth().addStateDidChangeListener { [weak self] _, user in
            Task { @MainActor in
                self?.currentUserId = user?.uid
                self?.authState = user != nil ? .signedIn(userId: user!.uid) : .signedOut
            }
        }
    }

    func signInAnonymously() async throws {
        isLoading = true
        defer { isLoading = false }

        let result = try await Auth.auth().signInAnonymously()
        currentUserId = result.user.uid
        authState = .signedIn(userId: result.user.uid)
    }

    func signInWithApple(idToken: String, nonce: String) async throws {
        isLoading = true
        defer { isLoading = false }

        let credential = OAuthProvider.credential(
            providerID: AuthProviderID.apple,
            idToken: idToken,
            rawNonce: nonce
        )
        let result = try await Auth.auth().signIn(with: credential)
        currentUserId = result.user.uid
        authState = .signedIn(userId: result.user.uid)
    }

    func signOut() {
        try? Auth.auth().signOut()
        currentUserId = nil
        authState = .signedOut
    }

    // MARK: - Firestore Operations

    func submitScore(floor: Int, displayName: String, characterType: String) async throws {
        guard let userId = currentUserId else {
            throw FirebaseError.notAuthenticated
        }

        isLoading = true
        defer { isLoading = false }

        let db = Firestore.firestore()
        try await db.collection("rankings").document(userId).setData([
            "userId": userId,
            "displayName": displayName,
            "floor": floor,
            "characterType": characterType,
            "timestamp": FieldValue.serverTimestamp()
        ], merge: true)
    }

    func getRankings(limit: Int = 100) async throws -> [FirebaseRankingEntry] {
        isLoading = true
        defer { isLoading = false }

        let db = Firestore.firestore()
        let snapshot = try await db.collection("rankings")
            .order(by: "floor", descending: true)
            .limit(to: limit)
            .getDocuments()

        return snapshot.documents.compactMap { doc in
            try? doc.data(as: FirebaseRankingEntry.self)
        }
    }

    func getUserHighScore() async throws -> Int? {
        guard let userId = currentUserId else { return nil }

        let db = Firestore.firestore()
        let doc = try await db.collection("rankings").document(userId).getDocument()
        return doc.data()?["floor"] as? Int
    }

    #else

    // ---- Mock Implementation (Firebase SDK未導入時) ----

    /// モック用ユーザーIDのUserDefaultsキー（再起動後も同一IDを維持）
    private static let mockUserIdKey = "firebase_mock_user_id"

    func signInAnonymously() async throws {
        isLoading = true
        defer { isLoading = false }

        // 再起動後も同一のモックIDを使用する
        let mockUserId: String
        if let stored = UserDefaults.standard.string(forKey: Self.mockUserIdKey) {
            mockUserId = stored
        } else {
            mockUserId = UUID().uuidString
            UserDefaults.standard.set(mockUserId, forKey: Self.mockUserIdKey)
        }
        currentUserId = mockUserId
        authState = .signedIn(userId: mockUserId)
        logger.info("[FirebaseService] 匿名認証成功 (mock): \(mockUserId.prefix(4))***")
    }

    func signInWithApple(idToken: String, nonce: String) async throws {
        isLoading = true
        defer { isLoading = false }

        let mockUserId = "apple_\(UUID().uuidString.prefix(8))"
        UserDefaults.standard.set(mockUserId, forKey: Self.mockUserIdKey)
        currentUserId = mockUserId
        authState = .signedIn(userId: mockUserId)
        logger.info("[FirebaseService] Apple Sign In成功 (mock): \(mockUserId.prefix(8))***")
    }

    func signOut() {
        currentUserId = nil
        authState = .signedOut
        logger.info("[FirebaseService] サインアウト成功")
    }

    func submitScore(floor: Int, displayName: String, characterType: String) async throws {
        guard let userId = currentUserId else {
            throw FirebaseError.notAuthenticated
        }

        isLoading = true
        defer { isLoading = false }

        logger.info("[FirebaseService] スコア送信 (mock): Floor \(floor), User \(userId.prefix(4))***")
    }

    func getRankings(limit: Int = 100) async throws -> [FirebaseRankingEntry] {
        isLoading = true
        defer { isLoading = false }

        return generateMockRankings(count: min(limit, 10))
    }

    func getUserHighScore() async throws -> Int? {
        guard let userId = currentUserId else { return nil }
        logger.info("[FirebaseService] ユーザースコア取得 (mock): \(userId.prefix(4))***")
        return nil
    }

    private func generateMockRankings(count: Int) -> [FirebaseRankingEntry] {
        let names = ["勇者アレス", "盗賊シャドウ", "魔導師メルリン", "射手エルフィン", "戦士ガンダルフ"]
        let characters = ["hero", "thief", "wizard", "elf"]

        return (0..<count).map { index in
            FirebaseRankingEntry(
                id: "mock_\(index)",
                userId: "user_\(index)",
                displayName: names[index % names.count],
                floor: 100 - index * 5,
                characterType: characters[index % characters.count],
                timestamp: Date().addingTimeInterval(TimeInterval(-index * 3600))
            )
        }
    }

    #endif
}

// MARK: - Firebase Errors
enum FirebaseError: LocalizedError {
    case notAuthenticated
    case networkError
    case invalidData
    case unknown

    var errorDescription: String? {
        switch self {
        case .notAuthenticated:
            return "認証されていません"
        case .networkError:
            return "ネットワークエラーが発生しました"
        case .invalidData:
            return "データが無効です"
        case .unknown:
            return "不明なエラーが発生しました"
        }
    }
}

// MARK: - Firebase Setup Instructions
/*
 Firebase連携の設定手順:

 1. Firebase Console (https://console.firebase.google.com) でプロジェクトを作成

 2. iOSアプリを追加:
    - Bundle ID: com.yoshidometoru.EscapeNine-endless-
    - GoogleService-Info.plist をダウンロード
    - プロジェクトルート (EscapeNine-endless-/EscapeNine-endless-/) に配置
    - Xcode で「Add Files to EscapeNine-endless-」から追加 (Copy items if needed をチェック)

 3. SPMパッケージ依存関係（設定済み）:
    - firebase-ios-sdk >= 12.10.0 (FirebaseAuth, FirebaseFirestore, FirebaseCore)
    - swift-package-manager-google-mobile-ads >= 12.0.0
    ※ Xcodeで一度ビルドすると自動的にパッケージが解決される

 4. App.swift の初期化（設定済み）:
    - #if canImport(FirebaseCore) ガード付きで FirebaseApp.configure() 実装済み
    - GoogleService-Info.plist を配置するだけで自動的に有効化される

 5. Firebase Console で必要なサービスを有効化:
    - Authentication > Sign-in method > 匿名認証 を有効化
    - Firestore Database を作成 (本番モード or テストモードで開始)

 6. Firestore Security Rules をデプロイ:
    プロジェクトルートの firestore.rules を使用
    firebase deploy --only firestore:rules

 7. Apple Sign In の設定 (オプション):
    - Signing & Capabilities で "Sign in with Apple" を追加
    - Firebase Console > Authentication > Sign-in method で Apple を有効化

 8. 動作確認:
    - GoogleService-Info.plist 配置後にビルドすると #if canImport(FirebaseAuth) が有効になり
      モック実装から本番実装に自動切替される
*/
