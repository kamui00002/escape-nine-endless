// ScreenRouter.cs
// Swift 正本: SwiftUI 版の画面遷移 (HomeView からの fullScreenCover / 各 View の切替) を
// 「1 Canvas + 画面ごとのルートパネルを SetActive で切替」する uGUI 定番構成に置き換える。
// SwiftUI はビュー階層の再構築で遷移するが、uGUI では生成済み UI の活性切替の方が
// GC/レイアウト負荷が低く、リズムゲームのフレーム安定に有利なため。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeNine.Runtime.UI
{
    /// <summary>画面 ID。SwiftUI 版の主要 View と 1:1 対応。</summary>
    public enum ScreenId
    {
        Home,            // HomeView
        Game,            // GameView
        Result,          // ResultView
        Ranking,         // RankingView
        Shop,            // ShopView
        CharacterSelect, // CharacterSelectionView
        Settings,        // SettingsView
        Tutorial,        // TutorialOverlayView
    }

    /// <summary>
    /// 各画面のルートにアタッチする基底クラス。
    /// BuildUI() は Register 時に 1 回だけ呼ばれ、UIFactory で子階層を組み立てる。
    /// OnShow/OnHide は表示切替のたびに呼ばれる (SwiftUI の onAppear/onDisappear 相当)。
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public abstract ScreenId Id { get; }

        /// <summary>UI 階層をコードで構築する。Register 時に 1 回だけ呼ばれる。</summary>
        public abstract void BuildUI();

        /// <summary>表示された直後。payload は Show() の呼び出し側が渡す任意データ (例: リザルト情報)。</summary>
        public virtual void OnShow(object payload) { }

        /// <summary>非表示になる直前。</summary>
        public virtual void OnHide() { }
    }

    /// <summary>
    /// 画面切替の唯一の入口。MonoBehaviour ではなく素のクラス
    /// (App が保持して寿命管理するため、シーンオブジェクトである必要がない)。
    /// </summary>
    public sealed class ScreenRouter
    {
        private readonly Dictionary<ScreenId, ScreenBase> _screens = new Dictionary<ScreenId, ScreenBase>();

        // enum 既定値 (Home) と「まだ何も表示していない」状態を区別するためのフラグ。
        // これが無いと初回 Show(Home) で Home 自身に OnHide が飛ぶバグになる。
        private bool _hasCurrent;
        private ScreenId _current;

        /// <summary>現在表示中の画面。初回 Show 前は既定値 (Home) を返す。</summary>
        public ScreenId Current => _current;

        /// <summary>画面が切り替わった時に発火 (引数 = 新しい画面)。BGM 切替やログ用。</summary>
        public event Action<ScreenId> OnChanged;

        /// <summary>
        /// 画面を登録する。BuildUI() を即時実行し、非表示状態にして待機させる。
        /// 起動時に全画面を一括登録する前提 (遷移時の構築スパイクを避ける)。
        /// </summary>
        public void Register(ScreenBase screen)
        {
            if (screen == null)
            {
                Debug.LogError("[ScreenRouter] null の画面は登録できない");
                return;
            }
            if (_screens.ContainsKey(screen.Id))
            {
                Debug.LogError($"[ScreenRouter] 画面 {screen.Id} は登録済み (二重登録を無視)");
                return;
            }

            _screens.Add(screen.Id, screen);
            screen.BuildUI();
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// 指定画面へ切り替える。前画面: OnHide → SetActive(false)、
        /// 新画面: SetActive(true) → OnShow(payload) の順で呼ぶ。
        /// 同一画面への Show は再表示せず OnShow のみ再送する (payload 更新用途)。
        /// </summary>
        public void Show(ScreenId id, object payload = null)
        {
            if (!_screens.TryGetValue(id, out ScreenBase next))
            {
                Debug.LogError($"[ScreenRouter] 未登録の画面: {id}");
                return;
            }

            // 同一画面: activate し直すとちらつくので OnShow だけ通す
            if (_hasCurrent && _current == id)
            {
                next.OnShow(payload);
                return;
            }

            if (_hasCurrent && _screens.TryGetValue(_current, out ScreenBase prev))
            {
                prev.OnHide();
                prev.gameObject.SetActive(false);
            }

            next.gameObject.SetActive(true);
            next.OnShow(payload);

            _current = id;
            _hasCurrent = true;
            OnChanged?.Invoke(id);
        }
    }
}
