// CellVisual.cs
// 盤面 1 マス分の表示状態 (Swift 正本: GridCellView.swift の let プロパティ群)。
//
// 元は旧 uGUI 盤面の GridCellWidget.cs (namespace EscapeNine.Runtime.UI) に定義されていたが、
// Phase 4.5 W5 で旧 uGUI 盤面 (GridBoardWidget / GridBoardWidgetAdapter / GridCellWidget) を
// 削除 (design D4: W4 ゲート通過後の旧盤面削除) した際、残った唯一の利用者である
// 3D 盤面側 (BoardStage.Render が組み立て、TileView.Render が消費) へ移設した。
// フィールド構成・コメントは移設前と完全に同一 (情報パリティ検査の対象フィールド群)。

namespace EscapeNine.Runtime.Stage
{
    /// <summary>1 マス分の表示状態。Swift: GridCellView の let プロパティ群に対応。</summary>
    public struct CellVisual
    {
        public bool IsPlayer;      // Swift: isPlayer
        public bool IsEnemy;       // Swift: isEnemy
        public bool IsAvailable;   // Swift: isAvailable (移動可能マス)
        public bool IsSelected;    // Swift: isSelected (予約済み移動先)
        public bool IsVisible;     // Swift: isVisible (霧マップで見えるか)
        public bool IsDisappeared; // Swift: isDisappeared (消失マス)
        public bool Disabled;      // Swift: disabled (gameStatus != .playing)
    }
}
