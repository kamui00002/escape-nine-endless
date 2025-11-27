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
            // 全体の背景
            Color(hex: GameColors.background)
                .ignoresSafeArea()
            
            VStack(spacing: 0) {
                // Header with background
                VStack(spacing: 0) {
                    LinearGradient(
                        colors: [
                            Color(hex: GameColors.background),
                            Color(hex: GameColors.backgroundSecondary)
                        ],
                        startPoint: .top,
                        endPoint: .bottom
                    )
                    .frame(height: ResponsiveLayout.isIPad() ? 140 : 120)
                    .overlay(
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
                            
                            Text("キャラクター選択")
                                .font(.fantasyHeading())
                                .foregroundColor(Color(hex: GameColors.text))
                            
                            Spacer()
                            
                            Color.clear
                                .frame(width: 80)
                        }
                        .padding()
                    )
                }
                
                // Content area
                GeometryReader { geometry in
                    ScrollView {
                        VStack(spacing: ResponsiveLayout.isIPad() ? 30 : 20) {
                            ForEach(CharacterType.allCases, id: \.self) { characterType in
                                CharacterCardView(
                                    characterType: characterType,
                                    isUnlocked: playerViewModel.unlockedCharacters.contains(characterType) || playerViewModel.debugUnlockAllCharacters,
                                    isSelected: playerViewModel.selectedCharacter == characterType,
                                    highestFloor: playerViewModel.highestFloor,
                                    onSelect: {
                                        if playerViewModel.unlockedCharacters.contains(characterType) || playerViewModel.debugUnlockAllCharacters {
                                            AudioManager.shared.playSoundEffect(.buttonTap)
                                            playerViewModel.selectCharacter(characterType)
                                        }
                                    },
                                    onPurchase: {
                                        AudioManager.shared.playSoundEffect(.buttonTap)
                                        // 管理者用設定が有効な場合は、購入処理をスキップして直接アンロック
                                        if playerViewModel.debugUnlockAllCharacters {
                                            playerViewModel.unlockCharacter(characterType)
                                            playerViewModel.selectCharacter(characterType)
                                        } else {
                                            // StoreKit課金処理
                                            Task {
                                                let success = await purchaseManager.purchaseCharacter(characterType)
                                                if success {
                                                    playerViewModel.unlockCharacter(characterType)
                                                    playerViewModel.selectCharacter(characterType)
                                                }
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
        VStack(spacing: 16) {
            // キャラクター名とステータス
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
            
            // キャラクター画像
            HStack {
                Spacer()
                ZStack {
                    // 背景グラデーション
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
                    
                    // キャラクター画像
                    Image(characterType.rawValue)
                        .resizable()
                        .scaledToFit()
                        .frame(
                            width: ResponsiveLayout.isIPad() ? 140 : 95,
                            height: ResponsiveLayout.isIPad() ? 140 : 95
                        )
                        .clipShape(Circle())  // 円形にクリップ
                        .opacity(isUnlocked ? 1.0 : 0.4)
                        .shadow(
                            color: isUnlocked ? Color(hex: GameColors.main).opacity(0.3) : Color.clear,
                            radius: 8,
                            x: 0,
                            y: 4
                        )
                    
                    // ロックオーバーレイ
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
            
            // スキル情報
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
            
            // ボタン
            if isUnlocked {
                Button(action: onSelect) {
                    Text(isSelected ? "選択中" : "選択する")
                        .font(.fantasyBody())
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding()
                        .background(
                            isSelected ?
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
                            )
                        )
                        .cornerRadius(12)
                }
            } else {
                Button(action: {
                    if characterType == .thief {
                        // 盗賊の場合は何もしない（10階層クリアで自動解放）
                        AudioManager.shared.playSoundEffect(.buttonTap)
                    } else {
                        onPurchase()
                    }
                }) {
                    Text(characterType == .thief ? "10階層クリアで解放" : "購入する")
                        .font(.fantasyBody())
                        .foregroundColor(.white)
                        .frame(maxWidth: .infinity)
                        .padding()
                        .background(
                            LinearGradient(
                                colors: [
                                    Color(hex: GameColors.warning),
                                    Color(hex: GameColors.enemy)
                                ],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                        )
                        .cornerRadius(12)
                }
                .disabled(characterType == .thief) // 盗賊の場合はボタンを無効化
                
                // 盗賊の場合は現在の階層を表示
                if characterType == .thief {
                    Text("現在: \(highestFloor)階層 / 必要: 10階層")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                        .padding(.top, 4)
                }
            }
        }
        .padding()
        .background(
            RoundedRectangle(cornerRadius: 16)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(
                            isSelected ? Color(hex: GameColors.available) : Color(hex: GameColors.gridBorder).opacity(0.5),
                            lineWidth: isSelected ? 3 : 2
                        )
                )
        )
    }
}

