//
//  AnimationEffects.swift
//  EscapeNine-endless-
//
//  アニメーション効果とビジュアルエフェクトのヘルパー
//

import SwiftUI

// MARK: - View Modifiers for Effects

/// 3Dボタン効果（押すと奥に沈む）
struct PressableButtonStyle: ButtonStyle {
    let scale: CGFloat
    let shadowRadius: CGFloat

    init(scale: CGFloat = 0.95, shadowRadius: CGFloat = 8) {
        self.scale = scale
        self.shadowRadius = shadowRadius
    }

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .scaleEffect(configuration.isPressed ? scale : 1.0)
            .shadow(
                color: .black.opacity(configuration.isPressed ? 0.2 : 0.4),
                radius: configuration.isPressed ? shadowRadius / 2 : shadowRadius,
                y: configuration.isPressed ? 2 : 4
            )
            .animation(.spring(response: 0.3, dampingFraction: 0.6), value: configuration.isPressed)
    }
}

/// パルス（脈動）アニメーション
struct PulseEffect: ViewModifier {
    @State private var isPulsing = false
    let minScale: CGFloat
    let maxScale: CGFloat
    let duration: Double

    func body(content: Content) -> some View {
        content
            .scaleEffect(isPulsing ? maxScale : minScale)
            .animation(
                .easeInOut(duration: duration)
                .repeatForever(autoreverses: true),
                value: isPulsing
            )
            .onAppear {
                isPulsing = true
            }
    }
}

/// グローエフェクト（光の輝き）
struct GlowEffect: ViewModifier {
    let color: Color
    let radius: CGFloat
    let intensity: Double

    func body(content: Content) -> some View {
        content
            .shadow(color: color.opacity(intensity), radius: radius, x: 0, y: 0)
            .shadow(color: color.opacity(intensity * 0.7), radius: radius * 0.7, x: 0, y: 0)
            .shadow(color: color.opacity(intensity * 0.5), radius: radius * 0.5, x: 0, y: 0)
    }
}

/// シマーエフェクト（光沢の移動）
struct ShimmerEffect: ViewModifier {
    @State private var phase: CGFloat = 0
    let duration: Double

    func body(content: Content) -> some View {
        content
            .overlay(
                LinearGradient(
                    colors: [
                        .clear,
                        .white.opacity(0.3),
                        .clear
                    ],
                    startPoint: .leading,
                    endPoint: .trailing
                )
                .offset(x: phase)
                .mask(content)
            )
            .onAppear {
                withAnimation(.linear(duration: duration).repeatForever(autoreverses: false)) {
                    phase = 400
                }
            }
    }
}

/// シェイクエフェクト（振動）
struct ShakeEffect: GeometryEffect {
    var amount: CGFloat = 10
    var shakesPerUnit = 3
    var animatableData: CGFloat

    func effectValue(size: CGSize) -> ProjectionTransform {
        ProjectionTransform(
            CGAffineTransform(
                translationX: amount * sin(animatableData * .pi * CGFloat(shakesPerUnit)),
                y: 0
            )
        )
    }
}

/// バウンス登場アニメーション
struct BounceInEffect: ViewModifier {
    @State private var scale: CGFloat = 0.3
    @State private var opacity: Double = 0
    let delay: Double

    func body(content: Content) -> some View {
        content
            .scaleEffect(scale)
            .opacity(opacity)
            .onAppear {
                withAnimation(.spring(response: 0.6, dampingFraction: 0.6).delay(delay)) {
                    scale = 1.0
                    opacity = 1.0
                }
            }
    }
}

/// スライドイン登場アニメーション
struct SlideInEffect: ViewModifier {
    @State private var offset: CGFloat = 100
    @State private var opacity: Double = 0
    let delay: Double
    let from: Edge

    func body(content: Content) -> some View {
        content
            .offset(
                x: from == .leading || from == .trailing ? (from == .leading ? -offset : offset) : 0,
                y: from == .top || from == .bottom ? (from == .top ? -offset : offset) : 0
            )
            .opacity(opacity)
            .onAppear {
                withAnimation(.spring(response: 0.6, dampingFraction: 0.8).delay(delay)) {
                    offset = 0
                    opacity = 1.0
                }
            }
    }
}

/// パーティクルエフェクト（背景の粒子）
struct ParticleView: View {
    let particleCount: Int
    @State private var particles: [Particle] = []

    struct Particle: Identifiable {
        let id = UUID()
        var x: CGFloat
        var y: CGFloat
        var size: CGFloat
        var opacity: Double
        var speed: Double
    }

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                ForEach(particles) { particle in
                    Circle()
                        .fill(Color.white)
                        .frame(width: particle.size, height: particle.size)
                        .opacity(particle.opacity)
                        .position(x: particle.x, y: particle.y)
                }
            }
            .onAppear {
                createParticles(in: geometry.size)
                animateParticles(in: geometry.size)
            }
        }
    }

    private func createParticles(in size: CGSize) {
        particles = (0..<particleCount).map { _ in
            Particle(
                x: CGFloat.random(in: 0...size.width),
                y: CGFloat.random(in: 0...size.height),
                size: CGFloat.random(in: 1...3),
                opacity: Double.random(in: 0.1...0.4),
                speed: Double.random(in: 20...60)
            )
        }
    }

    private func animateParticles(in size: CGSize) {
        Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { _ in
            for i in particles.indices {
                particles[i].y -= particles[i].speed * 0.05

                if particles[i].y < -10 {
                    particles[i].y = size.height + 10
                    particles[i].x = CGFloat.random(in: 0...size.width)
                }
            }
        }
    }
}

/// カウントアップ数字アニメーション
struct AnimatedNumber: View {
    let value: Int
    let duration: Double
    @State private var displayValue: Int = 0

    var body: some View {
        Text("\(displayValue)")
            .onChange(of: value) { newValue in
                animateCount(to: newValue)
            }
            .onAppear {
                animateCount(to: value)
            }
    }

    private func animateCount(to target: Int) {
        let steps = min(50, abs(target - displayValue))
        guard steps > 0 else {
            displayValue = target
            return
        }

        let stepDuration = duration / Double(steps)
        let increment = (target - displayValue) / steps

        for i in 0..<steps {
            DispatchQueue.main.asyncAfter(deadline: .now() + stepDuration * Double(i)) {
                displayValue += increment
            }
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + duration) {
            displayValue = target
        }
    }
}

// MARK: - View Extensions

extension View {
    /// 3Dボタン効果を適用
    func pressableButton(scale: CGFloat = 0.95, shadowRadius: CGFloat = 8) -> some View {
        buttonStyle(PressableButtonStyle(scale: scale, shadowRadius: shadowRadius))
    }

    /// パルスアニメーションを適用
    func pulse(minScale: CGFloat = 1.0, maxScale: CGFloat = 1.1, duration: Double = 1.0) -> some View {
        modifier(PulseEffect(minScale: minScale, maxScale: maxScale, duration: duration))
    }

    /// グローエフェクトを適用
    func glow(color: Color, radius: CGFloat = 10, intensity: Double = 0.8) -> some View {
        modifier(GlowEffect(color: color, radius: radius, intensity: intensity))
    }

    /// シマーエフェクトを適用
    func shimmer(duration: Double = 2.0) -> some View {
        modifier(ShimmerEffect(duration: duration))
    }

    /// シェイクエフェクトを適用
    func shake(amount: CGFloat = 10, trigger: Bool) -> some View {
        modifier(ShakeEffect(amount: amount, animatableData: trigger ? 1 : 0))
    }

    /// バウンス登場アニメーション
    func bounceIn(delay: Double = 0) -> some View {
        modifier(BounceInEffect(delay: delay))
    }

    /// スライドイン登場アニメーション
    func slideIn(from edge: Edge = .bottom, delay: Double = 0) -> some View {
        modifier(SlideInEffect(delay: delay, from: edge))
    }
}

// MARK: - Celebration Effects

/// 階層クリア時のお祝いエフェクト
struct CelebrationEffect: View {
    @State private var showConfetti = false
    @State private var scale: CGFloat = 0.5
    @State private var rotation: Double = 0

    var body: some View {
        ZStack {
            // 背景のフラッシュ
            Color.white
                .opacity(showConfetti ? 0 : 0.3)
                .ignoresSafeArea()
                .animation(.easeOut(duration: 0.5), value: showConfetti)

            // 紙吹雪
            ForEach(0..<30, id: \.self) { index in
                ConfettiPiece(index: index)
            }

            // 中央のアイコン
            Image(systemName: "star.fill")
                .font(.system(size: 100))
                .foregroundColor(.yellow)
                .scaleEffect(scale)
                .rotationEffect(.degrees(rotation))
        }
        .onAppear {
            withAnimation(.spring(response: 0.6, dampingFraction: 0.6)) {
                scale = 1.2
            }
            withAnimation(.linear(duration: 1.0).repeatForever(autoreverses: false)) {
                rotation = 360
            }
            showConfetti = true
        }
    }
}

struct ConfettiPiece: View {
    let index: Int
    @State private var y: CGFloat = -100
    @State private var x: CGFloat = 0
    @State private var rotation: Double = 0
    @State private var opacity: Double = 1.0

    let colors: [Color] = [.red, .blue, .green, .yellow, .orange, .purple, .pink]

    var body: some View {
        Rectangle()
            .fill(colors[index % colors.count])
            .frame(width: 10, height: 20)
            .rotationEffect(.degrees(rotation))
            .opacity(opacity)
            .position(x: x, y: y)
            .onAppear {
                let randomX = CGFloat.random(in: 50...350)
                let randomDelay = Double.random(in: 0...0.5)

                x = randomX

                withAnimation(.easeIn(duration: 2.0).delay(randomDelay)) {
                    y = 1000
                    rotation = Double.random(in: 360...720)
                    opacity = 0
                }
            }
    }
}
