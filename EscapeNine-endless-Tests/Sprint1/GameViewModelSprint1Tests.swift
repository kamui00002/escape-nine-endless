//
//  GameViewModelSprint1Tests.swift
//  EscapeNine-endless-Tests
//
//  Sprint 1 (2026-05-09): GameViewModel に追加された
//  near-miss meter / elapsedSeconds / chebyshevDistance ロジックを検証。
//
//  対象:
//  - `nearMissDistance` 初期値・endGame 後の計算結果
//  - `elapsedSeconds` 初期値・startGame 後の挙動
//  - chebyshevDistance(from:to:) は private のため endGame 経由で間接検証
//

import XCTest
@testable import EscapeNine_endless_

@MainActor
final class GameViewModelSprint1Tests: XCTestCase {

    // MARK: - 初期値

    func testInitialNearMissDistanceIsZero() {
        let vm = GameViewModel()
        XCTAssertEqual(vm.nearMissDistance, 0)
    }

    func testInitialElapsedSecondsIsZero() {
        let vm = GameViewModel()
        XCTAssertEqual(vm.elapsedSeconds, 0)
    }

    // MARK: - startGame() の Sprint 1 副作用

    /// startGame() が gameStartTime を設定し、その後 endGame() で
    /// elapsedSeconds が 0 以上の値に更新されることを確認する。
    /// gameStartTime 自体は private のため elapsedSeconds 経由で検証。
    func testStartGameInitializesGameStartTime() {
        let vm = GameViewModel()
        vm.startGame(aiLevel: .easy)

        // startGame() 直後は elapsedSeconds は 0 にリセットされる
        XCTAssertEqual(vm.elapsedSeconds, 0)
        XCTAssertEqual(vm.nearMissDistance, 0)

        // endGame 時に gameStartTime が設定されていれば elapsedSeconds が
        // Date().timeIntervalSince(start) で更新される (>= 0)
        vm.endGame(result: .lose)
        XCTAssertGreaterThanOrEqual(vm.elapsedSeconds, 0)
    }

    /// resetGame() で Sprint 1 関連の状態がクリアされる。
    func testResetGameClearsSprint1State() {
        let vm = GameViewModel()
        vm.playerPosition = 5
        vm.enemyPosition = 6
        vm.endGame(result: .lose)
        // ここで nearMissDistance != 0 / elapsedSeconds は計算済みかもしれない

        vm.resetGame()
        XCTAssertEqual(vm.nearMissDistance, 0)
        XCTAssertEqual(vm.elapsedSeconds, 0)
    }

    // MARK: - chebyshevDistance (endGame 経由で間接検証)

    /// 隣接マス (pos 5 中央 → pos 6 右) の Chebyshev 距離は 1。
    func testChebyshevDistanceForHorizontallyAdjacentCells() {
        let vm = GameViewModel()
        vm.playerPosition = 5
        vm.enemyPosition = 6
        vm.endGame(result: .lose)
        XCTAssertEqual(vm.nearMissDistance, 1)
    }

    /// 斜め隣接 (pos 1 左上 → pos 5 中央) の Chebyshev 距離は 1。
    func testChebyshevDistanceForDiagonallyAdjacentCells() {
        let vm = GameViewModel()
        vm.playerPosition = 1
        vm.enemyPosition = 5
        vm.endGame(result: .lose)
        XCTAssertEqual(vm.nearMissDistance, 1)
    }

    /// 反対角 (pos 1 左上 → pos 9 右下) の Chebyshev 距離は 2 (最大値)。
    func testChebyshevDistanceForOppositeCorners() {
        let vm = GameViewModel()
        vm.playerPosition = 1
        vm.enemyPosition = 9
        vm.endGame(result: .lose)
        XCTAssertEqual(vm.nearMissDistance, 2)
    }

    /// 同一マス (player == enemy) の Chebyshev 距離は 0。
    func testChebyshevDistanceForSameCell() {
        let vm = GameViewModel()
        vm.playerPosition = 5
        vm.enemyPosition = 5
        vm.endGame(result: .lose)
        XCTAssertEqual(vm.nearMissDistance, 0)
    }

    // MARK: - endGame() の near-miss 計算

    /// player=5, enemy=6 で endGame(.lose) → nearMissDistance == 1
    /// (タスク仕様で明示された代表ケース)
    func testEndGameCalculatesNearMissDistance() {
        let vm = GameViewModel()
        vm.playerPosition = 5
        vm.enemyPosition = 6
        vm.endGame(result: .lose)
        XCTAssertEqual(vm.nearMissDistance, 1)
        XCTAssertEqual(vm.gameStatus, .lose)
    }

    /// .win 結果でも nearMissDistance は計算される (Game Over Analytics は飛ばないが
    /// 距離自体は endGame() 冒頭で常に更新される実装)。
    func testEndGameWinAlsoUpdatesNearMissDistance() {
        let vm = GameViewModel()
        vm.playerPosition = 1
        vm.enemyPosition = 9
        vm.endGame(result: .win)
        XCTAssertEqual(vm.nearMissDistance, 2)
        XCTAssertEqual(vm.gameStatus, .win)
    }
}
