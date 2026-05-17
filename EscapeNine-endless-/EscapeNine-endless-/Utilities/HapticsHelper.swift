//
//  HapticsHelper.swift
//  EscapeNine-endless-
//
//  Sprint 3 v1.1: 触覚フィードバックの統一ヘルパー (Sprint 3 #12)。
//  SettingsView の Toggle (hapticsEnabled) と連動し、無効時は no-op になる。
//  既存の UIImpactFeedbackGenerator 直呼びは全て本ヘルパー経由に置換済。
//

import UIKit

enum HapticsHelper {
    /// UserDefaults キー (SettingsView の `@AppStorage` と共有)。
    /// review-full P3 反映 (2026-05-17): SettingsView 側は
    /// `@AppStorage(HapticsHelper.storageKey)` で本定数を参照し、
    /// "hapticsEnabled" 文字列が 2 箇所にハードコードされないようにする。
    static let storageKey = "hapticsEnabled"

    /// 触覚 ON/OFF の現在値。未設定時は ON (true) を返して既存挙動と一致させる。
    static var isEnabled: Bool {
        // `bool(forKey:)` は未設定時 false を返してしまうため、`object(forKey:)` で
        // 明示的に存在判定して未設定なら true (デフォルト ON) を返す。
        if let raw = UserDefaults.standard.object(forKey: storageKey) as? Bool {
            return raw
        }
        return true
    }

    /// 通常の振動フィードバック。Toggle OFF または Reduce Motion ON 時は何もしない。
    static func impact(_ style: UIImpactFeedbackGenerator.FeedbackStyle) {
        guard isEnabled else { return }
        guard !UIAccessibility.isReduceMotionEnabled else { return }
        let generator = UIImpactFeedbackGenerator(style: style)
        generator.impactOccurred()
    }
}
