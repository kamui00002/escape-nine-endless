//
//  AchievementPopupView.swift
//  EscapeNine-endless-
//
//  Created by Claude Code on 2025/11/28.
//

import SwiftUI

struct AchievementPopupView: View {
    let achievement: Achievement
    @State private var isShowing = false
    @State private var scale: CGFloat = 0.5
    
    var body: some View {
        VStack(spacing: 12) {
            HStack(spacing: 12) {
                // アイコン
                Image(systemName: achievement.icon)
                    .font(.system(size: 32))
                    .foregroundColor(Color(hex: achievement.color))
                
                // テキスト
                VStack(alignment: .leading, spacing: 4) {
                    Text("実績解除!")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.success))
                    
                    Text(achievement.rawValue)
                        .font(.fantasyBody())
                        .foregroundColor(.white)
                    
                    Text(achievement.description)
                        .font(.caption)
                        .foregroundColor(.white.opacity(0.7))
                }
            }
            .padding()
            .background(
                RoundedRectangle(cornerRadius: 16)
                    .fill(Color(hex: GameColors.backgroundSecondary))
                    .shadow(color: Color(hex: achievement.color).opacity(0.5), radius: 20)
            )
        }
        .scaleEffect(scale)
        .offset(y: isShowing ? 0 : -100)
        .opacity(isShowing ? 1 : 0)
        .onAppear {
            withAnimation(.spring(response: 0.6, dampingFraction: 0.7)) {
                isShowing = true
                scale = 1.0
            }
            
            // 2.5秒後にフェードアウト
            DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
                withAnimation(.easeOut(duration: 0.3)) {
                    isShowing = false
                    scale = 0.8
                }
            }
        }
    }
}

// 実績一覧画面
struct AchievementListView: View {
    @ObservedObject var achievementManager = AchievementManager.shared
    @Environment(\.presentationMode) var presentationMode
    
    var body: some View {
        ZStack {
            Color(hex: GameColors.background)
                .ignoresSafeArea()
            
            VStack(spacing: 20) {
                // ヘッダー
                HStack {
                    Button(action: {
                        AudioManager.shared.playSoundEffect(.buttonTap)
                        presentationMode.wrappedValue.dismiss()
                    }) {
                        Image(systemName: "xmark.circle.fill")
                            .font(.title2)
                            .foregroundColor(.white.opacity(0.7))
                    }
                    
                    Spacer()
                    
                    Text("実績")
                        .font(.fantasyHeading())
                        .foregroundColor(.white)
                    
                    Spacer()
                    
                    // プログレス
                    Text("\(achievementManager.unlockedAchievements.count)/\(Achievement.allCases.count)")
                        .font(.fantasyCaption())
                        .foregroundColor(Color(hex: GameColors.available))
                }
                .padding(.horizontal)
                
                // プログレスバー
                GeometryReader { geometry in
                    ZStack(alignment: .leading) {
                        RoundedRectangle(cornerRadius: 8)
                            .fill(Color.white.opacity(0.2))
                            .frame(height: 8)
                        
                        RoundedRectangle(cornerRadius: 8)
                            .fill(
                                LinearGradient(
                                    gradient: Gradient(colors: [
                                        Color(hex: GameColors.main),
                                        Color(hex: GameColors.accent)
                                    ]),
                                    startPoint: .leading,
                                    endPoint: .trailing
                                )
                            )
                            .frame(width: geometry.size.width * achievementManager.progress, height: 8)
                    }
                }
                .frame(height: 8)
                .padding(.horizontal)
                
                // 実績リスト
                ScrollView {
                    LazyVStack(spacing: 12) {
                        ForEach(Achievement.allCases, id: \.self) { achievement in
                            AchievementRow(
                                achievement: achievement,
                                isUnlocked: achievementManager.unlockedAchievements.contains(achievement)
                            )
                        }
                    }
                    .padding(.horizontal)
                }
            }
            .padding(.top)
        }
    }
}

struct AchievementRow: View {
    let achievement: Achievement
    let isUnlocked: Bool
    
    var body: some View {
        HStack(spacing: 16) {
            // アイコン
            ZStack {
                Circle()
                    .fill(isUnlocked ? Color(hex: achievement.color).opacity(0.2) : Color.white.opacity(0.1))
                    .frame(width: 50, height: 50)
                
                Image(systemName: achievement.icon)
                    .font(.title2)
                    .foregroundColor(isUnlocked ? Color(hex: achievement.color) : .white.opacity(0.3))
            }
            
            // テキスト
            VStack(alignment: .leading, spacing: 4) {
                Text(achievement.rawValue)
                    .font(.fantasyBody())
                    .foregroundColor(isUnlocked ? .white : .white.opacity(0.5))
                
                Text(achievement.description)
                    .font(.caption)
                    .foregroundColor(.white.opacity(0.6))
            }
            
            Spacer()
            
            // ロック/アンロックアイコン
            if isUnlocked {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundColor(Color(hex: GameColors.success))
            } else {
                Image(systemName: "lock.fill")
                    .foregroundColor(.white.opacity(0.3))
            }
        }
        .padding()
        .background(
            RoundedRectangle(cornerRadius: 12)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .opacity(isUnlocked ? 1.0 : 0.5)
        )
    }
}
