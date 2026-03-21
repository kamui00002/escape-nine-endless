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
    static let baseTurns = 5
    static let turnsPerFloorStep = 10 // 10階ごとにターン数+1

    /// 階層に応じた最大ターン数を計算（Floor 1-10: 5, 11-20: 6, ..., 91-100: 14）
    static func getMaxTurns(for floor: Int) -> Int {
        baseTurns + (floor - 1) / turnsPerFloorStep
    }
    static let maxSkillUsage = 5  // レガシー（キャラ別定数を優先）

    // Timing（BPM連動で動的計算）
    static let timingToleranceLow = 0.6    // BPM < 100: ±60%
    static let timingToleranceMid = 0.45   // BPM 100-150: ±45%
    static let timingToleranceHigh = 0.35  // BPM > 150: ±35%
    static let beatCheckInterval = 0.01 // 10msごとにチェック
    static let invisibilityDuration = 0.1 // 透明化スキルの持続時間（秒）

    // BPM曲線（べき乗曲線: BPM = start + (end - start) * (floor/99)^exponent）
    static let bpmCurveStart = 70.0      // Floor 1
    static let bpmCurveEnd = 200.0       // Floor 100
    static let bpmCurveExponent = 1.4    // 序盤緩やか→終盤急加速

    // Skill Settings
    static let skillResetInterval = 10  // 10階層ごとにスキルリセット

    // Skill Usage per Character
    static let heroSkillMaxUsage = 3
    static let thiefSkillMaxUsage = 5
    static let wizardSkillMaxUsage = 7
    static let elfSkillMaxUsage = 4
    static let knightSkillMaxUsage = 2
    static let bindDurationTurns = 2

    // Combo System
    static let comboMultiplierThreshold1 = 3   // combo >= 3: ×1.5
    static let comboMultiplierThreshold2 = 5   // combo >= 5: ×2.0
    static let comboJustTimingRatio = 0.20     // ±20% = ジャスト判定
    static let comboGoodTimingRatio = 0.35     // ±35% = グッド判定

    // Boss Floor
    static let bossFloorInterval = 10          // 10の倍数階がボス
    static let bossAIChaseChance = 0.95        // ボスAI追跡確率

    // Turn Countdown Settings（1ターンあたりのカウントダウン）
    static let turnCountdownBeats = 3      // 1ターンあたりのカウントダウンビート数
    static let gameStartCountdownBeats = 3 // ゲーム開始カウントダウン

    // Countdown Audio
    static let countdownLowFrequency = 440.0   // カウントダウン低音周波数
    static let countdownHighFrequency = 880.0  // カウントダウン高音周波数

    // Floor Ranges for Special Rules（要件定義書準拠）
    static let fogStartFloor = 21        // 霧マップ: 階層21-40
    static let disappearStartFloor = 41  // マス消失: 階層41-60
    static let combinedRulesStartFloor = 61  // 霧+消失: 階層61-100

    // AI Difficulty Ranges（自然難易度の閾値）
    static let aiNaturalNormalFloor = 16
    static let aiNaturalHardFloor = 36

    // Easy AI行動確率
    static let easyAIChaseChance = 0.15
    static let easyAIFleeChance = 0.20

    // 消失マス段階（階層→消失マス数）
    static let disappearCellStages: [(floor: Int, count: Int)] = [
        (86, 4), (71, 3), (56, 2), (41, 1)
    ]

    // Character Unlock
    static let thiefUnlockFloor = 10

    // Pricing
    static let premiumCharacterPrice = 240

    // Audio
    static let defaultVolume = 0.7

    // BPM連動タイミング許容値
    static func timingTolerance(for bpm: Double) -> Double {
        if bpm < 100 {
            return timingToleranceLow
        } else if bpm <= 150 {
            return timingToleranceMid
        } else {
            return timingToleranceHigh
        }
    }

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

