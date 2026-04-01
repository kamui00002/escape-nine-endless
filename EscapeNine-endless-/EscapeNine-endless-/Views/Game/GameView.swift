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
    @State private var selectedAILevel: AILevel = .easy

    var body: some View {
        ZStack {
            GameBackground()

            GeometryReader { geometry in
                let vSpacing = ResponsiveLayout.verticalSpacing(for: geometry)

                VStack(spacing: 0) {
                    gameHeader

                    // Top info section
                    VStack(spacing: vSpacing) {
                        BPMInfoView(
                            floor: viewModel.currentFloor,
                            bpm: Floor.calculateBPM(for: viewModel.currentFloor)
                        )

                        BeatIndicatorView(turnCountdown: viewModel.turnCountdown, turnCount: viewModel.turnCount, maxTurns: viewModel.maxTurns)

                        comboDisplay

                        turnAndSkillInfo
                    }

                    Spacer(minLength: vSpacing)

                    // Center: grid board（比率ベースで自動フィット）
                    gridBoard
                        .frame(maxHeight: ResponsiveLayout.gridMaxHeight(for: geometry))
                        .frame(maxWidth: ResponsiveLayout.gridMaxWidth(for: geometry))

                    Spacer(minLength: vSpacing)

                    // Bottom: skill button + special rule (fixed at bottom)
                    VStack(spacing: vSpacing) {
                        skillButton(geometry: geometry)

                        specialRuleLabel
                    }
                    .padding(.bottom, vSpacing)
                }
                .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
            }

            if viewModel.showFloorClear {
                floorClearOverlay
            }

            if viewModel.showSkillReset {
                skillResetNotification
            }

            if !isGameStarted {
                startGameOverlay
            }

            if viewModel.gameStatus == .paused {
                pausedOverlay
            }

            if viewModel.isGameStartCountdownActive {
                gameStartCountdownOverlay
            }

            if viewModel.showGameOverOverlay {
                gameOverOverlay
            }

            if viewModel.showBossWarning {
                bossWarningOverlay
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
        .toolbar(.hidden, for: .navigationBar)
        .navigationBarBackButtonHidden(true)
        .sheet(isPresented: $showResult) {
            ResultView(
                floor: viewModel.currentFloor,
                result: viewModel.gameStatus,
                defeatReason: viewModel.defeatReason,
                onPlayAgain: {
                    viewModel.resetGame()
                    isGameStarted = false
                    showResult = false
                },
                onHome: {
                    dismiss()
                }
            )
        }
    }

    // MARK: - Game Header

    private var gameHeader: some View {
        HStack {
            Button(action: {
                AudioManager.shared.playSoundEffect(.buttonTap)
                viewModel.resetGame()
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
        .padding(.top, ResponsiveLayout.isIPad() ? 16 : 10)
        .padding(.bottom, ResponsiveLayout.isIPad() ? 12 : 8)
    }

    // MARK: - Combo Display

    @ViewBuilder
    private var comboDisplay: some View {
        if viewModel.comboCount >= 2 || viewModel.lastTimingGrade != nil {
            HStack(spacing: 6) {
                if let grade = viewModel.lastTimingGrade {
                    Text(grade == .just ? "JUST!" : grade == .good ? "GOOD" : "MISS")
                        .font(.system(size: 12, weight: .black, design: .rounded))
                        .foregroundColor(grade == .just ? Color(hex: GameColors.success) : grade == .good ? Color(hex: GameColors.available) : Color(hex: GameColors.warning))
                        .transition(.scale.combined(with: .opacity))
                }

                if viewModel.comboCount >= 2 {
                    Image(systemName: "flame.fill")
                        .foregroundColor(.orange)
                        .font(.system(size: 12))

                    Text("×\(viewModel.comboCount) コンボ")
                        .font(.system(size: 13, weight: .bold, design: .rounded))
                        .foregroundColor(viewModel.scoreMultiplier > 1.0 ? Color(hex: GameColors.textSecondary) : Color(hex: GameColors.text))

                    if viewModel.scoreMultiplier > 1.0 {
                        Text("×\(String(format: "%.1f", viewModel.scoreMultiplier))")
                            .font(.system(size: 12, weight: .black, design: .rounded))
                            .foregroundColor(Color(hex: GameColors.success))
                            .padding(.horizontal, 5)
                            .padding(.vertical, 2)
                            .background(
                                RoundedRectangle(cornerRadius: 4)
                                    .fill(Color(hex: GameColors.success).opacity(0.2))
                            )
                    }
                }
            }
            .animation(.easeInOut(duration: 0.2), value: viewModel.comboCount)
        }
    }

    // MARK: - Boss Warning Overlay

    private var bossWarningOverlay: some View {
        ZStack {
            Color.red.opacity(0.15)
                .ignoresSafeArea()

            VStack(spacing: 12) {
                Image(systemName: "exclamationmark.shield.fill")
                    .font(.system(size: 60))
                    .foregroundColor(.red)
                    .shadow(color: .red.opacity(0.8), radius: 20)

                Text("ボス出現！")
                    .font(.system(size: 36, weight: .black, design: .rounded))
                    .foregroundColor(.red)
                    .shadow(color: .red.opacity(0.8), radius: 10)

                Text("\(viewModel.currentFloor)階層")
                    .font(.fantasySubheading())
                    .foregroundColor(Color(hex: GameColors.text))
            }
            .transition(.scale.combined(with: .opacity))
        }
        .animation(.spring(response: 0.4, dampingFraction: 0.6), value: viewModel.showBossWarning)
    }

    // MARK: - Turn & Skill Info

    private var turnAndSkillInfo: some View {
        VStack(spacing: 8) {
            HStack(spacing: 8) {
                Text("ターン")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))

                Text("\(viewModel.turnCount) / \(viewModel.maxTurns)")
                    .font(.fantasyNumber())
                    .foregroundColor(Color(hex: GameColors.available))
            }

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
    }

    // MARK: - Grid Board

    private var gridBoard: some View {
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
                if viewModel.currentSkill.type == .bind {
                    viewModel.bindEnemy()
                }
            },
            disabled: viewModel.gameStatus != .playing,
            playerSprite: viewModel.currentCharacter.spriteName,
            enemySprite: Floor.getEnemySprite(for: viewModel.currentFloor)
        )
    }

    // MARK: - Skill Button

    @ViewBuilder
    private func skillButton(geometry: GeometryProxy) -> some View {
        if viewModel.currentSkill.type == .dash || viewModel.currentSkill.type == .invisible || viewModel.currentSkill.type == .shield {
            let buttonWidth = ResponsiveLayout.buttonWidth(for: geometry)
            let isActive = (viewModel.currentSkill.type == .dash && viewModel.isSkillActive)
                        || (viewModel.currentSkill.type == .invisible && viewModel.isInvisible)
                        || (viewModel.currentSkill.type == .shield && viewModel.shieldActive)

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
                .background(skillButtonBackground(isActive: isActive))
                .cornerRadius(12)
                .overlay(
                    RoundedRectangle(cornerRadius: 12)
                        .stroke(isActive ? Color(hex: GameColors.success) : Color.clear, lineWidth: 2)
                )
            }
            .disabled(viewModel.remainingSkillUses <= 0 || viewModel.gameStatus != .playing)
            .frame(height: ResponsiveLayout.isIPad() ? 56 : 48)
        }
    }

    private func skillButtonBackground(isActive: Bool) -> LinearGradient {
        if isActive {
            return LinearGradient(
                colors: [Color(hex: GameColors.success), Color(hex: GameColors.available)],
                startPoint: .leading,
                endPoint: .trailing
            )
        } else if viewModel.remainingSkillUses > 0 {
            return LinearGradient(
                colors: [Color(hex: GameColors.available), Color(hex: GameColors.main)],
                startPoint: .leading,
                endPoint: .trailing
            )
        } else {
            return LinearGradient(
                colors: [Color(hex: GameColors.gridBorder).opacity(0.5), Color(hex: GameColors.main).opacity(0.3)],
                startPoint: .leading,
                endPoint: .trailing
            )
        }
    }

    // MARK: - Special Rule Label

    @ViewBuilder
    private var specialRuleLabel: some View {
        if viewModel.specialRule != .none {
            Text(specialRuleText)
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
            Color.clear
                .frame(height: 0)
        }
    }

    private var specialRuleText: String {
        switch viewModel.specialRule {
        case .none: return ""
        case .fog: return "霧の呪い: 視界が制限されています"
        case .disappear: return "崩壊の罠: 消失したマスに注意"
        case .fogDisappear: return "霧の呪い + 崩壊の罠"
        }
    }

    // MARK: - Floor Clear Overlay

    private var floorClearOverlay: some View {
        ZStack {
            Color(hex: GameColors.background).opacity(0.95)
                .ignoresSafeArea()

            CelebrationEffect()
                .ignoresSafeArea()

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

                GameButton(title: "スタート", style: .primary, maxWidth: 200) {
                    viewModel.nextFloor()
                }
                .glow(color: Color(hex: GameColors.available), radius: 20, intensity: 0.9)
                .pulse(minScale: 1.0, maxScale: 1.05, duration: 1.5)
                .bounceIn(delay: 0.7)
            }
        }
    }

    // MARK: - Skill Reset Notification

    private var skillResetNotification: some View {
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

    // MARK: - Start Game Overlay

    private var startGameOverlay: some View {
        ZStack {
            Color(hex: GameColors.background).opacity(0.95)
                .ignoresSafeArea()

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

                aiLevelSelector

                GameButton(title: "冒険を始める", style: .primary, maxWidth: 220) {
                    isGameStarted = true
                    viewModel.startGame(aiLevel: selectedAILevel)
                }
                .shadow(color: Color(hex: GameColors.available).opacity(0.6), radius: 15)

                GameButton(title: "戻る", style: .secondary, maxWidth: 220) {
                    dismiss()
                }
            }
        }
    }

    // MARK: - AI Level Selector

    private var aiLevelSelector: some View {
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
    }

    // MARK: - Game Start Countdown Overlay

    private var gameStartCountdownOverlay: some View {
        ZStack {
            Color.black.opacity(0.7)
                .ignoresSafeArea()

            if viewModel.gameStartCountdown > 0 {
                Text("\(viewModel.gameStartCountdown)")
                    .font(.system(size: ResponsiveLayout.isIPad() ? 150 : 120, weight: .black, design: .rounded))
                    .foregroundColor(Color(hex: GameColors.available))
                    .shadow(color: Color(hex: GameColors.available).opacity(0.8), radius: 30)
                    .scaleEffect(1.2)
                    .transition(.scale.combined(with: .opacity))
                    .animation(.spring(response: 0.3, dampingFraction: 0.5), value: viewModel.gameStartCountdown)
            } else {
                Text("GO!")
                    .font(.system(size: ResponsiveLayout.isIPad() ? 130 : 100, weight: .black, design: .rounded))
                    .foregroundColor(Color(hex: GameColors.success))
                    .shadow(color: Color(hex: GameColors.success).opacity(0.8), radius: 30)
                    .scaleEffect(1.5)
                    .transition(.scale.combined(with: .opacity))
            }
        }
        .animation(.easeInOut(duration: 0.3), value: viewModel.gameStartCountdown)
    }

    // MARK: - Game Over Overlay

    private var gameOverOverlay: some View {
        ZStack {
            if viewModel.defeatReason == .caughtByEnemy {
                // 赤フラッシュ
                Color(hex: GameColors.warning).opacity(0.4)
                    .ignoresSafeArea()
                    .transition(.opacity)
            } else {
                Color.black.opacity(0.7)
                    .ignoresSafeArea()
                    .transition(.opacity)
            }

            VStack(spacing: 16) {
                if viewModel.defeatReason == .caughtByEnemy {
                    Image(systemName: "exclamationmark.triangle.fill")
                        .font(.system(size: ResponsiveLayout.isIPad() ? 80 : 60))
                        .foregroundColor(Color(hex: GameColors.warning))
                    Text("捕まった！")
                        .font(.fantasyHeading())
                        .foregroundColor(Color(hex: GameColors.warning))
                } else {
                    Image(systemName: "clock.badge.xmark")
                        .font(.system(size: ResponsiveLayout.isIPad() ? 80 : 60))
                        .foregroundColor(Color.orange)
                    Text("時間切れ！")
                        .font(.fantasyHeading())
                        .foregroundColor(Color.orange)
                }
            }
            .transition(.scale.combined(with: .opacity))
        }
        .animation(.easeInOut(duration: 0.3), value: viewModel.showGameOverOverlay)
    }

    // MARK: - Paused Overlay

    private var pausedOverlay: some View {
        ZStack {
            Color(hex: GameColors.background).opacity(0.95)
                .ignoresSafeArea()

            VStack(spacing: 30) {
                Text("一時停止")
                    .font(.fantasyHeading())
                    .foregroundColor(Color(hex: GameColors.text))

                VStack(spacing: 16) {
                    GameButton(title: "再開", style: .primary, maxWidth: 180) {
                        viewModel.resumeGame()
                    }
                    .shadow(color: Color(hex: GameColors.available).opacity(0.5), radius: 10)

                    Button(action: {
                        viewModel.resetGame()
                        dismiss()
                    }) {
                        Text("終了")
                            .font(.fantasyBody())
                            .foregroundColor(Color(hex: GameColors.text))
                            .padding()
                            .frame(maxWidth: 220)
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

// MARK: - Previews

#Preview("iPhone") {
    NavigationStack {
        GameView()
    }
}

#Preview("iPad") {
    NavigationStack {
        GameView()
    }
    .previewDevice("iPad Pro 13-inch (M4)")
}
