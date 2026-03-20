//
//  ShopView.swift
//  EscapeNine-endless-
//
//  In-app purchase shop for characters and ad removal
//

import SwiftUI

struct ShopView: View {
    @StateObject private var purchaseManager = PurchaseManager.shared
    @StateObject private var playerViewModel = PlayerViewModel()
    @Environment(\.dismiss) var dismiss

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                // Background
                Color(hex: GameColors.background)
                    .ignoresSafeArea()

                VStack(spacing: 0) {
                    // Header
                    VStack(spacing: 0) {
                        LinearGradient(
                            colors: [
                                Color(hex: GameColors.background),
                                Color(hex: GameColors.backgroundSecondary)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                        .frame(height: ResponsiveLayout.isIPad() ? 100 : 80)
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

                                Text("ショップ")
                                    .font(.fantasyHeading())
                                    .foregroundColor(Color(hex: GameColors.text))

                                Spacer()

                                Color.clear.frame(width: 80)
                            }
                            .padding()
                        )
                    }

                    ScrollView {
                        VStack(spacing: ResponsiveLayout.isIPad() ? 24 : 16) {
                            // Character Section
                            VStack(alignment: .leading, spacing: 12) {
                                Text("キャラクター")
                                    .font(.fantasySubheading())
                                    .foregroundColor(Color(hex: GameColors.textSecondary))

                                // Wizard
                                ShopItemCard(
                                    icon: "🔮",
                                    title: "魔法使い",
                                    description: "透明化スキル: 鬼に当たっても無敵（7回）",
                                    price: purchaseManager.characterPrice(.wizard),
                                    isPurchased: purchaseManager.isCharacterPurchased(.wizard),
                                    action: {
                                        Task {
                                            let success = await purchaseManager.purchaseCharacter(.wizard)
                                            if success {
                                                playerViewModel.unlockCharacter(.wizard)
                                            }
                                        }
                                    }
                                )

                                // Elf
                                ShopItemCard(
                                    icon: "🏹",
                                    title: "エルフ",
                                    description: "拘束スキル: 鬼を2ターン停止（4回）",
                                    price: purchaseManager.characterPrice(.elf),
                                    isPurchased: purchaseManager.isCharacterPurchased(.elf),
                                    action: {
                                        Task {
                                            let success = await purchaseManager.purchaseCharacter(.elf)
                                            if success {
                                                playerViewModel.unlockCharacter(.elf)
                                            }
                                        }
                                    }
                                )

                                // Knight
                                ShopItemCard(
                                    icon: "🛡️",
                                    title: "ナイト",
                                    description: "盾ガードスキル: 次の衝突を1回無効化する（2回）",
                                    price: purchaseManager.characterPrice(.knight),
                                    isPurchased: purchaseManager.isCharacterPurchased(.knight),
                                    action: {
                                        Task {
                                            let success = await purchaseManager.purchaseCharacter(.knight)
                                            if success {
                                                playerViewModel.unlockCharacter(.knight)
                                            }
                                        }
                                    }
                                )
                            }
                            .padding(.horizontal, ResponsiveLayout.padding(for: geometry))

                            // Ad Removal Section
                            VStack(alignment: .leading, spacing: 12) {
                                Text("その他")
                                    .font(.fantasySubheading())
                                    .foregroundColor(Color(hex: GameColors.textSecondary))

                                ShopItemCard(
                                    icon: "🚫",
                                    title: "広告削除",
                                    description: "すべての広告を非表示にします",
                                    price: purchaseManager.adRemovalPrice,
                                    isPurchased: purchaseManager.isAdRemoved,
                                    action: {
                                        Task {
                                            _ = await purchaseManager.purchaseAdRemoval()
                                        }
                                    }
                                )
                            }
                            .padding(.horizontal, ResponsiveLayout.padding(for: geometry))

                            // Restore Purchases
                            Button(action: {
                                AudioManager.shared.playSoundEffect(.buttonTap)
                                Task {
                                    await purchaseManager.restorePurchases()
                                }
                            }) {
                                HStack {
                                    Image(systemName: "arrow.clockwise")
                                    Text("購入を復元")
                                }
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                                .frame(maxWidth: .infinity)
                                .padding()
                                .background(
                                    RoundedRectangle(cornerRadius: 12)
                                        .fill(Color(hex: GameColors.backgroundSecondary))
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 12)
                                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.3), lineWidth: 1)
                                        )
                                )
                            }
                            .padding(.horizontal, ResponsiveLayout.padding(for: geometry))
                            .padding(.top, 8)
                        }
                        .padding(.top, 16)
                        .padding(.bottom, 30)
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

// MARK: - Shop Item Card
struct ShopItemCard: View {
    let icon: String
    let title: String
    let description: String
    let price: String
    let isPurchased: Bool
    let action: () -> Void

    var body: some View {
        HStack(spacing: 16) {
            // Icon
            Text(icon)
                .font(.system(size: 36))
                .frame(width: 56, height: 56)
                .background(
                    RoundedRectangle(cornerRadius: 12)
                        .fill(Color(hex: GameColors.background).opacity(0.5))
                )

            // Info
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text))

                Text(description)
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    .lineLimit(2)
            }

            Spacer()

            // Purchase Button
            if isPurchased {
                Text("購入済")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.success))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 6)
                    .background(
                        RoundedRectangle(cornerRadius: 8)
                            .fill(Color(hex: GameColors.success).opacity(0.15))
                    )
            } else {
                Button(action: action) {
                    Text(price)
                        .font(.fantasyNumber())
                        .foregroundColor(.white)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 8)
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
                        .cornerRadius(8)
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
                            isPurchased ?
                            Color(hex: GameColors.success).opacity(0.3) :
                            Color(hex: GameColors.gridBorder).opacity(0.3),
                            lineWidth: 1
                        )
                )
        )
    }
}
