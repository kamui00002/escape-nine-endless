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
    // 冒険ファンタジー系カラーパレット
    static let main = "#d4af37"        // ゴールド（冒険の色）
    static let accent = "#8b4513"      // サドルブラウン（革の色）
    static let background = "#0d0d0d"  // 深い黒（ダンジョン）
    static let backgroundSecondary = "#1a1a1a" // 少し明るい黒
    static let text = "#f5deb3"        // ベージュ（羊皮紙の色）
    static let textSecondary = "#d4af37" // ゴールドテキスト
    static let warning = "#dc143c"     // クリムゾン（危険）
    static let success = "#32cd32"     // ライムグリーン（成功）
    static let player = "#4ade80"      // エメラルドグリーン（勇者）
    static let enemy = "#dc143c"       // クリムゾン（敵）
    static let grid = "#2d2d2d"        // グリッドの色
    static let gridBorder = "#8b4513"  // グリッドの枠
    static let available = "#ffd700"   // ゴールド（移動可能）
    static let fog = "#1a1a1a"         // 霧の色
    static let disappeared = "#000000" // 消失マス
}

