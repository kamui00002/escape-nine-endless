//
//  TutorialOverlayView.swift
//  EscapeNine-endless-
//
//  Tutorial overlay for first-time players (6 pages with illustrations)
//

import SwiftUI

struct TutorialOverlayView: View {
    @Binding var isShowing: Bool
    @State private var currentPage = 0

    private let totalPages = 6

    var body: some View {
        ZStack {
            Color.black.opacity(0.95)
                .ignoresSafeArea()

            VStack(spacing: 24) {
                Spacer()

                pageContent

                Spacer()

                // Page indicator
                HStack(spacing: 8) {
                    ForEach(0..<totalPages, id: \.self) { index in
                        Circle()
                            .fill(index == currentPage ? Color(hex: GameColors.available) : Color(hex: GameColors.text).opacity(0.3))
                            .frame(width: 8, height: 8)
                    }
                }

                // Navigation buttons
                HStack(spacing: 20) {
                    if currentPage > 0 {
                        Button(action: {
                            withAnimation {
                                currentPage -= 1
                            }
                        }) {
                            Text("前へ")
                                .font(.fantasyBody())
                                .foregroundColor(Color(hex: GameColors.text))
                                .padding(.horizontal, 30)
                                .padding(.vertical, 14)
                                .background(
                                    RoundedRectangle(cornerRadius: 12)
                                        .fill(Color(hex: GameColors.backgroundSecondary))
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 12)
                                                .stroke(Color(hex: GameColors.gridBorder).opacity(0.5), lineWidth: 1)
                                        )
                                )
                        }
                    }

                    Button(action: {
                        if currentPage < totalPages - 1 {
                            withAnimation {
                                currentPage += 1
                            }
                        } else {
                            UserDefaults.standard.set(true, forKey: "tutorialCompleted")
                            withAnimation {
                                isShowing = false
                            }
                        }
                    }) {
                        Text(currentPage < totalPages - 1 ? "次へ" : "始める！")
                            .font(.fantasyBody())
                            .foregroundColor(.white)
                            .padding(.horizontal, 30)
                            .padding(.vertical, 14)
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
                    }
                }

                // Skip button
                if currentPage < totalPages - 1 {
                    Button(action: {
                        UserDefaults.standard.set(true, forKey: "tutorialCompleted")
                        withAnimation {
                            isShowing = false
                        }
                    }) {
                        Text("スキップ")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    }
                }

                Spacer().frame(height: 40)
            }
        }
        .transition(.opacity)
    }

    // MARK: - Page Content

    @ViewBuilder
    private var pageContent: some View {
        switch currentPage {
        case 0: welcomePage
        case 1: movementPage
        case 2: beatTimingPage
        case 3: skillPage
        case 4: specialRulesPage
        case 5: startPage
        default: EmptyView()
        }
    }

    // MARK: - Page 1: Welcome

    private var welcomePage: some View {
        VStack(spacing: 20) {
            Image(systemName: "music.note")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.available))

            Text("ようこそ！")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            Text("3x3のグリッドでビートに合わせて\n鬼から逃げるリズムゲームです。\n10ターン逃げ切れば階層クリア！")
                .font(.fantasyBody())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                .multilineTextAlignment(.center)
                .lineSpacing(8)
                .padding(.horizontal, 40)

            // Mini grid illustration
            miniGridIllustration
        }
    }

    // MARK: - Page 2: Movement

    private var movementPage: some View {
        VStack(spacing: 20) {
            Image(systemName: "arrow.up.arrow.down")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.available))

            Text("移動方法")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            Text("光っているマスをタップして移動先を選択。\n毎ターン必ず移動が必要です！\nその場に留まることはできません。")
                .font(.fantasyBody())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                .multilineTextAlignment(.center)
                .lineSpacing(8)
                .padding(.horizontal, 40)

            // Movement illustration
            movementIllustration
        }
    }

    // MARK: - Page 3: Beat Timing (NEW)

    private var beatTimingPage: some View {
        VStack(spacing: 20) {
            Image(systemName: "metronome.fill")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.available))

            Text("ビートタイミング")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            VStack(spacing: 12) {
                HStack(spacing: 8) {
                    Image(systemName: "3.circle.fill")
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                    Image(systemName: "arrow.right")
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    Image(systemName: "2.circle.fill")
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                    Image(systemName: "arrow.right")
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    Image(systemName: "1.circle.fill")
                        .foregroundColor(Color(hex: GameColors.textSecondary))
                    Image(systemName: "arrow.right")
                        .foregroundColor(Color(hex: GameColors.text).opacity(0.5))
                    Text("GO!")
                        .font(.fantasyBody())
                        .foregroundColor(Color(hex: GameColors.success))
                }
                .font(.system(size: 24))

                Text("各ターンは3ビートのカウントダウン。\n3→2→1の間に移動先を選んでください。\nカウントが0になると移動が実行されます。")
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.8))
                    .multilineTextAlignment(.center)
                    .lineSpacing(8)
                    .padding(.horizontal, 40)

                VStack(spacing: 6) {
                    HStack(spacing: 4) {
                        Image(systemName: "clock.fill")
                            .foregroundColor(Color(hex: GameColors.warning))
                        Text("移動しないと時間切れでゲームオーバー！")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.warning))
                    }

                    HStack(spacing: 4) {
                        Image(systemName: "speedometer")
                            .foregroundColor(Color(hex: GameColors.textSecondary))
                        Text("階層が上がるとカウントが速くなる")
                            .font(.fantasyCaption())
                            .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    }
                }
                .padding(.horizontal, 40)
            }
        }
    }

    // MARK: - Page 4: Skills

    private var skillPage: some View {
        VStack(spacing: 20) {
            Image(systemName: "sparkles")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.available))

            Text("スキル")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            VStack(spacing: 8) {
                skillCard(name: "勇者", skill: "ダッシュ", desc: "2マス移動", count: Constants.heroSkillMaxUsage, icon: "figure.run")
                skillCard(name: "盗賊", skill: "斜め移動", desc: "斜め方向に移動", count: Constants.thiefSkillMaxUsage, icon: "arrow.up.right")
                skillCard(name: "魔法使い", skill: "透明化", desc: "衝突時に無敵", count: Constants.wizardSkillMaxUsage, icon: "eye.slash.fill")
                skillCard(name: "エルフ", skill: "拘束", desc: "鬼を停止", count: Constants.elfSkillMaxUsage, icon: "link")
            }
            .padding(.horizontal, 30)
        }
    }

    private func skillCard(name: String, skill: String, desc: String, count: Int, icon: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon)
                .font(.system(size: 18))
                .foregroundColor(Color(hex: GameColors.available))
                .frame(width: 30)

            VStack(alignment: .leading, spacing: 2) {
                Text("\(name) - \(skill)")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text))
                Text(desc)
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.6))
            }

            Spacer()

            Text("x\(count)")
                .font(.fantasyNumber())
                .foregroundColor(Color(hex: GameColors.textSecondary))
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.backgroundSecondary))
        )
    }

    // MARK: - Page 5: Special Rules (NEW)

    private var specialRulesPage: some View {
        VStack(spacing: 20) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.warning))

            Text("特殊ルール")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            VStack(spacing: 16) {
                specialRuleCard(
                    icon: "cloud.fog.fill",
                    title: "霧の呪い（階層21〜）",
                    desc: "周囲のマスしか見えなくなります。\n鬼がどこにいるか分からない！"
                )

                specialRuleCard(
                    icon: "square.dashed",
                    title: "崩壊の罠（階層41〜）",
                    desc: "ランダムでマスが消失します。\n消えたマスには移動できません！"
                )

                specialRuleCard(
                    icon: "bolt.fill",
                    title: "両方発動（階層61〜）",
                    desc: "霧と消失が同時に発動！\n最高難度に挑戦しましょう。"
                )
            }
            .padding(.horizontal, 30)
        }
    }

    private func specialRuleCard(icon: String, title: String, desc: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: icon)
                .font(.system(size: 22))
                .foregroundColor(Color(hex: GameColors.warning))
                .frame(width: 30)

            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.textSecondary))
                Text(desc)
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
                    .lineSpacing(4)
            }

            Spacer()
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.backgroundSecondary))
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color(hex: GameColors.warning).opacity(0.3), lineWidth: 1)
                )
        )
    }

    // MARK: - Page 6: Let's Start

    private var startPage: some View {
        VStack(spacing: 20) {
            Image(systemName: "flag.fill")
                .font(.system(size: ResponsiveLayout.isIPad() ? 72 : 60))
                .foregroundColor(Color(hex: GameColors.success))

            Text("さあ始めよう！")
                .font(.fantasyHeading())
                .foregroundColor(Color(hex: GameColors.text))

            VStack(spacing: 12) {
                Text("難易度を選んでスタート！")
                    .font(.fantasyBody())
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.8))

                VStack(spacing: 6) {
                    difficultyRow(level: "Easy", desc: "鬼がゆっくり追ってくる")
                    difficultyRow(level: "Normal", desc: "鬼が最短距離で追ってくる")
                    difficultyRow(level: "Hard", desc: "鬼が移動先を予測してくる")
                }
                .padding(.horizontal, 40)

                Text("100階層クリアを目指しましょう！")
                    .font(.fantasyCaption())
                    .foregroundColor(Color(hex: GameColors.textSecondary))
                    .padding(.top, 8)
            }
        }
    }

    private func difficultyRow(level: String, desc: String) -> some View {
        HStack {
            Text(level)
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.available))
                .frame(width: 60, alignment: .leading)
            Text(desc)
                .font(.fantasyCaption())
                .foregroundColor(Color(hex: GameColors.text).opacity(0.7))
            Spacer()
        }
    }

    // MARK: - Mini Illustrations

    private var miniGridIllustration: some View {
        VStack(spacing: 2) {
            ForEach(0..<3, id: \.self) { row in
                HStack(spacing: 2) {
                    ForEach(0..<3, id: \.self) { col in
                        let isPlayer = (row == 2 && col == 0)
                        let isEnemy = (row == 0 && col == 2)
                        RoundedRectangle(cornerRadius: 4)
                            .fill(
                                isPlayer ? Color(hex: GameColors.player).opacity(0.7) :
                                isEnemy ? Color(hex: GameColors.enemy).opacity(0.7) :
                                Color(hex: GameColors.grid)
                            )
                            .frame(width: ResponsiveLayout.isIPad() ? 50 : 36, height: ResponsiveLayout.isIPad() ? 50 : 36)
                            .overlay(
                                Group {
                                    if isPlayer {
                                        Text("P")
                                            .font(.fantasyCaption())
                                            .foregroundColor(.white)
                                    } else if isEnemy {
                                        Text("E")
                                            .font(.fantasyCaption())
                                            .foregroundColor(.white)
                                    }
                                }
                            )
                    }
                }
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.background))
        )
    }

    private var movementIllustration: some View {
        VStack(spacing: 2) {
            ForEach(0..<3, id: \.self) { row in
                HStack(spacing: 2) {
                    ForEach(0..<3, id: \.self) { col in
                        let isPlayer = (row == 1 && col == 1)
                        let isAvailable = (row == 0 && col == 1) || (row == 2 && col == 1) ||
                                          (row == 1 && col == 0) || (row == 1 && col == 2)
                        RoundedRectangle(cornerRadius: 4)
                            .fill(
                                isPlayer ? Color(hex: GameColors.player).opacity(0.7) :
                                isAvailable ? Color(hex: GameColors.available).opacity(0.4) :
                                Color(hex: GameColors.grid)
                            )
                            .frame(width: ResponsiveLayout.isIPad() ? 50 : 36, height: ResponsiveLayout.isIPad() ? 50 : 36)
                            .overlay(
                                Group {
                                    if isPlayer {
                                        Text("P")
                                            .font(.fantasyCaption())
                                            .foregroundColor(.white)
                                    } else if isAvailable {
                                        Image(systemName: "arrow.up")
                                            .font(.system(size: 10))
                                            .foregroundColor(Color(hex: GameColors.available))
                                            .rotationEffect(
                                                row == 0 ? .degrees(0) :
                                                row == 2 ? .degrees(180) :
                                                col == 0 ? .degrees(-90) : .degrees(90)
                                            )
                                    }
                                }
                            )
                    }
                }
            }
        }
        .padding(8)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(hex: GameColors.background))
        )
    }
}
