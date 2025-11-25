# コードスタイル・規約

## 命名規則

### ファイル名
- PascalCase（例: `GameState.swift`, `AIEngine.swift`）
- ファイル名はクラス/構造体名と一致

### 変数・定数
- camelCase（例: `playerPosition`, `currentFloor`）
- 定数はstaticで定義（例: `Constants.maxFloors`）

### 型
- PascalCase（例: `CharacterType`, `GameStatus`）
- enum、struct、classすべて同様

### enum
- CaseIterableプロトコルを活用
- 小文字のrawValueを使用（例: `case hero = "hero"`）

## コード構造

### enum定数の使用
プロジェクト全体の定数は `Constants` enum にまとめる:
```swift
enum Constants {
    static let gridSize = 9
    static let maxFloors = 100
    static let maxTurns = 10
}
```

カラーパレットは `GameColors` enum で管理:
```swift
enum GameColors {
    static let main = "#f4a460"
    static let accent = "#daa520"
}
```

### Factory Pattern
キャラクター生成などでstaticファクトリメソッドを使用:
```swift
static func getCharacter(for type: CharacterType) -> Character {
    // ...
}
```

### SwiftUI Views
- `View` プロトコルを実装
- `body` プロパティでUIを構築
- レスポンシブレイアウトを考慮（ResponsiveLayout.swift使用）

## コメント
- 日本語コメントを使用
- ファイルヘッダーにはファイル名、作成日、作成者を記載
```swift
//
//  FileName.swift
//  EscapeNine-endless-
//
//  Created by 吉留徹 on 2025/11/14.
//
```

## アーキテクチャパターン

### MVVM
- **Model**: データ構造（Character, GameState, Floor, Skill）
- **View**: SwiftUIビュー（HomeView, GameView, etc.）
- **ViewModel**: ビューロジック（GameViewModel, PlayerViewModel, etc.）

### Combine
- リアクティブプログラミング
- データバインディング
- イベント処理

## 型安全
- enumを活用して型安全性を確保
- オプショナルは最小限に（明示的なデフォルト値を使用）
- switch文で全ケースをカバー

## プロトコル指向
- `CaseIterable`, `Identifiable` などのプロトコルを活用
- プロトコルエクステンションでデフォルト実装

## その他
- インデント: スペース4つ
- 行末にセミコロン不要
- guard文を積極的に使用
- 早期リターンを推奨
