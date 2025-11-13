//
//  Constants.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

enum Constants {
    static let gridSize = 9
    static let maxFloors = 100
    static let maxTurns = 10
    static let maxSkillUsage = 5
    static let timingTolerance = 0.15 // ±15%の誤差許容
    static let beatCheckInterval = 0.01 // 10msごとにチェック
}

enum GameColors {
    // 明るい冒険ファンタジー系カラーパレット
    static let main = "#f4a460"        // サンディブラウン（明るい冒険の色）
    static let accent = "#daa520"      // ゴールデンロッド（明るいゴールド）
    static let background = "#2c1810"  // 明るい茶色のダンジョン
    static let backgroundSecondary = "#3d2817" // 少し明るい茶色
    static let text = "#f5deb3"        // ベージュ（羊皮紙の色）
    static let textSecondary = "#ffd700" // 明るいゴールドテキスト
    static let warning = "#ff6347"     // トマトレッド（危険）
    static let success = "#90ee90"     // ライトグリーン（成功）
    static let player = "#98fb98"      // ペールグリーン（勇者）
    static let enemy = "#ff6347"       // トマトレッド（敵）
    static let grid = "#4a3728"        // 明るいグリッドの色
    static let gridBorder = "#daa520"  // ゴールドの枠
    static let available = "#ffd700"   // ゴールド（移動可能）
    static let fog = "#3d2817"         // 霧の色（少し明るく）
    static let disappeared = "#1a1a1a" // 消失マス（少し明るく）
}

