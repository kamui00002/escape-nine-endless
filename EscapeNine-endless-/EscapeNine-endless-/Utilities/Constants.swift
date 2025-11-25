//
//  Constants.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import Foundation

enum Constants {
    // Grid
    static let gridSize = 9
    static let gridColumns = 3
    static let gridRows = 3

    // Game Progress
    static let maxFloors = 100
    static let maxTurns = 10
    static let maxSkillUsage = 5

    // Timing
    static let timingTolerance = 0.15 // ±15%の誤差許容
    static let beatCheckInterval = 0.01 // 10msごとにチェック
    static let invisibilityDuration = 0.1 // 透明化スキルの持続時間（秒）

    // BPM Settings（要件定義書準拠: 60→240、10階層ごとに20BPM上昇）
    static let minBPM = 60.0
    static let maxBPM = 240.0
    static let bpmIncrement = 20.0

    // Skill Settings
    static let skillResetInterval = 10  // 10階層ごとにスキルリセット

    // Wait Settings
    static let maxConsecutiveWaits = 2  // 連続待機の最大回数

    // Floor Ranges for Special Rules（要件定義書準拠）
    static let fogStartFloor = 21        // 霧マップ: 階層21-40
    static let disappearStartFloor = 41  // マス消失: 階層41-60
    static let combinedRulesStartFloor = 61  // 霧+消失: 階層61-100

    // AI Difficulty Ranges
    static let normalDifficultyStartFloor = 21
    static let hardDifficultyStartFloor = 51

    // Character Unlock
    static let thiefUnlockFloor = 10

    // Pricing
    static let premiumCharacterPrice = 240

    // Audio
    static let defaultVolume = 0.7

    // Grid Position Helpers
    static func rowFromPosition(_ position: Int) -> Int {
        return (position - 1) / gridColumns
    }

    static func columnFromPosition(_ position: Int) -> Int {
        return (position - 1) % gridColumns
    }

    static func positionFromRowColumn(row: Int, column: Int) -> Int {
        return row * gridColumns + column + 1
    }
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

