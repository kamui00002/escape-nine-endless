//
//  FirebaseService.swift
//  EscapeNine-endless-
//
//  Firebase Authentication と Cloud Firestore の連携サービス
//

import Foundation
import Combine

// MARK: - Firebase認証状態
enum AuthState {
    case signedOut
    case signedIn(userId: String)
    case error(Error)
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
class FirebaseService: ObservableObject {
    static let shared = FirebaseService()
    
    // MARK: - Published Properties
    @Published var authState: AuthState = .signedOut
    @Published var currentUserId: String?
    @Published var isLoading: Bool = false
    
    // MARK: - Private Properties
    private var cancellables = Set<AnyCancellable>()
    
    private init() {
        // TODO: Firebase初期化
        // Firebase.configure()は AppDelegate または App.swift で実行
    }
    
    // MARK: - Authentication
    
    /// 匿名認証でサインイン
    func signInAnonymously() async throws {
        isLoading = true
        defer { isLoading = false }
        
        // TODO: Firebase実装
        // let result = try await Auth.auth().signInAnonymously()
        // currentUserId = result.user.uid
        // authState = .signedIn(userId: result.user.uid)
        
        // プレースホルダー実装
        let mockUserId = UUID().uuidString
        currentUserId = mockUserId
        authState = .signedIn(userId: mockUserId)
        print("[FirebaseService] 匿名認証成功 (mock): \(mockUserId)")
    }
    
    /// Apple Sign In でサインイン
    func signInWithApple(idToken: String, nonce: String) async throws {
        isLoading = true
        defer { isLoading = false }
        
        // TODO: Firebase実装
        // let credential = OAuthProvider.credential(
        //     withProviderID: "apple.com",
        //     idToken: idToken,
        //     rawNonce: nonce
        // )
        // let result = try await Auth.auth().signIn(with: credential)
        // currentUserId = result.user.uid
        // authState = .signedIn(userId: result.user.uid)
        
        // プレースホルダー実装
        let mockUserId = "apple_\(UUID().uuidString.prefix(8))"
        currentUserId = mockUserId
        authState = .signedIn(userId: mockUserId)
        print("[FirebaseService] Apple Sign In成功 (mock): \(mockUserId)")
    }
    
    /// サインアウト
    func signOut() {
        // TODO: Firebase実装
        // try? Auth.auth().signOut()
        
        currentUserId = nil
        authState = .signedOut
        print("[FirebaseService] サインアウト成功")
    }
    
    /// 認証状態の監視を開始
    func startAuthStateListener() {
        // TODO: Firebase実装
        // Auth.auth().addStateDidChangeListener { [weak self] _, user in
        //     if let user = user {
        //         self?.currentUserId = user.uid
        //         self?.authState = .signedIn(userId: user.uid)
        //     } else {
        //         self?.currentUserId = nil
        //         self?.authState = .signedOut
        //     }
        // }
        
        print("[FirebaseService] 認証状態リスナー開始 (mock)")
    }
    
    // MARK: - Firestore Operations
    
    /// ランキングにスコアを送信
    func submitScore(floor: Int, displayName: String, characterType: String) async throws {
        guard let userId = currentUserId else {
            throw FirebaseError.notAuthenticated
        }
        
        isLoading = true
        defer { isLoading = false }
        
        let entry = FirebaseRankingEntry(
            id: nil,
            userId: userId,
            displayName: displayName,
            floor: floor,
            characterType: characterType,
            timestamp: Date()
        )
        
        // TODO: Firebase実装
        // let db = Firestore.firestore()
        // try await db.collection("rankings").document(userId).setData([
        //     "userId": entry.userId,
        //     "displayName": entry.displayName,
        //     "floor": entry.floor,
        //     "characterType": entry.characterType,
        //     "timestamp": FieldValue.serverTimestamp()
        // ], merge: true)
        
        print("[FirebaseService] スコア送信 (mock): Floor \(floor), User \(userId)")
    }
    
    /// ランキングを取得
    func getRankings(limit: Int = 100) async throws -> [FirebaseRankingEntry] {
        isLoading = true
        defer { isLoading = false }
        
        // TODO: Firebase実装
        // let db = Firestore.firestore()
        // let snapshot = try await db.collection("rankings")
        //     .order(by: "floor", descending: true)
        //     .limit(to: limit)
        //     .getDocuments()
        //
        // return snapshot.documents.compactMap { doc in
        //     try? doc.data(as: FirebaseRankingEntry.self)
        // }
        
        // プレースホルダー実装（モックデータ）
        return generateMockRankings(count: min(limit, 10))
    }
    
    /// ユーザーの最高スコアを取得
    func getUserHighScore() async throws -> Int? {
        guard let userId = currentUserId else {
            return nil
        }
        
        // TODO: Firebase実装
        // let db = Firestore.firestore()
        // let doc = try await db.collection("rankings").document(userId).getDocument()
        // return doc.data()?["floor"] as? Int
        
        print("[FirebaseService] ユーザースコア取得 (mock): \(userId)")
        return nil
    }
    
    // MARK: - Helper Methods
    
    private func generateMockRankings(count: Int) -> [FirebaseRankingEntry] {
        let names = ["勇者アレス", "盗賊シャドウ", "魔導師メルリン", "射手エルフィン", "戦士ガンダルフ"]
        let characters = ["hero", "thief", "mage", "elf"]
        
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

// MARK: - Firebase Configuration Instructions
/*
 Firebase連携の設定手順:
 
 1. Firebase Console (https://console.firebase.google.com) でプロジェクトを作成
 
 2. iOSアプリを追加:
    - Bundle ID: com.yourcompany.escapenine-endless
    - GoogleService-Info.plist をダウンロード
    - プロジェクトに追加 (Copy items if needed をチェック)
 
 3. SPMで依存関係を追加:
    File > Add Package Dependencies... で以下を追加:
    - https://github.com/firebase/firebase-ios-sdk
    - Products: FirebaseAuth, FirebaseFirestore
 
 4. AppDelegate.swift または App.swift で初期化:
    import FirebaseCore
    
    @main
    struct EscapeNineEndlessApp: App {
        init() {
            FirebaseApp.configure()
        }
        ...
    }
 
 5. Apple Sign In の設定 (オプション):
    - Signing & Capabilities で "Sign in with Apple" を追加
    - Firebase Console > Authentication > Sign-in method で Apple を有効化
 
 6. Firestore Rules (推奨):
    rules_version = '2';
    service cloud.firestore {
      match /databases/{database}/documents {
        match /rankings/{userId} {
          allow read: if true;
          allow write: if request.auth != null && request.auth.uid == userId;
        }
      }
    }
*/
