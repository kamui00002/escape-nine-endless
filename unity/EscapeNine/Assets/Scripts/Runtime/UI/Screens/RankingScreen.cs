// RankingScreen.cs
// Swift 正本: Views/Ranking/RankingView.swift + ViewModels/RankingViewModel.swift
//   - 自己ベストカード (あなたの記録) / タブ (プレイ履歴・クラウド) / ランキング行 / 空状態 を移植。
//   - Swift の List → コード構築の uGUI ScrollRect + 行パネル。
//   - クラウドタブは OnlineRankingService (Firebase Auth REST + Firestore REST) 経由の世界ランキング。
//     Swift 側に対応する REST 実装はまだ無い Unity 独自の追加 (Game Center 経由の Swift とは非対称)。
//     画面表示のたびに FetchRankings を実行し、成功時は世界ランキング (isMe 行をハイライト)、
//     失敗/未認証時は既存のローカル RankingStore 表示へフォールバックする (silent fail 禁止)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;
using EscapeNine.Runtime.Ranking;

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

        /// <summary>クラウドタブの取得状態。Swift の isLoading/hasError 相当をまとめた簡易状態機械。</summary>
        private enum CloudState
        {
            Loading,
            Success,
            Failed
        }

        /// <summary>1 行あたりの高さ (ビューポート高さ比、行間込み)。</summary>
        private const float RowHeightVp = 0.085f;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築を防ぐガード (HomeScreen と同じ対策)。
        private bool _built;

        private Tab _selectedTab = Tab.Local;

        // ---- クラウド (世界ランキング) 状態 ----
        private CloudState _cloudState = CloudState.Loading;
        private List<OnlineRankingEntry> _cloudEntries;

        /// <summary>連続表示 (OnShow 連打) で古いフェッチ応答が新しい結果を上書きしないための世代カウンタ。</summary>
        private int _cloudFetchGeneration;

        // ---- 動的要素への参照 ----
        private TextMeshProUGUI _myRecordLabel;      // 自己ベスト階層の数字
        private Image _tabLocalBg;
        private TextMeshProUGUI _tabLocalLabel;
        private Image _tabCloudBg;
        private TextMeshProUGUI _tabCloudLabel;
        private GameObject _scrollRoot;   // ローカル履歴リスト
        private ScrollRect _scroll;
        private RectTransform _listContent;
        private GameObject _emptyPanel;   // ローカル履歴が空のとき
        private GameObject _cloudPanel;   // クラウドタブ (読み込み中 / データなしの簡易表示)
        private TextMeshProUGUI _cloudTitleLabel;
        private TextMeshProUGUI _cloudCaptionLabel;

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

            // 背景 (HD-2D、2026-07-07: 単色 ColorRect から縦グラデ+下端ヴィネットの軽量版へ)
            UIFactory.SimpleDepthBackground(transform);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildMyRecordCard(safe);
            BuildTabs(safe);
            BuildListArea(safe);
        }

        public override void OnShow(object payload)
        {
            // 「表示を世界ランキングにする」方針のため、表示のたびにクラウドタブへリセットしフェッチする
            // (Swift の Game Center 経由とは非対称。ローカル履歴は引き続きタブ切替で参照可能)。
            _selectedTab = Tab.Cloud;
            _cloudState = CloudState.Loading;
            _cloudEntries = null;
            RefreshAll();

            // この呼び出しの世代をラムダのクロージャで捕捉し、コールバック到達時に古い世代なら破棄する
            // (連続表示で発生し得る stale 応答の上書きを防ぐ)。
            int generation = ++_cloudFetchGeneration;
            App.I.OnlineRanking.FetchRankings(entries => OnCloudRankingsFetched(generation, entries));
        }

        /// <summary>OnlineRankingService.FetchRankings のコールバック。null = 取得失敗 (未認証/ネットワーク不可等)。</summary>
        private void OnCloudRankingsFetched(int generation, List<OnlineRankingEntry> entries)
        {
            if (generation != _cloudFetchGeneration) return; // stale (この後に新しいフェッチが開始済み) は破棄

            if (entries != null)
            {
                _cloudState = CloudState.Success;
                _cloudEntries = entries;
            }
            else
            {
                _cloudState = CloudState.Failed;
                _cloudEntries = null;
            }

            RefreshAll();
        }

        // MARK: - Header (Swift: 戻るボタン + タイトル "ランキング")

        private void BuildHeader(RectTransform parent)
        {
            // HD-2D (2026-07-07): Home のサブボタンと同じ質感に統一。
            UIFactory.SecondaryButton(parent, "BackButton", "← 戻る", 0.12f, 0.955f, 0.18f, 0.045f,
                OnBackTapped, 36);

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
            // HD-2D (2026-07-07): フラット塗り+手動4辺ボーダーから Card(PanelFill) + BorderTrim へ。
            // 枠線色は元の Available (Swift: available→main グラデの簡略単色) を踏襲する。
            var card = UIFactory.Card(parent, "MyRecordCard", out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(card, 0.5f, 0.855f, 0.92f, 0.13f);
            UIFactory.BorderTrim(card, "MyRecordCardBorder", UITheme.Available, 0.5f);

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
            // HD-2D (2026-07-07): Card で浮かせ + Emboss で質感統一。選択状態で背景/文字色を動的に
            // 塗り替える (ApplyTabVisual) ため、塗りを ButtonFill 固定にする SecondaryButton ではなく
            // ElevatedButton (色を渡せる版) を使う。TextButton 自身の Image は Card ラップの有無に
            // 関わらず返り値の Button 直下にあるため、GetComponent<Image>() での動的着色は維持される。
            var local = UIFactory.ElevatedButton(parent, "TabLocal", "プレイ履歴", 36,
                UITheme.Background, UITheme.TextColor, 0.29f, 0.765f, 0.42f, 0.045f, () => SelectTab(Tab.Local));
            UIFactory.EmbossTrim(local.transform, "TabLocalEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            _tabLocalBg = local.GetComponent<Image>();
            _tabLocalLabel = local.GetComponentInChildren<TextMeshProUGUI>();

            var cloud = UIFactory.ElevatedButton(parent, "TabCloud", "クラウド", 36,
                UITheme.Background, UITheme.TextColor, 0.71f, 0.765f, 0.42f, 0.045f, () => SelectTab(Tab.Cloud));
            UIFactory.EmbossTrim(cloud.transform, "TabCloudEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            _tabCloudBg = cloud.GetComponent<Image>();
            _tabCloudLabel = cloud.GetComponentInChildren<TextMeshProUGUI>();
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

            // クラウドタブ: 読み込み中 / 取得失敗かつローカル記録も無い場合の簡易表示
            // (取得成功時、および取得失敗でもローカル記録があるときはスクロールリストを使うため非表示になる)。
            var cloud = UIFactory.Panel(parent, "CloudPanel");
            UIFactory.Place(cloud, 0.5f, 0.365f, 1f, 0.73f);
            var cloudTitle = UIFactory.Label(cloud, "Title", "読み込み中...", 40,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f));
            UIFactory.Place((RectTransform)cloudTitle.transform, 0.5f, 0.56f, 0.9f, 0.10f);
            var cloudCaption = UIFactory.Label(cloud, "Caption", "世界ランキングを取得しています", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.5f));
            UIFactory.Place((RectTransform)cloudCaption.transform, 0.5f, 0.43f, 0.9f, 0.14f);
            _cloudPanel = cloud.gameObject;
            _cloudTitleLabel = cloudTitle;
            _cloudCaptionLabel = cloudCaption;
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
            UpdateCloudTabLabel();

            if (_selectedTab == Tab.Local)
            {
                RefreshLocalList();
                return;
            }

            RefreshCloudList();
        }

        /// <summary>ローカルタブの描画 (Swift 正本のローカル履歴表示、変更なし)。</summary>
        private void RefreshLocalList()
        {
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

        /// <summary>
        /// クラウドタブの描画。読み込み中はプレースホルダ、成功時は世界ランキング (isMe ハイライト)、
        /// 失敗時は既存ローカル RankingStore 表示へフォールバックする (silent fail 禁止、
        /// タブラベルに "(オフライン)" を付けて簡易表示する。UpdateCloudTabLabel 参照)。
        /// </summary>
        private void RefreshCloudList()
        {
            switch (_cloudState)
            {
                case CloudState.Loading:
                    _scrollRoot.SetActive(false);
                    _emptyPanel.SetActive(false);
                    _cloudPanel.SetActive(true);
                    if (_cloudTitleLabel != null) _cloudTitleLabel.text = "読み込み中...";
                    if (_cloudCaptionLabel != null) _cloudCaptionLabel.text = "世界ランキングを取得しています";
                    break;

                case CloudState.Success:
                    if (_cloudEntries == null || _cloudEntries.Count == 0)
                    {
                        _cloudPanel.SetActive(false);
                        _scrollRoot.SetActive(false);
                        _emptyPanel.SetActive(true);
                    }
                    else
                    {
                        _cloudPanel.SetActive(false);
                        _emptyPanel.SetActive(false);
                        _scrollRoot.SetActive(true);
                        RebuildOnlineRows(_cloudEntries);
                    }
                    break;

                case CloudState.Failed:
                    IReadOnlyList<RankingEntry> localEntries = App.I.Ranking.GetRankings();
                    if (localEntries.Count == 0)
                    {
                        _scrollRoot.SetActive(false);
                        _emptyPanel.SetActive(false);
                        _cloudPanel.SetActive(true);
                        if (_cloudTitleLabel != null) _cloudTitleLabel.text = "世界ランキングを取得できませんでした";
                        if (_cloudCaptionLabel != null) _cloudCaptionLabel.text = "ネットワーク接続を確認してください";
                    }
                    else
                    {
                        _cloudPanel.SetActive(false);
                        _emptyPanel.SetActive(false);
                        _scrollRoot.SetActive(true);
                        RebuildRows(localEntries); // 既存ローカル RankingStore 表示へフォールバック
                    }
                    break;
            }
        }

        /// <summary>クラウドタブのラベルに取得失敗の簡易表示 ("(オフライン)") を付与する。</summary>
        private void UpdateCloudTabLabel()
        {
            if (_tabCloudLabel == null) return;
            _tabCloudLabel.text = _cloudState == CloudState.Failed ? "クラウド(オフライン)" : "クラウド";
        }

        private static void ApplyTabVisual(Image bg, TextMeshProUGUI label, bool selected)
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
            // HD-2D (2026-07-07): フラット塗りの行パネルから Card(PanelFill) + BorderTrim へ
            // (BPMInfoWidget と同じ「枠付き計器パネル」の質感)。
            var row = UIFactory.Card(parent, "Row" + index, out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(row, 0.5f, cy, 0.94f, h);
            UIFactory.BorderTrim(row, "Row" + index + "Border", UITheme.Accent, 0.4f);

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

        // MARK: - Online Rows (世界ランキング。ローカル行と同じ Card/Border 質感を流用し、
        // 日付の代わりに isMe ハイライトを持たせる)

        /// <summary>RebuildRows と同型 (OnlineRankingEntry 版)。データ源が違うだけで生成ロジックは同じにする。</summary>
        private void RebuildOnlineRows(IReadOnlyList<OnlineRankingEntry> entries)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            float k = Mathf.Max(1f, entries.Count * RowHeightVp + 0.02f);
            SetContentHeight(_listContent, k);

            for (int i = 0; i < entries.Count; i++)
            {
                float cy = 1f - (0.01f + RowHeightVp * (i + 0.5f)) / k;
                float h = RowHeightVp * 0.9f / k;
                BuildOnlineRow(_listContent, i, entries[i], cy, h);
            }

            _scroll.verticalNormalizedPosition = 1f;
            _listContent.anchoredPosition = Vector2.zero;
        }

        private void BuildOnlineRow(RectTransform parent, int index, OnlineRankingEntry entry, float cy, float h)
        {
            var row = UIFactory.Card(parent, "OnlineRow" + index, out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(row, 0.5f, cy, 0.94f, h);
            // isMe (自分の記録) はボーダーをゴールドで強調する (Swift に対応表現が無いため Unity 独自)。
            UIFactory.BorderTrim(row, "OnlineRow" + index + "Border",
                entry.IsMe ? UITheme.GoldText : UITheme.Accent, entry.IsMe ? 0.7f : 0.4f);

            var rank = UIFactory.Label(row, "Rank", "#" + (index + 1), 44, RankColor(index),
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)rank.transform, 0.10f, 0.5f, 0.18f, 0.9f);

            // 日付が無い分、名前ラベルは縦中央いっぱいを使う (ローカル行は名前+日付の2段組)。
            string nameText = CharacterDisplayName(entry.CharacterType) + "  " + entry.DisplayName
                + (entry.IsMe ? " (YOU)" : "");
            var name = UIFactory.Label(row, "Name", nameText, 36,
                entry.IsMe ? UITheme.GoldText : UITheme.TextColor, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)name.transform, 0.45f, 0.5f, 0.52f, 0.9f);

            var floor = UIFactory.Label(row, "Floor", entry.Floor + "階", 44, UITheme.GoldText,
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
