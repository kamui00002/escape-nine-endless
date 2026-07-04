// GridBoardWidgetAdapter.cs
// GridBoardWidget (Wave 1 uGUI 盤面) を IBoardView へ委譲するアダプタ。
// GridBoardWidget.cs 自体は変更禁止 (W4 ゲート通過までの比較対象として温存) のため、
// 新規ファイルとしてここで契約を橋渡しする。GameScreen.UseWorldBoard=false
// (旧経路) を選んだ時のみ使われる。GridBoardWidget の公開 API をそのまま
// フォワードするだけで、独自のロジックは持たない。

using System;
using UnityEngine;
using EscapeNine.Core;
using EscapeNine.Runtime.Stage;

namespace EscapeNine.Runtime.UI
{
    public sealed class GridBoardWidgetAdapter : IBoardView
    {
        private readonly GridBoardWidget _widget;

        public GridBoardWidgetAdapter(GridBoardWidget widget)
        {
            _widget = widget;
        }

        public event Action<int> OnCellTapped
        {
            add { _widget.OnCellTapped += value; }
            remove { _widget.OnCellTapped -= value; }
        }

        public event Action OnEnemyTapped
        {
            add { _widget.OnEnemyTapped += value; }
            remove { _widget.OnEnemyTapped -= value; }
        }

        public void Render(GameSession session, bool disabled, Sprite playerSprite, Sprite enemySprite)
            => _widget.Render(session, disabled, playerSprite, enemySprite);

        public void SnapNextRender() => _widget.SnapNextRender();

        public void FlashPlayer(Color color, float duration = 0.2f) => _widget.FlashPlayer(color, duration);

        public void BurstAtPlayer(Color color, int count = 12, float speed = 600f)
            => _widget.BurstAtPlayer(color, count, speed);

        public void Shake(float amplitude = 12f, float duration = 0.3f) => _widget.Shake(amplitude, duration);

        public void ResetFxState() => _widget.ResetFxState();
    }
}
