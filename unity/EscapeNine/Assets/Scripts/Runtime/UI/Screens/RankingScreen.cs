// RankingScreen.cs
// Swift 正本: Views/Ranking/RankingView.swift + ViewModels/RankingViewModel.swift
//   - 自己ベストカード (あなたの記録) / タブ (プレイ履歴・クラウド) / ランキング行 / 空状態 を移植。
//   - Swift の List → コード構築の uGUI ScrollRect + 行パネル。
//   - クラウドタブ (Firestore) と Game Center ボタンは Phase 3 のため「準備中」プレースホルダのみ。
//     (RankingViewModel.fetchCloudRankings / isLoading / hasError / retry のオンライン系状態機械も
//      Phase 3 でクラウド実装とセットで移植する。ローカルは同期取得なのでローディング表示は不要。)
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// ランキング画面。ローカル履歴 (RankingStore) の順位・階層・日付・使用キャラと自己ベストを表示する。
    /// </summary>
    public sealed class RankingScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Ranking;

        /// <summary>タブ種別。Swift: enum RankingTab { local, cloud }</summary>
        private enum Tab
        {
            Local,
            Cloud
        }

        /// <summary>1 行あたりの高さ (ビューポート高さ比、行間込み)。</summary>
        private const float RowHeightVp = 0.085f;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築を防ぐガード (HomeScreen と同じ対策)。
        private bool _built;

        private Tab _selectedTab = Tab.Local;

        // ---- 動的要素への参照 ----
        private Text _myRecordLabel;      // 自己ベスト階層の数字
        private Image _tabLocalBg;
        private Text _tabLocalLabel;
        private Image _tabCloudBg;
        private Text _tabCloudLabel;
        private GameObject _scrollRoot;   // ローカル履歴リスト
        private ScrollRect _scroll;
        private RectTransform _listContent;
        private GameObject _emptyPanel;   // ローカル履歴が空のとき
        private GameObject _cloudPanel;   // クラウドタブ (準備中)

        public override void BuildUI()
        {
            if (_built) return; // Router.Register が 1 回だけ呼ぶ (再入防御。呼び出し元は ScreenRouter.Register のみ)
            _built = true;

            // 画面ルートを親いっぱいに固定 (シーン側の配置ミスに影響されないための防御)
            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            // 背景 (Swift: background → backgroundSecondary の縦グラデーション。
            // グラデーションは Phase 4/juice 送りで単色に簡略化)
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildMyRecordCard(safe);
            BuildTabs(safe);
            BuildListArea(safe);
        }

        public override void OnShow(object payload)
        {
            // Swift は @StateObject が push のたびに作り直され selectedTab = .local に戻る。
            // それに合わせて表示のたびにローカルタブへリセットする。
            _selectedTab = Tab.Local;
            RefreshAll();
        }

        // MARK: - Header (Swift: 戻るボタン + タイトル "ランキング")

        private void BuildHeader(RectTransform parent)
        {
            var back = UIFactory.TextButton(parent, "BackButton", "← 戻る", 36,
                UITheme.BackgroundSecondary, UITheme.TextColor, OnBackTapped);
            UIFactory.Place((RectTransform)back.transform, 0.12f, 0.955f, 0.18f, 0.045f);

            var title = UIFactory.Label(parent, "TitleLabel", "ランキング", 64, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.955f, 0.5f, 0.05f);
        }

        /// <summary>戻る = Home へ (Swift: dismiss()。ランキングは Home からしか開かないため固定で良い)。</summary>
        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - My Record (Swift: 「あなたの記録」カード)

        private void BuildMyRecordCard(RectTransform parent)
        {
            var card = UIFactory.Panel(parent, "MyRecordCard", UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.95f));
            UIFactory.Place(card, 0.5f, 0.855f, 0.92f, 0.13f);

            // 枠線 (Swift: available→main のグラデーションストローク → 単色 4 辺に簡略化)
            var border = UITheme.WithAlpha(UITheme.Available, 0.5f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderT", border).transform, 0.5f, 1f, 1f, 0.03f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderB", border).transform, 0.5f, 0f, 1f, 0.03f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderL", border).transform, 0f, 0.5f, 0.008f, 1f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderR", border).transform, 1f, 0.5f, 0.008f, 1f);

            var caption = UIFactory.Label(card, "Caption", "あなたの記録", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)caption.transform, 0.5f, 0.80f, 0.8f, 0.24f);

            _myRecordLabel = UIFactory.Label(card, "FloorNumber", "0", 84, UITheme.Available,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_myRecordLabel.transform, 0.5f, 0.47f, 0.8f, 0.42f);

            var unit = UIFactory.Label(card, "Unit", "階層", 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)unit.transform, 0.5f, 0.15f, 0.8f, 0.22f);
        }

        // MARK: - Tabs (Swift: Picker(.segmented) プレイ履歴 / クラウド)

        private void BuildTabs(RectTransform parent)
        {
            var local = UIFactory.TextButton(parent, "TabLocal", "プレイ履歴", 36,
                UITheme.Background, UITheme.TextColor, () => SelectTab(Tab.Local));
            UIFactory.Place((RectTransform)local.transform, 0.29f, 0.765f, 0.42f, 0.045f);
            _tabLocalBg = local.GetComponent<Image>();
            _tabLocalLabel = local.GetComponentInChildren<Text>();

            var cloud = UIFactory.TextButton(parent, "TabCloud", "クラウド", 36,
                UITheme.Background, UITheme.TextColor, () => SelectTab(Tab.Cloud));
            UIFactory.Place((RectTransform)cloud.transform, 0.71f, 0.765f, 0.42f, 0.045f);
            _tabCloudBg = cloud.GetComponent<Image>();
            _tabCloudLabel = cloud.GetComponentInChildren<Text>();
        }

        /// <summary>タブ切替 (Swift: viewModel.selectTab)。</summary>
        private void SelectTab(Tab tab)
        {
            App.I.Audio.PlaySfx("button_tap");
            _selectedTab = tab;
            RefreshAll();
        }

        // MARK: - List Area (Swift: List / 空状態 / クラウドプレースホルダ)

        private void BuildListArea(RectTransform parent)
        {
            // ローカル履歴スクロール
            _scroll = BuildScrollView(parent, "RankingScroll", out _listContent);
            var scrollRt = (RectTransform)_scroll.transform;
            UIFactory.Place(scrollRt, 0.5f, 0.365f, 1f, 0.73f);
            _scrollRoot = _scroll.gameObject;

            // 空状態 (Swift: "まだランキングがありません")
            var empty = UIFactory.Panel(parent, "EmptyPanel");
            UIFactory.Place(empty, 0.5f, 0.365f, 1f, 0.73f);
            var emptyTitle = UIFactory.Label(empty, "Title", "まだランキングがありません", 40,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f));
            UIFactory.Place((RectTransform)emptyTitle.transform, 0.5f, 0.56f, 0.9f, 0.10f);
            var emptyCaption = UIFactory.Label(empty, "Caption", "冒険を始めて記録を残そう", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.5f));
            UIFactory.Place((RectTransform)emptyCaption.transform, 0.5f, 0.47f, 0.9f, 0.08f);
            _emptyPanel = empty.gameObject;

            // クラウドタブ (Phase 3 準備中。Swift の世界ランキング (Game Center) ボタン +
            // クラウドランキング (Firestore) を将来ここへ実装する)
            var cloud = UIFactory.Panel(parent, "CloudPanel");
            UIFactory.Place(cloud, 0.5f, 0.365f, 1f, 0.73f);
            var cloudTitle = UIFactory.Label(cloud, "Title", "クラウドランキングは準備中です", 40,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f));
            UIFactory.Place((RectTransform)cloudTitle.transform, 0.5f, 0.56f, 0.9f, 0.10f);
            var cloudCaption = UIFactory.Label(cloud, "Caption",
                "Phase 3 で Game Center / Firestore の\n世界ランキングと連携します", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.5f));
            UIFactory.Place((RectTransform)cloudCaption.transform, 0.5f, 0.43f, 0.9f, 0.14f);
            _cloudPanel = cloud.gameObject;
        }

        // MARK: - 再描画 (Swift の @Published バインディング相当を OnShow / タブ切替で一括実行)

        private void RefreshAll()
        {
            // 自己ベスト (Swift: playerViewModel.highestFloor)
            if (_myRecordLabel != null)
            {
                _myRecordLabel.text = App.I.Player.HighestFloor.ToString();
            }

            // タブの選択見た目 (選択中 = ゴールド地 + 濃色文字)
            ApplyTabVisual(_tabLocalBg, _tabLocalLabel, _selectedTab == Tab.Local);
            ApplyTabVisual(_tabCloudBg, _tabCloudLabel, _selectedTab == Tab.Cloud);

            if (_selectedTab == Tab.Cloud)
            {
                _scrollRoot.SetActive(false);
                _emptyPanel.SetActive(false);
                _cloudPanel.SetActive(true);
                return;
            }

            _cloudPanel.SetActive(false);

            IReadOnlyList<RankingEntry> entries = App.I.Ranking.GetRankings();
            if (entries.Count == 0)
            {
                _scrollRoot.SetActive(false);
                _emptyPanel.SetActive(true);
                return;
            }

            _emptyPanel.SetActive(false);
            _scrollRoot.SetActive(true);
            RebuildRows(entries);
        }

        private static void ApplyTabVisual(Image bg, Text label, bool selected)
        {
            if (bg != null) bg.color = selected ? UITheme.Available : UITheme.Background;
            if (label != null)
            {
                label.color = selected ? UITheme.Background : UITheme.WithAlpha(UITheme.TextColor, 0.75f);
            }
        }

        // MARK: - Rows (Swift: rankingRow(entry:index:))

        /// <summary>
        /// ランキング行を全て作り直す。件数が変わるためコンテンツ高さもここで再計算する
        /// (SwiftUI の List が差分描画するのに対し、開くたび全再構築 = 最大 100 件なので許容)。
        /// </summary>
        private void RebuildRows(IReadOnlyList<RankingEntry> entries)
        {
            // 旧行を破棄 (Destroy は同フレーム末で消える。1 フレームの重なりは許容)
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            // コンテンツ高さ = 行数ぶん (最低でもビューポート 1 枚分 = スクロール不要時も自然に上詰め)
            float k = Mathf.Max(1f, entries.Count * RowHeightVp + 0.02f);
            SetContentHeight(_listContent, k);

            for (int i = 0; i < entries.Count; i++)
            {
                // コンテンツ空間 (0..1、1 が上端) での行中心
                float cy = 1f - (0.01f + RowHeightVp * (i + 0.5f)) / k;
                float h = RowHeightVp * 0.9f / k; // 0.9 = 行間ぶんの余白
                BuildRow(_listContent, i, entries[i], cy, h);
            }

            // 常に先頭 (1 位) から表示
            _scroll.verticalNormalizedPosition = 1f;
            _listContent.anchoredPosition = Vector2.zero;
        }

        private void BuildRow(RectTransform parent, int index, RankingEntry entry, float cy, float h)
        {
            var row = UIFactory.Panel(parent, "Row" + index, UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.95f));
            UIFactory.Place(row, 0.5f, cy, 0.94f, h);

            // 順位 (Swift: 1位 available / 2位 textSecondary / 3位 main / それ以外 text 0.7。
            // このパレットでは available と textSecondary が同じ #ffd700 だが、Swift の対応を忠実に保つ)
            var rank = UIFactory.Label(row, "Rank", "#" + (index + 1), 44, RankColor(index),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)rank.transform, 0.10f, 0.5f, 0.18f, 0.9f);

            // 使用キャラ + プレイヤー名 (Swift は絵文字 + 名前。legacy Text は絵文字グリフを
            // 描画できないため、キャラの日本語名に置き換える = 意図的差分)
            var name = UIFactory.Label(row, "Name",
                CharacterDisplayName(entry.characterType) + "  " + entry.playerName, 36,
                UITheme.TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)name.transform, 0.45f, 0.66f, 0.52f, 0.46f);

            // 日付 (Swift: entry.formattedDate、10pt 相当の小さめ表示)
            var date = UIFactory.Label(row, "Date", entry.FormattedDate, 24,
                UITheme.WithAlpha(UITheme.TextColor, 0.5f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)date.transform, 0.45f, 0.25f, 0.52f, 0.36f);

            // 到達階層 (Swift: "\(entry.floor)階" textSecondary)
            var floor = UIFactory.Label(row, "Floor", entry.floor + "階", 44, UITheme.GoldText,
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place((RectTransform)floor.transform, 0.86f, 0.5f, 0.24f, 0.9f);
        }

        private static Color RankColor(int index)
        {
            switch (index)
            {
                case 0: return UITheme.Available;   // Swift: GameColors.available
                case 1: return UITheme.GoldText;    // Swift: GameColors.textSecondary
                case 2: return UITheme.Main;        // Swift: GameColors.main
                default: return UITheme.WithAlpha(UITheme.TextColor, 0.7f);
            }
        }

        /// <summary>rawValue ("hero" 等) → 日本語キャラ名。未知の値は「冒険者」(Swift の default ⚔️ 相当)。</summary>
        private static string CharacterDisplayName(string raw)
        {
            switch (raw)
            {
                case "hero": return CharacterType.Hero.Name();
                case "thief": return CharacterType.Thief.Name();
                case "wizard": return CharacterType.Wizard.Name();
                case "elf": return CharacterType.Elf.Name();
                case "knight": return CharacterType.Knight.Name();
                default: return "冒険者";
            }
        }

        // MARK: - ScrollRect のコード構築 (SettingsScreen と同型。クラス間の共有シンボルを
        // 作らないため意図的に各画面へ private 複製している — 他画面担当エージェントとの
        // クラス名衝突を避けるトレードオフ)

        private static ScrollRect BuildScrollView(RectTransform parent, string name, out RectTransform content)
        {
            // ルートに透明 Image (raycastTarget=true) = 行の隙間でもドラッグを受ける
            var root = UIFactory.Panel(parent, name, Color.clear);

            var viewport = UIFactory.Panel(root, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>(); // はみ出したコンテンツを切り抜く

            content = UIFactory.Panel(viewport, "Content");

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; // iOS 風のバウンス
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        /// <summary>コンテンツの縦アンカーをビューポートの k 倍に広げる (上端揃え・固定 px なし)。</summary>
        private static void SetContentHeight(RectTransform content, float k)
        {
            content.pivot = new Vector2(0.5f, 1f);
            content.anchorMin = new Vector2(0f, 1f - k);
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
        }
    }
}
