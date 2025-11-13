//
//  GameView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct GameView: View {
    @StateObject private var viewModel = GameViewModel()
    @StateObject private var playerViewModel = PlayerViewModel()
    @Environment(\.dismiss) var dismiss
    @State private var showResult = false
    
    var body: some View {
        ZStack {
            // 背景グラデーション
            LinearGradient(
                colors: [
                    Color(hex: GameColors.background),
                    Color(hex: GameColors.backgroundSecondary)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .ignoresSafeArea()
            
            VStack(spacing: 0) {
                // 最小限のヘッダー
                HStack {
                    Button(action: { dismiss() }) {
                        HStack(spacing: 4) {
                            Image(systemName: "chevron.left")
                            Text("戻る")
                        }
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(
                            RoundedRectangle(cornerRadius: 8)
                                .fill(Color(hex: GameColors.backgroundSecondary))
                                .overlay(
                                    RoundedRectangle(cornerRadius: 8)
                                        .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                )
                        )
                    }
                    
                    Spacer()
                    
                    Button(action: {
                        if viewModel.gameStatus == .playing {
                            viewModel.pauseGame()
                        } else if viewModel.gameStatus == .paused {
                            viewModel.resumeGame()
                        }
                    }) {
                        Text(viewModel.gameStatus == .paused ? "再開" : "一時停止")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text))
                            .padding(.horizontal, 12)
                            .padding(.vertical, 6)
                            .background(
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(hex: GameColors.backgroundSecondary))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 8)
                                            .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                    )
                            )
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 10)
                .padding(.bottom, 8)
                
                // メインコンテンツ（中央配置）
                Spacer()
                
                VStack(spacing: 20) {
                    // BPM Info（コンパクトに）
                    BPMInfoView(
                        floor: viewModel.currentFloor,
                        bpm: Floor.calculateBPM(for: viewModel.currentFloor)
                    )
                    
                    // Beat Indicator
                    BeatIndicatorView(currentBeat: viewModel.currentBeat)
                    
                    // Turn Count（小さく）
                    HStack(spacing: 8) {
                        Text("ターン")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        
                        Text("\(viewModel.turnCount) / \(Constants.maxTurns)")
                            .font(.fantasyNumber())
                            .foregroundColor(Color(hex: GameColors.available))
                    }
                    
                    // Grid Board（大きく中央に）
                    GridBoardView(
                        playerPosition: viewModel.playerPosition,
                        enemyPosition: viewModel.enemyPosition,
                        availableMoves: viewModel.getAvailableMoves(),
                        selectedMove: viewModel.pendingPlayerMove,
                        isCellVisible: { position in
                            viewModel.isCellVisible(position)
                        },
                        isCellDisappeared: { position in
                            viewModel.isCellDisappeared(position)
                        },
                        onCellTap: { position in
                            viewModel.selectMove(to: position)
                        },
                        disabled: viewModel.gameStatus != .playing
                    )
                    
                    // 特殊ルール表示
                    if viewModel.specialRule != .none {
                        Text(getSpecialRuleText(viewModel.specialRule))
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.warning))
                            .padding(.horizontal, 16)
                            .padding(.vertical, 8)
                            .background(
                                RoundedRectangle(cornerRadius: 8)
                                    .fill(Color(hex: GameColors.backgroundSecondary))
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 8)
                                            .stroke(Color(hex: GameColors.warning).opacity(0.5), lineWidth: 1)
                                    )
                            )
                    }
                    
                    // 次の移動先が指定されていない場合の警告
                    if viewModel.gameStatus == .playing && viewModel.pendingPlayerMove == nil {
                        HStack(spacing: 6) {
                            Text("⚠️")
                            Text("次の移動先を選択してください")
                        }
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.warning))
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
                        .background(
                            RoundedRectangle(cornerRadius: 8)
                                .fill(Color(hex: GameColors.warning).opacity(0.1))
                        )
                    }
                }
                
                Spacer()
            }
            .padding()
            
            // Floor Clear Overlay
            if viewModel.showFloorClear {
                ZStack {
                    // 背景
                    Color(hex: GameColors.background).opacity(0.95)
                        .ignoresSafeArea()
                    
                    // 装飾的な背景
                    RadialGradient(
                        colors: [
                            Color(hex: GameColors.available).opacity(0.2),
                            Color(hex: GameColors.background).opacity(0.9)
                        ],
                        center: .center,
                        startRadius: 50,
                        endRadius: 200
                    )
                    .ignoresSafeArea()
                    
                    VStack(spacing: 30) {
                        // 装飾的なアイコン
                        ZStack {
                            Circle()
                                .fill(
                                    LinearGradient(
                                        colors: [
                                            Color(hex: GameColors.available).opacity(0.3),
                                            Color(hex: GameColors.main).opacity(0.2)
                                        ],
                                        startPoint: .topLeading,
                                        endPoint: .bottomTrailing
                                    )
                                )
                                .frame(width: 120, height: 120)
                                .blur(radius: 20)
                            
                            Text("⚔️")
                                .font(.system(size: 80))
                        }
                        
                        Text("\(viewModel.currentFloor)階層")
                            .font(.fantasyHeading())
                            .foregroundColor(Color(hex: GameColors.available))
                        
                        Text("クリア！")
                            .font(.fantasySubheading())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                        
                        // 区切り線
                        Rectangle()
                            .fill(
                                LinearGradient(
                                    colors: [
                                        Color(hex: GameColors.available).opacity(0.5),
                                        Color(hex: GameColors.main).opacity(0.3),
                                        Color(hex: GameColors.available).opacity(0.5)
                                    ],
                                    startPoint: .leading,
                                    endPoint: .trailing
                                )
                            )
                            .frame(height: 2)
                            .padding(.horizontal, 60)
                        
                        Text("次: \(viewModel.currentFloor + 1)階層")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                    }
                }
            }
            
            // Paused Overlay
            if viewModel.gameStatus == .paused {
                ZStack {
                    Color(hex: GameColors.background).opacity(0.95)
                        .ignoresSafeArea()
                    
                    VStack(spacing: 30) {
                        Text("⏸")
                            .font(.system(size: 60))
                        
                        Text("一時停止")
                            .font(.fantasyHeading())
                            .foregroundColor(Color(hex: GameColors.text))
                        
                        VStack(spacing: 16) {
                            Button(action: {
                                viewModel.resumeGame()
                            }) {
                                HStack {
                                    Text("▶")
                                    Text("再開")
                                }
                                .font(.fantasyBody())
                                .foregroundColor(.white)
                                .padding()
                                .frame(width: 180)
                                .background(
                                    LinearGradient(
                                        colors: [
                                            Color(hex: GameColors.available),
                                            Color(hex: GameColors.main)
                                        ],
                                        startPoint: .leading,
                                        endPoint: .trailing
                                    )
                                )
                                .cornerRadius(12)
                                .shadow(color: Color(hex: GameColors.available).opacity(0.5), radius: 10)
                            }
                            
                            Button(action: {
                                dismiss()
                            }) {
                                HStack {
                                    Text("🚪")
                                    Text("終了")
                                }
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .padding()
                                .frame(width: 180)
                                .background(
                                    RoundedRectangle(cornerRadius: 12)
                                        .fill(Color(hex: GameColors.backgroundSecondary))
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 12)
                                                .stroke(Color(hex: GameColors.warning).opacity(0.5), lineWidth: 2)
                                        )
                                )
                            }
                        }
                    }
                }
            }
        }
        .onAppear {
            viewModel.startGame(aiLevel: .normal)
        }
        .onChange(of: viewModel.gameStatus) {
            if viewModel.gameStatus == .win || viewModel.gameStatus == .lose {
                playerViewModel.updateHighestFloor(viewModel.currentFloor)
                showResult = true
            }
        }
        .sheet(isPresented: $showResult) {
            ResultView(
                floor: viewModel.currentFloor,
                result: viewModel.gameStatus,
                onPlayAgain: {
                    viewModel.resetGame()
                    viewModel.startGame(aiLevel: .normal)
                    showResult = false
                },
                onHome: {
                    dismiss()
                }
            )
        }
    }
    
    private func getSpecialRuleText(_ rule: SpecialRule) -> String {
        switch rule {
        case .none:
            return ""
        case .fog:
            return "🌫️ 霧の呪い: 視界が制限されています"
        case .disappear:
            return "💀 崩壊の罠: 消失したマスに注意"
        case .fogDisappear:
            return "🌫️💀 霧の呪い + 崩壊の罠"
        }
    }
}

