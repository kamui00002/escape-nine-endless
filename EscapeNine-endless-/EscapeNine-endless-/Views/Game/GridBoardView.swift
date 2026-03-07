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
    let onEnemyTap: () -> Void
    let disabled: Bool
    let playerSprite: String?
    let enemySprite: String?
    
    var body: some View {
        GeometryReader { geometry in
            let cellSize = ResponsiveLayout.gridCellSize(for: geometry)
            let characterSize = ResponsiveLayout.characterSize(for: geometry)
            let spacing = ResponsiveLayout.spacing(for: geometry)
            
            VStack(spacing: spacing) {
                ForEach(0..<3, id: \.self) { row in
                    HStack(spacing: spacing) {
                        ForEach(0..<3, id: \.self) { col in
                            let position = row * 3 + col + 1
                            GridCellView(
                                position: position,
                                isPlayer: playerPosition == position,
                                isEnemy: enemyPosition == position,
                                isAvailable: availableMoves.contains(position),
                                isSelected: selectedMove == position,
                                isVisible: isCellVisible(position),
                                isDisappeared: isCellDisappeared(position),
                                onTap: {
                                    if enemyPosition == position {
                                        onEnemyTap()
                                    } else {
                                        onCellTap(position)
                                    }
                                },
                                disabled: disabled,
                                cellSize: cellSize,
                                characterSize: characterSize,
                                playerSprite: playerSprite,
                                enemySprite: enemySprite
                            )
                        }
                    }
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .aspectRatio(1.0, contentMode: .fit)
    }
}

