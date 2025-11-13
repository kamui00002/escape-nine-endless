//
//  GridBoardView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct GridBoardView: View {
    let playerPosition: Int
    let enemyPosition: Int
    let availableMoves: [Int]
    let selectedMove: Int?
    let isCellVisible: (Int) -> Bool
    let isCellDisappeared: (Int) -> Bool
    let onCellTap: (Int) -> Void
    let disabled: Bool
    
    var body: some View {
        VStack(spacing: 8) {
            // レイアウトの安定化のため、固定サイズを確保
            HStack(spacing: 8) {
                GridCellView(position: 1, isPlayer: playerPosition == 1, isEnemy: enemyPosition == 1, isAvailable: availableMoves.contains(1), isSelected: selectedMove == 1, isVisible: isCellVisible(1), isDisappeared: isCellDisappeared(1), onTap: { onCellTap(1) }, disabled: disabled)
                GridCellView(position: 2, isPlayer: playerPosition == 2, isEnemy: enemyPosition == 2, isAvailable: availableMoves.contains(2), isSelected: selectedMove == 2, isVisible: isCellVisible(2), isDisappeared: isCellDisappeared(2), onTap: { onCellTap(2) }, disabled: disabled)
                GridCellView(position: 3, isPlayer: playerPosition == 3, isEnemy: enemyPosition == 3, isAvailable: availableMoves.contains(3), isSelected: selectedMove == 3, isVisible: isCellVisible(3), isDisappeared: isCellDisappeared(3), onTap: { onCellTap(3) }, disabled: disabled)
            }
            HStack(spacing: 8) {
                GridCellView(position: 4, isPlayer: playerPosition == 4, isEnemy: enemyPosition == 4, isAvailable: availableMoves.contains(4), isSelected: selectedMove == 4, isVisible: isCellVisible(4), isDisappeared: isCellDisappeared(4), onTap: { onCellTap(4) }, disabled: disabled)
                GridCellView(position: 5, isPlayer: playerPosition == 5, isEnemy: enemyPosition == 5, isAvailable: availableMoves.contains(5), isSelected: selectedMove == 5, isVisible: isCellVisible(5), isDisappeared: isCellDisappeared(5), onTap: { onCellTap(5) }, disabled: disabled)
                GridCellView(position: 6, isPlayer: playerPosition == 6, isEnemy: enemyPosition == 6, isAvailable: availableMoves.contains(6), isSelected: selectedMove == 6, isVisible: isCellVisible(6), isDisappeared: isCellDisappeared(6), onTap: { onCellTap(6) }, disabled: disabled)
            }
            HStack(spacing: 8) {
                GridCellView(position: 7, isPlayer: playerPosition == 7, isEnemy: enemyPosition == 7, isAvailable: availableMoves.contains(7), isSelected: selectedMove == 7, isVisible: isCellVisible(7), isDisappeared: isCellDisappeared(7), onTap: { onCellTap(7) }, disabled: disabled)
                GridCellView(position: 8, isPlayer: playerPosition == 8, isEnemy: enemyPosition == 8, isAvailable: availableMoves.contains(8), isSelected: selectedMove == 8, isVisible: isCellVisible(8), isDisappeared: isCellDisappeared(8), onTap: { onCellTap(8) }, disabled: disabled)
                GridCellView(position: 9, isPlayer: playerPosition == 9, isEnemy: enemyPosition == 9, isAvailable: availableMoves.contains(9), isSelected: selectedMove == 9, isVisible: isCellVisible(9), isDisappeared: isCellDisappeared(9), onTap: { onCellTap(9) }, disabled: disabled)
            }
        }
    }
}

