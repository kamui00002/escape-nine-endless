//
//  TutorialStepInstructionView.swift
//  EscapeNine-endless-
//
//  Sprint 3 v1.1 動的オンボーディング — 上部に表示する Step ヘッダ + 説明文。
//  設計典拠: docs/onboarding-v1.1-design.md §3 各 Step 上部文字 / §5 VoiceOver。
//

import SwiftUI

/// 各チュートリアル Step の上部に表示する「Step n / 4」+ タイトル + 説明文。
///
/// アクセシビリティ要件 (設計書 §5):
/// - Dynamic Type: 全テキストがセマンティックフォント (`.caption` / `.title3` / `.body`)
/// - VoiceOver: `accessibilityValue` で "Step 2 / 4: 影を避けてください" 形式の
///   ナレーションを発火させるため要素を combine 化
/// - Bold Text 設定 ON で `.fontWeight(.bold)` が反映される設計
struct TutorialStepInstructionView: View {
    let stepNumber: Int
    let totalSteps: Int
    let title: String
    let subtitle: String?

    init(stepNumber: Int, totalSteps: Int = 4, title: String, subtitle: String? = nil) {
        self.stepNumber = stepNumber
        self.totalSteps = totalSteps
        self.title = title
        self.subtitle = subtitle
    }

    var body: some View {
        VStack(spacing: 10) {
            Text("Step \(stepNumber) / \(totalSteps)")
                .font(.caption)
                .fontWeight(.semibold)
                .foregroundColor(Color(hex: GameColors.textSecondary))
                .tracking(1.5)

            Text(title)
                .font(.title3)
                .fontWeight(.bold)
                .foregroundColor(Color(hex: GameColors.text))
                .multilineTextAlignment(.center)
                .fixedSize(horizontal: false, vertical: true)

            if let subtitle, !subtitle.isEmpty {
                Text(subtitle)
                    .font(.body)
                    .foregroundColor(Color(hex: GameColors.text).opacity(0.85))
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(.horizontal, 20)
        .padding(.vertical, 16)
        .frame(maxWidth: .infinity)
        .background(
            RoundedRectangle(cornerRadius: 14)
                .fill(Color(hex: GameColors.backgroundSecondary).opacity(0.85))
                .overlay(
                    RoundedRectangle(cornerRadius: 14)
                        .stroke(Color(hex: GameColors.accent).opacity(0.4), lineWidth: 1)
                )
        )
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Step \(stepNumber) / \(totalSteps): \(title)")
        .accessibilityValue(subtitle ?? "")
    }
}

#Preview("iPhone — Step 1") {
    VStack {
        TutorialStepInstructionView(
            stepNumber: 1,
            title: "動きを覚える",
            subtitle: "タップで隣のマスに移動できる"
        )
        Spacer()
    }
    .padding()
    .background(Color(hex: GameColors.background))
}

#Preview("iPhone — Step 3 (危険警告)") {
    VStack {
        TutorialStepInstructionView(
            stepNumber: 3,
            title: "ヒヤリ体験",
            subtitle: "あと 1 マスで影に飲まれる"
        )
        Spacer()
    }
    .padding()
    .background(Color(hex: GameColors.background))
}

#Preview("iPhone — タイトルのみ") {
    VStack {
        TutorialStepInstructionView(
            stepNumber: 4,
            title: "10 ターン耐えれば階層クリア"
        )
        Spacer()
    }
    .padding()
    .background(Color(hex: GameColors.background))
}

#Preview("iPad — large", traits: .landscapeLeft) {
    VStack {
        TutorialStepInstructionView(
            stepNumber: 2,
            title: "影を避ける",
            subtitle: "鬼の隣接 8 マスは危険圏。毎ターン 1 マスずつ近づいてくる"
        )
        .padding(.horizontal, 100)
        Spacer()
    }
    .padding(.top, 40)
    .frame(maxWidth: .infinity, maxHeight: .infinity)
    .background(Color(hex: GameColors.background))
}
