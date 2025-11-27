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
    @State private var isGameStarted = false
    @State private var selectedAILevel: AILevel = .easy // プレイヤーが選択したAI難易度（初心者向けにEasyをデフォルト）
    
    init() {
        // ViewModelにPlayerViewModelを設定
    }
    
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
            
            GeometryReader { geometry in
                VStack(spacing: 0) {
                    // 最小限のヘッダー
                    HStack {
                        Button(action: { 
                            AudioManager.shared.playSoundEffect(.buttonTap)
                            dismiss() 
                        }) {
                            HStack(spacing: 4) {
                                Image(systemName: "chevron.left")
                                Text("戻る")
                            }
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text))
                            .padding(.horizontal, ResponsiveLayout.isIPad() ? 16 : 12)
                            .padding(.vertical, ResponsiveLayout.isIPad() ? 10 : 6)
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
                                .padding(.horizontal, ResponsiveLayout.isIPad() ? 16 : 12)
                                .padding(.vertical, ResponsiveLayout.isIPad() ? 10 : 6)
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
                    .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                    .padding(.top, ResponsiveLayout.isIPad() ? 16 : 10)
                    .padding(.bottom, ResponsiveLayout.isIPad() ? 12 : 8)
                    
                    // メインコンテンツ（中央配置）
                    Spacer()
                    
                    VStack(spacing: ResponsiveLayout.isIPad() ? 30 : 20) {
                    // BPM Info（コンパクトに）
                    BPMInfoView(
                        floor: viewModel.currentFloor,
                        bpm: Floor.calculateBPM(for: viewModel.currentFloor)
                    )
                    
                    // Beat Indicator
                    BeatIndicatorView(currentBeat: viewModel.currentBeat)
                    
                    // Turn Count & Skill Info
                    VStack(spacing: 8) {
                        HStack(spacing: 8) {
                            Text("ターン")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                            
                            Text("\(viewModel.turnCount) / \(Constants.maxTurns)")
                                .font(.fantasyNumber())
                                .foregroundColor(Color(hex: GameColors.available))
                        }
                        
                        // スキル残回数表示
                        if viewModel.currentSkill.type != .diagonal {
                            HStack(spacing: 8) {
                                Text("\(viewModel.currentSkill.name)")
                                    .font(.fantasyCaption())
                                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                
                                Text("\(viewModel.remainingSkillUses) / \(viewModel.currentSkill.maxUsage)")
                                    .font(.fantasyNumber())
                                    .foregroundColor(viewModel.remainingSkillUses > 0 ? Color(hex: GameColors.available) : Color(hex: GameColors.warning))
                            }
                        }
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
                        onEnemyTap: {
                            // エルフの拘束スキル: 敵をタップで発動
                            if viewModel.currentSkill.type == .bind {
                                viewModel.bindEnemy()
                            }
                        },
                        disabled: viewModel.gameStatus != .playing,
                        playerSprite: viewModel.currentCharacter.spriteName,
                        enemySprite: Floor.getEnemySprite(for: viewModel.currentFloor)
                    )
                    
                    // スキルボタン（ダッシュ、透明化の場合）
                    if viewModel.currentSkill.type == .dash || viewModel.currentSkill.type == .invisible {
                        GeometryReader { geometry in
                            let buttonWidth = ResponsiveLayout.buttonWidth(for: geometry)
                            let isActive = (viewModel.currentSkill.type == .dash && viewModel.isSkillActive) || (viewModel.currentSkill.type == .invisible && viewModel.isInvisible)
                            
                            Button(action: {
                                viewModel.activateSkill()
                            }) {
                                HStack(spacing: 8) {
                                    Text(viewModel.currentSkill.name)
                                        .font(.fantasyBody())
                                    Text("(残り\(viewModel.remainingSkillUses)回)")
                                        .font(.fantasyCaption())
                                    if isActive {
                                        Text("ON")
                                            .font(.fantasyCaption())
                                            .foregroundColor(.white)
                                            .padding(.horizontal, 6)
                                            .padding(.vertical, 2)
                                            .background(Color(hex: GameColors.success))
                                            .cornerRadius(4)
                                    }
                                }
                                .foregroundColor(.white)
                                .padding()
                                .frame(maxWidth: buttonWidth)
                                .background(
                                    isActive ?
                                    LinearGradient(
                                        colors: [
                                            Color(hex: GameColors.success),
                                            Color(hex: GameColors.available)
                                        ],
                                        startPoint: .leading,
                                        endPoint: .trailing
                                    ) :
                                    (viewModel.remainingSkillUses > 0 ?
                                     LinearGradient(
                                         colors: [
                                             Color(hex: GameColors.available),
                                             Color(hex: GameColors.main)
                                         ],
                                         startPoint: .leading,
                                         endPoint: .trailing
                                     ) :
                                     LinearGradient(
                                         colors: [
                                             Color(hex: GameColors.gridBorder).opacity(0.5),
                                             Color(hex: GameColors.main).opacity(0.3)
                                         ],
                                         startPoint: .leading,
                                         endPoint: .trailing
                                     ))
                                )
                                .cornerRadius(12)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 12)
                                        .stroke(isActive ? Color(hex: GameColors.success) : Color.clear, lineWidth: 2)
                                )
                            }
                            .disabled(viewModel.remainingSkillUses <= 0 || viewModel.gameStatus != .playing)
                            .frame(maxWidth: .infinity)
                        }
                        .frame(height: ResponsiveLayout.isIPad() ? 60 : 50)
                    }
                    
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
                    } else {
                        // レイアウトの安定化のため、空のスペースを確保
                        Color.clear
                            .frame(height: 0)
                    }
                    }
                    
                    Spacer()
                }
                .padding(ResponsiveLayout.padding(for: geometry))
            }
            
            // Floor Clear Overlay
            if viewModel.showFloorClear {
                ZStack {
                    // 背景
                    Color(hex: GameColors.background).opacity(0.95)
                        .ignoresSafeArea()

                    // 紙吹雪エフェクト
                    CelebrationEffect()
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
                        AnimatedNumber(value: viewModel.currentFloor, duration: 0.5)
                            .font(.fantasyHeading())
                            .foregroundColor(Color(hex: GameColors.available))
                            .overlay(
                                Text("\(viewModel.currentFloor)階層")
                                    .font(.fantasyHeading())
                                    .foregroundColor(.clear)
                            )
                            .glow(color: Color(hex: GameColors.available), radius: 25, intensity: 1.0)
                            .bounceIn(delay: 0.1)

                        Text("クリア！")
                            .font(.fantasySubheading())
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                            .glow(color: Color(hex: GameColors.textSecondary), radius: 15, intensity: 0.7)
                            .pulse(minScale: 1.0, maxScale: 1.1, duration: 0.8)
                            .bounceIn(delay: 0.3)
                        
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
                            .slideIn(from: .bottom, delay: 0.5)

                        // スタートボタン
                        Button(action: {
                            viewModel.nextFloor()
                        }) {
                            Text("スタート")
                                .font(.fantasyBody())
                                .foregroundColor(.white)
                                .padding()
                                .frame(width: 200)
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
                                .cornerRadius(16)
                                .glow(color: Color(hex: GameColors.available), radius: 20, intensity: 0.9)
                        }
                        .pressableButton(scale: 0.96, shadowRadius: 12)
                        .pulse(minScale: 1.0, maxScale: 1.05, duration: 1.5)
                        .bounceIn(delay: 0.7)
                    }
                }
            }
            
            // Skill Reset Notification
            if viewModel.showSkillReset {
                VStack {
                    Spacer().frame(height: 100)
                    
                    HStack(spacing: 8) {
                        Image(systemName: "sparkles")
                            .foregroundColor(Color(hex: GameColors.success))
                        Text("スキル回復！")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.text))
                        Image(systemName: "sparkles")
                            .foregroundColor(Color(hex: GameColors.success))
                    }
                    .padding(.horizontal, 24)
                    .padding(.vertical, 12)
                    .background(
                        RoundedRectangle(cornerRadius: 12)
                            .fill(Color(hex: GameColors.backgroundSecondary))
                            .overlay(
                                RoundedRectangle(cornerRadius: 12)
                                    .stroke(Color(hex: GameColors.success), lineWidth: 2)
                            )
                    )
                    .glow(color: Color(hex: GameColors.success), radius: 15, intensity: 0.8)
                    .bounceIn(delay: 0)
                    .transition(.scale.combined(with: .opacity))
                    
                    Spacer()
                }
            }
            
            // Start Game Overlay
            if !isGameStarted {
                ZStack {
                    Color(hex: GameColors.background).opacity(0.95)
                        .ignoresSafeArea()
                    
                    // 装飾的な背景
                    RadialGradient(
                        colors: [
                            Color(hex: GameColors.available).opacity(0.1),
                            Color(hex: GameColors.background).opacity(0.9)
                        ],
                        center: .center,
                        startRadius: 50,
                        endRadius: 200
                    )
                    .ignoresSafeArea()
                    
                    VStack(spacing: 40) {
                        VStack(spacing: 16) {
                            Text("準備はできましたか？")
                                .font(.fantasySubheading())
                                .foregroundColor(Color(hex: GameColors.text))

                            Text("\(viewModel.currentFloor)階層")
                                .font(.fantasyHeading())
                                .foregroundColor(Color(hex: GameColors.available))
                        }

                        // AI難易度選択
                        VStack(spacing: 12) {
                            Text("鬼の強さを選ぶ")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.text))

                            HStack(spacing: 12) {
                                ForEach(AILevel.allCases, id: \.self) { level in
                                    Button(action: {
                                        selectedAILevel = level
                                    }) {
                                        Text(level.rawValue)
                                            .font(.fantasyCaption())
                                            .foregroundColor(selectedAILevel == level ? .white : Color(hex: GameColors.text))
                                            .padding(.horizontal, 16)
                                            .padding(.vertical, 8)
                                            .background(
                                                RoundedRectangle(cornerRadius: 8)
                                                    .fill(selectedAILevel == level ? Color(hex: GameColors.available) : Color(hex: GameColors.backgroundSecondary))
                                            )
                                            .overlay(
                                                RoundedRectangle(cornerRadius: 8)
                                                    .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                            )
                                    }
                                }
                            }
                        }

                        Button(action: {
                            AudioManager.shared.playSoundEffect(.buttonTap)
                            isGameStarted = true
                            viewModel.startGame(aiLevel: selectedAILevel)
                        }) {
                            Text("冒険を始める")
                                .font(.fantasyBody())
                                .foregroundColor(.white)
                                .padding()
                                .frame(width: 220)
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
                                .cornerRadius(16)
                                .shadow(color: Color(hex: GameColors.available).opacity(0.6), radius: 15)
                        }
                        
                        Button(action: {
                            dismiss()
                        }) {
                            Text("戻る")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .padding()
                                .frame(width: 220)
                                .background(
                                    RoundedRectangle(cornerRadius: 16)
                                        .fill(Color(hex: GameColors.backgroundSecondary))
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 16)
                                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 2)
                                        )
                                )
                        }
                    }
                }
            }
            
            // Paused Overlay
            if viewModel.gameStatus == .paused {
                ZStack {
                    Color(hex: GameColors.background).opacity(0.95)
                        .ignoresSafeArea()
                    
                    VStack(spacing: 30) {
                        Text("一時停止")
                            .font(.fantasyHeading())
                            .foregroundColor(Color(hex: GameColors.text))
                        
                        VStack(spacing: 16) {
                            Button(action: {
                                viewModel.resumeGame()
                            }) {
                                Text("再開")
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
                                Text("終了")
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
            viewModel.setPlayerViewModel(playerViewModel)
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
                    // ゲームを完全にリセット
                    viewModel.resetGame()
                    // 状態を同期的にリセットしてUIの不整合を防ぐ
                    isGameStarted = false
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
            return "霧の呪い: 視界が制限されています"
        case .disappear:
            return "崩壊の罠: 消失したマスに注意"
        case .fogDisappear:
            return "霧の呪い + 崩壊の罠"
        }
    }
}

