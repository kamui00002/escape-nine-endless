//
//  Fonts.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//

import SwiftUI

extension Font {
    // ファンタジー系フォント
    static func fantasyTitle() -> Font {
        return .system(size: 48, weight: .bold, design: .serif)
    }
    
    static func fantasyHeading() -> Font {
        return .system(size: 32, weight: .bold, design: .serif)
    }
    
    static func fantasySubheading() -> Font {
        return .system(size: 24, weight: .semibold, design: .serif)
    }
    
    static func fantasyBody() -> Font {
        return .system(size: 18, weight: .regular, design: .serif)
    }
    
    static func fantasyCaption() -> Font {
        return .system(size: 14, weight: .regular, design: .serif)
    }
    
    static func fantasyNumber() -> Font {
        return .system(size: 28, weight: .bold, design: .monospaced)
    }
}

