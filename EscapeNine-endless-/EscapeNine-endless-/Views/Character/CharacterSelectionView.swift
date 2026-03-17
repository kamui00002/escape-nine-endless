//
//  CharacterSelectionView.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

struct CharacterSelectionView: View {
    @StateObject private var playerViewModel = PlayerViewModel()
    @StateObject private var purchaseManager = PurchaseManager.shared
    @Environment(\.dismiss) var dismiss

    var body: some View {
        ZStack {
            Color(hex: GameColors.background)
                .ignoresSafeArea()

            VStack(spacing: 0) {
                GameHeader(title: "キャラクター選択")

                GeometryReader { geometry in
                    ScrollView {
                        VStack(spacing: ResponsiveLayout.isIPad() ? 30 : 20) {
                            ForEach(CharacterType.allCases, id: \.self) { characterType in
                                #if DEBUG
                                let isDebugUnlocked = playerViewModel.debugUnlockAllCharacters
                                #else
                                let isDebugUnlocked = false
                                #endif

                                CharacterCardView(
                                    characterType: characterType,
                                    isUnlocked: playerViewModel.unlockedCharacters.contains(characterType) || isDebugUnlocked,
                                    isSelected: playerViewModel.selectedCharacter == characterType,
                                    highestFloor: playerViewModel.highestFloor,
                                    onSelect: {
                                        if playerViewModel.unlockedCharacters.contains(characterType) || isDebugUnlocked {
                                            AudioManager.shared.playSoundEffect(.buttonTap)
                                            playerViewModel.selectCharacter(characterType)
                                        }
                                    },
                                    onPurchase: {
                                        AudioManager.shared.playSoundEffect(.buttonTap)
                                        #if DEBUG
                                        if playerViewModel.debugUnlockAllCharacters {
                                            playerViewModel.unlockCharacter(characterType)
                                            playerViewModel.selectCharacter(characterType)
                                            return
                                        }
                                        #endif
                                        Task {
                                            let success = await purchaseManager.purchaseCharacter(characterType)
                                            if success {
                                                playerViewModel.unlockCharacter(characterType)
                                                playerViewModel.selectCharacter(characterType)
                                            }
                                        }
                                    }
                                )
                            }
                        }
                        .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                        .padding(.top, ResponsiveLayout.isIPad() ? 20 : 16)
                    }
                }
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .navigationBarBackButtonHidden(true)
        .purchaseAlert()
        .purchaseLoadingOverlay()
    }
}

struct CharacterCardView: View {
    let characterType: CharacterType
    let isUnlocked: Bool
    let isSelected: Bool
    let highestFloor: Int
    let onSelect: () -> Void
    let onPurchase: () -> Void

    var skill: Skill {
        Character.getCharacter(for: characterType).skill
    }

    var body: some View {
        GameCard(isHighlighted: isSelected) {
            VStack(spacing: 16) {
                // Header
                HStack {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack {
                            Text(characterType.name)
                                .font(.fantasySubheading())
                                .foregroundColor(Color(hex: GameColors.text))

                            if isSelected {
                                Text("選択中")
                                    .font(.fantasyCaption())
                                    .foregroundColor(Color(hex: GameColors.available))
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 4)
                                    .background(
                                        RoundedRectangle(cornerRadius: 6)
                                            .fill(Color(hex: GameColors.available).opacity(0.2))
                                    )
                            }
                        }

                        if !characterType.isFree {
                            Text("有料キャラクター")
                                .font(.fantasyCaption())
                                .foregroundColor(Color(hex: GameColors.warning))
                        }
                    }

                    Spacer()

                    if !isUnlocked {
                        if let price = characterType.price {
                            Text("¥\(price)")
                                .font(.fantasyNumber())
                                .foregroundColor(Color(hex: GameColors.available))
                        }
                    }
                }

                // Character Image
                characterImageSection

                // Skill Info
                VStack(alignment: .leading, spacing: 8) {
                    Text("スキル: \(skill.name)")
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.textSecondary))

                    Text(skill.description)
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.8))

                    Text("使用回数: \(skill.maxUsage)回")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding()
                .background(
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: GameColors.background).opacity(0.5))
                )

                // Action Button
                actionButton
            }
        }
    }

    // MARK: - Character Image

    private var characterImageSection: some View {
        HStack {
            Spacer()
            ZStack {
                Circle()
                    .fill(
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.main).opacity(0.2),
                                Color(hex: GameColors.accent).opacity(0.1)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(
                        width: ResponsiveLayout.isIPad() ? 160 : 110,
                        height: ResponsiveLayout.isIPad() ? 160 : 110
                    )

                Image(characterType.rawValue)
                    .resizable()
                    .scaledToFit()
                    .frame(
                        width: ResponsiveLayout.isIPad() ? 140 : 95,
                        height: ResponsiveLayout.isIPad() ? 140 : 95
                    )
                    .clipShape(Circle())
                    .opacity(isUnlocked ? 1.0 : 0.4)
                    .shadow(
                        color: isUnlocked ? Color(hex: GameColors.main).opacity(0.3) : Color.clear,
                        radius: 8,
                        x: 0,
                        y: 4
                    )

                if !isUnlocked {
                    ZStack {
                        Circle()
                            .fill(Color.black.opacity(0.6))
                            .frame(
                                width: ResponsiveLayout.isIPad() ? 140 : 95,
                                height: ResponsiveLayout.isIPad() ? 140 : 95
                            )
                        Image(systemName: "lock.fill")
                            .font(.system(size: ResponsiveLayout.isIPad() ? 40 : 30))
                            .foregroundColor(Color(hex: GameColors.warning))
                    }
                }
            }
            Spacer()
        }
        .padding(.vertical, 8)
    }

    // MARK: - Action Button

    @ViewBuilder
    private var actionButton: some View {
        if isUnlocked {
            GameButton(
                title: isSelected ? "選択中" : "選択する",
                style: isSelected ? .primary : .secondary
            ) {
                onSelect()
            }
        } else {
            GameButton(
                title: characterType == .thief ? "10階層クリアで解放" : "購入する",
                style: .danger,
                withSound: true
            ) {
                if characterType == .thief {
                    AudioManager.shared.playSoundEffect(.buttonTap)
                } else {
                    onPurchase()
                }
            }
            .disabled(characterType == .thief)

            if characterType == .thief {
                Text("現在: \(highestFloor)階層 / 必要: 10階層")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    .padding(.top, 4)
            }
        }
    }
}
