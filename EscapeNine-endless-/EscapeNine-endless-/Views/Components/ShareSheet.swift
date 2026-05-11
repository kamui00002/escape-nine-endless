//
//  ShareSheet.swift
//  EscapeNine-endless-
//
//  Sprint 1 (Game Over 刷新): Wordle 風シェア機能の素地
//  - UIActivityViewController を SwiftUI で使うための wrapper
//  - Wordle 風結果記号 (🟩🟧⬛) + 階層数 + escape9.app テキスト生成
//

import SwiftUI
import UIKit

/// UIActivityViewController を SwiftUI で利用するための wrapper。
/// `.sheet(isPresented:)` 内で `ShareSheet(activityItems: [text])` のように使う。
struct ShareSheet: UIViewControllerRepresentable {
    let activityItems: [Any]
    var applicationActivities: [UIActivity]? = nil

    func makeUIViewController(context: Context) -> UIActivityViewController {
        UIActivityViewController(
            activityItems: activityItems,
            applicationActivities: applicationActivities
        )
    }

    func updateUIViewController(_ uiViewController: UIActivityViewController, context: Context) {
        // no-op: activityItems は init 時に確定
    }
}

// MARK: - ShareTextBuilder

/// Wordle 風シェア用テキストの組み立て。
/// Sprint 1 では「素地」のみ実装し、Sprint 2 で画像生成 / 9 マスの状態反映を拡張する想定。
enum ShareTextBuilder {
    /// シェア URL (App Store / LP) — 仮値。本番環境向けは Sprint 2 で差し替え予定。
    static let shareURL = "https://escape9.app"

    /// 9 マス絵文字記号
    /// - 🟩 = 安全マス (プレイヤーが踏んだ最終位置)
    /// - 🟧 = 敵接近マス (敵の最終位置)
    /// - ⬛ = その他のマス
    private enum CellEmoji {
        static let player = "🟩"
        static let enemy = "🟧"
        static let empty = "⬛"
    }

    /// 結果テキストを組み立てる。
    /// - Parameters:
    ///   - floor: 到達階層
    ///   - elapsedSeconds: 挑戦時間 (秒)
    ///   - isVictory: 勝利か敗北か
    ///   - playerPosition: 1-9 のプレイヤー最終位置 (敗北時は死亡マス)
    ///   - enemyPosition: 1-9 の敵最終位置
    ///   - dailyChallengeId: 任意。Daily Challenge の通し番号 (Sprint 2 で活用)
    /// - Returns: 例:
    ///     ```
    ///     Escape9 #138 → 9階クリア (38秒)
    ///     ⬛🟩⬛
    ///     ⬛⬛⬛
    ///     ⬛⬛🟧
    ///     escape9.app
    ///     ```
    static func build(
        floor: Int,
        elapsedSeconds: Double,
        isVictory: Bool,
        playerPosition: Int,
        enemyPosition: Int,
        dailyChallengeId: Int? = nil
    ) -> String {
        let header = buildHeader(
            floor: floor,
            elapsedSeconds: elapsedSeconds,
            isVictory: isVictory,
            dailyChallengeId: dailyChallengeId
        )
        let grid = buildGrid(playerPosition: playerPosition, enemyPosition: enemyPosition)
        return "\(header)\n\(grid)\n\(shareURL)"
    }

    // MARK: - Internal helpers

    private static func buildHeader(
        floor: Int,
        elapsedSeconds: Double,
        isVictory: Bool,
        dailyChallengeId: Int?
    ) -> String {
        let prefix: String
        if let id = dailyChallengeId {
            prefix = "Escape9 #\(id)"
        } else {
            prefix = "Escape9"
        }
        let outcome = isVictory ? "\(floor)階クリア" : "\(floor)階で敗北"
        let seconds = Int(elapsedSeconds.rounded())
        return "\(prefix) → \(outcome) (\(seconds)秒)"
    }

    /// 1-9 の position を 3x3 絵文字グリッドに変換。
    /// position レイアウトは既存仕様 (Constants.rowFromPosition / columnFromPosition) に整合させる。
    private static func buildGrid(playerPosition: Int, enemyPosition: Int) -> String {
        var rows: [String] = []
        for row in 0..<3 {
            var line = ""
            for col in 0..<3 {
                let position = row * 3 + col + 1 // 1-9
                if position == playerPosition {
                    line += CellEmoji.player
                } else if position == enemyPosition {
                    line += CellEmoji.enemy
                } else {
                    line += CellEmoji.empty
                }
            }
            rows.append(line)
        }
        return rows.joined(separator: "\n")
    }
}
