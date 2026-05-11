//
//  ShareTextBuilderTests.swift
//  EscapeNine-endless-Tests
//
//  Sprint 1 (2026-05-09): ShareTextBuilder の Wordle 風シェアテキスト生成を検証。
//  対象は ShareSheet.swift 内の `enum ShareTextBuilder`。
//
//  検証観点:
//  - 階層数・秒数・URL・絵文字グリッドが含まれる
//  - dailyChallengeId が指定された時のみ "#NNN" 形式が含まれる
//  - 勝敗ラベル ("X階クリア" / "X階で敗北") が反映される
//  - 3x3 グリッドが 3 行・各行 3 セルで生成される
//

import XCTest
@testable import EscapeNine_endless_

final class ShareTextBuilderTests: XCTestCase {

    // MARK: - 階層数

    func testBuildContainsFloor() {
        let text = ShareTextBuilder.build(
            floor: 9,
            elapsedSeconds: 38,
            isVictory: true,
            playerPosition: 5,
            enemyPosition: 6
        )
        XCTAssertTrue(text.contains("9階"), "expected '9階' in: \(text)")
    }

    func testBuildContainsLoseFloorLabel() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
        )
        XCTAssertTrue(text.contains("5階で敗北"), "expected '5階で敗北' in: \(text)")
    }

    func testBuildContainsWinFloorLabel() {
        let text = ShareTextBuilder.build(
            floor: 7,
            elapsedSeconds: 30,
            isVictory: true,
            playerPosition: 5,
            enemyPosition: 6
        )
        XCTAssertTrue(text.contains("7階クリア"), "expected '7階クリア' in: \(text)")
    }

    // MARK: - URL

    func testBuildContainsURL() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
        )
        XCTAssertTrue(
            text.contains("escape9.app") || text.contains("https"),
            "expected escape9.app / https URL in: \(text)"
        )
    }

    func testBuildContainsCanonicalShareURL() {
        let text = ShareTextBuilder.build(
            floor: 1,
            elapsedSeconds: 5,
            isVictory: false,
            playerPosition: 2,
            enemyPosition: 3
        )
        XCTAssertTrue(text.contains(ShareTextBuilder.shareURL))
    }

    // MARK: - Wordle 風絵文字

    func testBuildContainsWordleSymbols() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
        )
        // Wordle 風 emoji (🟩 / 🟧 / ⬛) のいずれかを含む
        XCTAssertTrue(
            text.contains("🟩") || text.contains("🟧") || text.contains("⬛"),
            "expected at least one Wordle-style emoji in: \(text)"
        )
    }

    /// プレイヤーマス・敵マス・空マスの 3 種類の絵文字がすべて含まれる
    /// (player と enemy が別マスに居る限り)。
    func testBuildContainsAllThreeEmojiKinds() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
        )
        XCTAssertTrue(text.contains("🟩"), "player emoji missing in: \(text)")
        XCTAssertTrue(text.contains("🟧"), "enemy emoji missing in: \(text)")
        XCTAssertTrue(text.contains("⬛"), "empty emoji missing in: \(text)")
    }

    /// 9 マス分の絵文字 (= 3 行 x 3 列) が生成される。
    /// 内訳: player 1 マス + enemy 1 マス + empty 7 マス = 合計 9 セル。
    func testBuildGridHasNineCells() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
        )
        let playerCount = text.components(separatedBy: "🟩").count - 1
        let enemyCount = text.components(separatedBy: "🟧").count - 1
        let emptyCount = text.components(separatedBy: "⬛").count - 1
        XCTAssertEqual(playerCount, 1, "expected exactly 1 player emoji, got \(playerCount)")
        XCTAssertEqual(enemyCount, 1, "expected exactly 1 enemy emoji, got \(enemyCount)")
        XCTAssertEqual(emptyCount, 7, "expected exactly 7 empty emojis, got \(emptyCount)")
        XCTAssertEqual(playerCount + enemyCount + emptyCount, 9)
    }

    // MARK: - dailyChallengeId

    func testBuildWithDailyChallengeId() {
        let text = ShareTextBuilder.build(
            floor: 9,
            elapsedSeconds: 38,
            isVictory: true,
            playerPosition: 5,
            enemyPosition: 6,
            dailyChallengeId: 138
        )
        XCTAssertTrue(text.contains("#138"), "expected '#138' in: \(text)")
    }

    /// dailyChallengeId が nil の時は "#数字" 形式が含まれない (header は "Escape9 → ...")。
    func testBuildWithoutDailyChallengeIdHasNoHashId() {
        let text = ShareTextBuilder.build(
            floor: 5,
            elapsedSeconds: 20,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 9
            // dailyChallengeId は省略 (nil)
        )
        // "Escape9 #" のパターンが現れないことを確認
        XCTAssertFalse(
            text.contains("Escape9 #"),
            "expected no 'Escape9 #' pattern when dailyChallengeId is nil, got: \(text)"
        )
    }

    // MARK: - 秒数 (rounded)

    /// elapsedSeconds は Int に丸められて表示される (例: 37.6 → "38秒")。
    func testElapsedSecondsRoundedToInt() {
        let text = ShareTextBuilder.build(
            floor: 9,
            elapsedSeconds: 37.6,
            isVictory: true,
            playerPosition: 5,
            enemyPosition: 6
        )
        XCTAssertTrue(text.contains("38秒"), "expected '38秒' (rounded) in: \(text)")
    }

    func testElapsedSecondsZero() {
        let text = ShareTextBuilder.build(
            floor: 1,
            elapsedSeconds: 0,
            isVictory: false,
            playerPosition: 1,
            enemyPosition: 2
        )
        XCTAssertTrue(text.contains("0秒"), "expected '0秒' in: \(text)")
    }
}
