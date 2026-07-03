// SettingsScreen.cs
// Swift 正本: Views/Settings/SettingsView.swift
//   - playerInfoSection (冒険者情報) / gameplaySection (ワンタップリトライ・触覚) /
//     soundSection (BGM・効果音スライダー) / aboutSection / purchaseSection を移植。
//   - #if DEBUG debugSection (管理者用設定) は HomeScreen の DangerZone へ移植済みのため
//     本画面では意図的に持たない (二重管理を避ける)。
//   - タスク要件による追加: AI 難易度選択 (Swift ではゲーム開始前選択だった項目を設定に集約) /
//     チュートリアル再表示 / プライバシーポリシー・サポート URL / バージョン表示。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。
// スクロールはコード構築の uGUI ScrollRect。コンテンツの高さも「ビューポート比率の倍率」
// (アンカーを 0..1 の外へ広げる) で表現し、固定 px を一切使わない。

using UnityEngine;
using UnityEngine.UI;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// 設定画面。SwiftUI 版 SettingsView の ScrollView + GameCard 構成を
    /// ScrollRect + カードパネルのスタックで再現する。
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Settings;

        // URL は docs/appstore-metadata.md §9 の値 (App Store Connect 登録済み URL) を正とする
        private const string PrivacyPolicyUrl = "https://kamui00002.github.io/escape-nine-endless/privacy-policy.html";
        private const string SupportUrl = "https://github.com/kamui00002/escape-nine-endless";

        // スクロールコンテンツの総高さ = ビューポートの何倍か。
        // カード合計 1.63 (FxCard 0.14 追加後) + 間隔 0.175 = 1.805 を収める (全カード比率の合計と連動して調整すること)。
        private const float ContentHeightRatio = 1.87f;

        /// <summary>カード間の縦間隔 (ビューポート高さ比)。Swift: VStack(spacing: 24) 相当。</summary>
        private const float SectionGap = 0.025f;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築を防ぐガード (HomeScreen と同じ対策)。
        private bool _built;

        // カードを上から詰むためのカーソル (コンテンツ空間 0..1、1 が上端)
        private float _cursor;

        // ---- OnShow のたびに更新する動的要素への参照 ----
        private Text _floorValueLabel;       // 最高到達階層
        private Text _characterValueLabel;   // 選択キャラクター名
        private Image _oneTapBg;             // ワンタップリトライのトグル背景
        private Text _oneTapLabel;
        private Image _hapticsBg;            // 触覚フィードバックのトグル背景
        private Text _hapticsLabel;
        private Image _reduceMotionBg;       // 視覚効果を減らす (Phase 4 juice) のトグル背景
        private Text _reduceMotionLabel;
        private Slider _bgmSlider;
        private Text _bgmPercentLabel;
        private Slider _sfxSlider;
        private Text _sfxPercentLabel;
        private GameObject _adPurchasedBadge; // 広告削除: 購入済みバッジ
        private GameObject _adBuyButton;      // 広告削除: 購入ボタン (Phase 3 スタブ)
        private Text _purchaseStatusLabel;    // 課金スタブの案内表示
        private ScrollRect _scroll;
        private RectTransform _content;

        /// <summary>AI 難易度のセグメントボタン 1 個分 (選択状態の色替えに使う参照セット)。</summary>
        private struct AiSegment
        {
            public AILevel Level;
            public Image Bg;
            public Text Label;
        }

        private AiSegment[] _aiSegments;

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

            // 背景はノッチ下まで全面 (Swift: GameBackground)
            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildScrollBody(safe);
        }

        public override void OnShow(object payload)
        {
            // Swift: @StateObject が push のたびに作り直されて最新値を読むのに相当。
            // GameController / 他画面が更新した PlayerState (同一インスタンス) を再描画する。
            RefreshDynamic();

            // スタブ案内は開くたびにリセット
            if (_purchaseStatusLabel != null) _purchaseStatusLabel.text = "";

            // 常に先頭から表示 (Swift の ScrollView も push のたびに先頭から)
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            if (_content != null) _content.anchoredPosition = Vector2.zero;
        }

        // MARK: - Header (Swift: GameHeader(title: "設定") + dismiss)

        private void BuildHeader(RectTransform parent)
        {
            var back = UIFactory.TextButton(parent, "BackButton", "← 戻る", 36,
                UITheme.BackgroundSecondary, UITheme.TextColor, OnBackTapped);
            UIFactory.Place((RectTransform)back.transform, 0.12f, 0.955f, 0.18f, 0.045f);

            var title = UIFactory.Label(parent, "TitleLabel", "設定", 64, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.955f, 0.5f, 0.05f);
        }

        /// <summary>戻る = Home へ (Swift: dismiss()。設定は Home からしか開かないため固定で良い)。</summary>
        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - Scroll Body (Swift: ScrollView { VStack(spacing: 24) { ...sections } })

        private void BuildScrollBody(RectTransform parent)
        {
            _scroll = BuildScrollView(parent, "SettingsScroll", out _content);
            UIFactory.Place((RectTransform)_scroll.transform, 0.5f, 0.455f, 1f, 0.91f);
            SetContentHeight(_content, ContentHeightRatio);

            _cursor = 1f;
            BuildPlayerInfoCard();
            BuildGameplayCard();
            BuildFxCard();
            BuildSoundCard();
            BuildTutorialCard();
            BuildAboutCard();
            BuildPurchaseCard();
        }

        // MARK: - Card 1: 冒険者情報 (Swift: playerInfoSection)

        private void BuildPlayerInfoCard()
        {
            var card = AddCard("PlayerInfoCard", "冒険者情報", 0.17f);

            CreateRowCaption(card, "FloorCaption", "最高到達階層", 0.58f);
            _floorValueLabel = CreateRowValue(card, "FloorValue", "0", 0.58f, UITheme.Available);

            CreateDivider(card, 0.40f);

            CreateRowCaption(card, "CharacterCaption", "選択キャラクター", 0.22f);
            _characterValueLabel = CreateRowValue(card, "CharacterValue", "", 0.22f, UITheme.GoldText);
        }

        // MARK: - Card 2: ゲームプレイ (Swift: gameplaySection + タスク要件の AI 難易度)

        private void BuildGameplayCard()
        {
            var card = AddCard("GameplayCard", "ゲームプレイ", 0.34f);

            // ワンタップリトライ (Swift: @AppStorage "oneTapRetryEnabled")
            BuildToggleRow(card, "OneTap",
                "ワンタップリトライ",
                "Game Over 後、画面のどこをタップしても即再挑戦します",
                0.76f, ToggleOneTapRetry, out _oneTapBg, out _oneTapLabel);

            CreateDivider(card, 0.60f);

            // 触覚フィードバック (Swift: @AppStorage HapticsHelper.storageKey)
            // 実際の振動制御 (HapticsHelper 相当) は Phase 4/juice 送り。永続化フラグのみ先行移植。
            BuildToggleRow(card, "Haptics",
                "触覚フィードバック",
                "ビート・タップ・衝突時に振動します (振動実装は Phase 4)",
                0.52f, ToggleHaptics, out _hapticsBg, out _hapticsLabel);

            CreateDivider(card, 0.36f);

            // AI 難易度 (Swift ではゲーム開始フローの選択。Unity ではタスク要件により設定へ集約。
            // HomeScreen の DangerZone (DEBUG) と同じく PlayerState.SelectedAILevel を正とする)
            var caption = UIFactory.Label(card, "AiCaption", "AI難易度", 36,
                UITheme.GoldText, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)caption.transform, 0.30f, 0.27f, 0.52f, 0.10f);

            // Boss は 10 の倍数階の内部専用難易度のためプレイヤー選択肢から除外 (Swift も 3 種)
            _aiSegments = new AiSegment[3];
            _aiSegments[0] = BuildAiSegment(card, "AiEasy", "Easy", AILevel.Easy, 0.21f);
            _aiSegments[1] = BuildAiSegment(card, "AiNormal", "Normal", AILevel.Normal, 0.50f);
            _aiSegments[2] = BuildAiSegment(card, "AiHard", "Hard", AILevel.Hard, 0.79f);
        }

        private AiSegment BuildAiSegment(RectTransform card, string name, string label, AILevel level, float cx)
        {
            var btn = UIFactory.TextButton(card, name, label, 34,
                UITheme.Background, UITheme.TextColor, () => SelectAiLevel(level));
            UIFactory.Place((RectTransform)btn.transform, cx, 0.12f, 0.26f, 0.14f);
            return new AiSegment
            {
                Level = level,
                Bg = btn.GetComponent<Image>(),
                Label = btn.GetComponentInChildren<Text>()
            };
        }

        // MARK: - Card 2.5: 演出設定 (Phase 4 juice。Swift 正本に対応なし、Unity 独自の追加設定)

        private void BuildFxCard()
        {
            var card = AddCard("FxCard", "演出設定", 0.14f);

            BuildToggleRow(card, "ReduceMotion",
                "視覚効果を減らす",
                "パンチ・シェイク・破片・ビート脈動などの演出を抑えます",
                0.40f, ToggleReduceMotion, out _reduceMotionBg, out _reduceMotionLabel);
        }

        // MARK: - Card 3: サウンド設定 (Swift: soundSection)

        private void BuildSoundCard()
        {
            var card = AddCard("SoundCard", "サウンド設定", 0.26f);

            CreateRowCaption(card, "BgmCaption", "BGM", 0.70f);
            _bgmPercentLabel = CreateRowValue(card, "BgmPercent", "0%", 0.70f, UITheme.Available);
            _bgmSlider = BuildVolumeSlider(card, "BgmSlider", 0.55f, OnBgmVolumeChanged);

            CreateDivider(card, 0.44f);

            CreateRowCaption(card, "SfxCaption", "効果音", 0.34f);
            _sfxPercentLabel = CreateRowValue(card, "SfxPercent", "0%", 0.34f, UITheme.Available);
            _sfxSlider = BuildVolumeSlider(card, "SfxSlider", 0.19f, OnSfxVolumeChanged);
        }

        // MARK: - Card 4: チュートリアル再表示 (タスク要件。Swift では HomeView の「遊び方」相当)

        private void BuildTutorialCard()
        {
            var card = AddCard("TutorialCard", "チュートリアル", 0.14f);

            var btn = UIFactory.TextButton(card, "ShowTutorialButton", "チュートリアルをもう一度見る", 38,
                UITheme.Main, UITheme.Background, () =>
                {
                    App.I.Audio.PlaySfx("button_tap");
                    App.I.Router.Show(ScreenId.Tutorial);
                });
            UIFactory.Place((RectTransform)btn.transform, 0.5f, 0.36f, 0.88f, 0.44f);
        }

        // MARK: - Card 5: アプリについて (Swift: aboutSection + タスク要件の URL/バージョン)

        private void BuildAboutCard()
        {
            var card = AddCard("AboutCard", "アプリについて", 0.32f);

            var appName = UIFactory.Label(card, "AppName", "Escape Nine: Endless", 38,
                UITheme.GoldText, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)appName.transform, 0.5f, 0.80f, 0.88f, 0.10f);

            // Swift は "バージョン 1.0.0" 固定文字列だった。Unity では Application.version
            // (PlayerSettings の bundleVersion) を表示して二重管理をなくす。
            var version = UIFactory.Label(card, "VersionLabel", "バージョン " + Application.version, 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)version.transform, 0.5f, 0.71f, 0.88f, 0.08f);

            var desc = UIFactory.Label(card, "DescLabel",
                "リズムに合わせてダンジョンを攻略する\nエンドレスチャレンジゲーム", 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)desc.transform, 0.5f, 0.57f, 0.88f, 0.16f);

            CreateDivider(card, 0.47f);

            var privacy = UIFactory.TextButton(card, "PrivacyButton", "プライバシーポリシー", 34,
                UITheme.Background, UITheme.TextColor, () => OpenUrl(PrivacyPolicyUrl));
            UIFactory.Place((RectTransform)privacy.transform, 0.5f, 0.35f, 0.88f, 0.14f);

            var support = UIFactory.TextButton(card, "SupportButton", "サポート (GitHub)", 34,
                UITheme.Background, UITheme.TextColor, () => OpenUrl(SupportUrl));
            UIFactory.Place((RectTransform)support.transform, 0.5f, 0.16f, 0.88f, 0.14f);
        }

        private void OpenUrl(string url)
        {
            App.I.Audio.PlaySfx("button_tap");
            Application.OpenURL(url);
        }

        // MARK: - Card 6: 課金設定 (Swift: purchaseSection。決済本体は Phase 3 スタブ)

        private void BuildPurchaseCard()
        {
            var card = AddCard("PurchaseCard", "課金設定", 0.26f);

            var adTitle = UIFactory.Label(card, "AdRemoveTitle", "広告削除", 36,
                UITheme.GoldText, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)adTitle.transform, 0.30f, 0.68f, 0.52f, 0.12f);

            var adDesc = UIFactory.Label(card, "AdRemoveDesc", "すべての広告を非表示にします", 28,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)adDesc.transform, 0.32f, 0.57f, 0.56f, 0.10f);

            // 購入済みバッジ (Swift: purchaseManager.isAdRemoved のとき表示)
            var badge = UIFactory.Panel(card, "AdPurchasedBadge", UITheme.WithAlpha(UITheme.Available, 0.2f));
            UIFactory.Place(badge, 0.84f, 0.63f, 0.24f, 0.16f);
            var badgeLabel = UIFactory.Label(badge, "Label", "購入済み", 30, UITheme.Available);
            UIFactory.Place((RectTransform)badgeLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _adPurchasedBadge = badge.gameObject;

            // 購入ボタン。Swift は StoreKit から取得した実価格 (adRemovalPrice) を表示するが、
            // 価格のハードコードは誤表示リスクがあるため IAP 導入 (Phase 3) まで「購入する」表記。
            var buy = UIFactory.TextButton(card, "AdBuyButton", "購入する", 32,
                UITheme.Available, UITheme.Background, OnBuyAdRemovalTapped);
            UIFactory.Place((RectTransform)buy.transform, 0.84f, 0.63f, 0.24f, 0.16f);
            _adBuyButton = buy.gameObject;

            CreateDivider(card, 0.46f);

            var restore = UIFactory.TextButton(card, "RestoreButton", "購入を復元", 34,
                UITheme.Background, UITheme.TextColor, OnRestorePurchasesTapped);
            UIFactory.Place((RectTransform)restore.transform, 0.5f, 0.30f, 0.88f, 0.18f);

            _purchaseStatusLabel = UIFactory.Label(card, "PurchaseStatus", "", 26,
                UITheme.WithAlpha(UITheme.Warning, 0.9f));
            UIFactory.Place((RectTransform)_purchaseStatusLabel.transform, 0.5f, 0.09f, 0.9f, 0.10f);
        }

        /// <summary>広告削除の購入 (Phase 3 スタブ)。Swift: purchaseManager.purchaseAdRemoval()</summary>
        private void OnBuyAdRemovalTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            // TODO(Phase 3): Unity IAP で ProductRemoveAds を決済し、成功時に
            //                PlayerState.AddPurchasedProduct(PlayerState.ProductRemoveAds) を呼ぶ。
            _purchaseStatusLabel.text = "課金は Phase 3 (IAP 導入) で対応予定です";
            Debug.Log("[SettingsScreen] 広告削除の購入は Phase 3 (IAP) で実装予定");
        }

        /// <summary>購入の復元 (Phase 3 スタブ)。Swift: purchaseManager.restorePurchases()</summary>
        private void OnRestorePurchasesTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            // TODO(Phase 3): Unity IAP の RestoreTransactions → PlayerState.AddPurchasedProduct 反映。
            _purchaseStatusLabel.text = "購入の復元は Phase 3 (IAP 導入) で対応予定です";
            Debug.Log("[SettingsScreen] 購入の復元は Phase 3 (IAP) で実装予定");
        }

        // MARK: - イベントハンドラ (トグル / セグメント / スライダー)

        private void ToggleOneTapRetry()
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            player.OneTapRetryEnabled = !player.OneTapRetryEnabled;
            player.Save(); // Swift: @AppStorage は即時永続化されるため、こちらも即 Save
            RefreshDynamic();
        }

        private void ToggleHaptics()
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            player.HapticsEnabled = !player.HapticsEnabled;
            player.Save();
            RefreshDynamic();
        }

        private void ToggleReduceMotion()
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            player.ReduceMotionEnabled = !player.ReduceMotionEnabled;
            player.Save();
            RefreshDynamic();
        }

        private void SelectAiLevel(AILevel level)
        {
            App.I.Audio.PlaySfx("button_tap");
            var player = App.I.Player;
            player.SelectedAILevel = level;
            player.Save();
            RefreshDynamic();
        }

        /// <summary>
        /// BGM スライダー変更。AudioDirector のプロパティ経由で「再生音量への即時反映 + 永続化」
        /// が一括で行われる (契約: get/set → PlayerState 永続化)。
        /// Swift はドラッグ終了時のみ saveData() だが、Unity は変更のたびに Save が走る
        /// (小データの PlayerPrefs 書込のため許容。気になる場合は Phase 4 でデバウンス)。
        /// </summary>
        private void OnBgmVolumeChanged(float value)
        {
            App.I.Audio.BgmVolume = value;
            if (_bgmPercentLabel != null) _bgmPercentLabel.text = ToPercent(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            App.I.Audio.SfxVolume = value;
            if (_sfxPercentLabel != null) _sfxPercentLabel.text = ToPercent(value);
        }

        private static string ToPercent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        // MARK: - 動的更新 (Swift の @Published バインディング相当を OnShow / 操作後に一括再描画)

        private void RefreshDynamic()
        {
            var player = App.I.Player;
            var audio = App.I.Audio;

            if (_floorValueLabel != null) _floorValueLabel.text = player.HighestFloor.ToString();
            if (_characterValueLabel != null) _characterValueLabel.text = player.SelectedCharacter.Name();

            ApplyToggleVisual(_oneTapBg, _oneTapLabel, player.OneTapRetryEnabled);
            ApplyToggleVisual(_hapticsBg, _hapticsLabel, player.HapticsEnabled);
            ApplyToggleVisual(_reduceMotionBg, _reduceMotionLabel, player.ReduceMotionEnabled);

            if (_aiSegments != null)
            {
                foreach (var seg in _aiSegments)
                {
                    bool selected = seg.Level == player.SelectedAILevel;
                    if (seg.Bg != null) seg.Bg.color = selected ? UITheme.Available : UITheme.Background;
                    if (seg.Label != null)
                    {
                        seg.Label.color = selected
                            ? UITheme.Background
                            : UITheme.WithAlpha(UITheme.TextColor, 0.85f);
                    }
                }
            }

            // スライダーは通知なしで反映 (通知経由だと Save が無駄に走るため)
            if (_bgmSlider != null)
            {
                _bgmSlider.SetValueWithoutNotify(audio.BgmVolume);
                _bgmPercentLabel.text = ToPercent(audio.BgmVolume);
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
                _sfxPercentLabel.text = ToPercent(audio.SfxVolume);
            }

            bool removed = player.AdRemoved;
            if (_adPurchasedBadge != null) _adPurchasedBadge.SetActive(removed);
            if (_adBuyButton != null) _adBuyButton.SetActive(!removed);
        }

        /// <summary>トグルの見た目 (Swift の Toggle tint: available を ON/OFF ボタンで代替)。</summary>
        private static void ApplyToggleVisual(Image bg, Text label, bool isOn)
        {
            if (bg != null) bg.color = isOn ? UITheme.Available : UITheme.Background;
            if (label != null)
            {
                label.text = isOn ? "ON" : "OFF";
                label.color = isOn ? UITheme.Background : UITheme.WithAlpha(UITheme.TextColor, 0.5f);
            }
        }

        // MARK: - カード / 行 部品 (Swift: GameCard + Divider の簡略移植)

        /// <summary>
        /// カードを上から順に積む。heightVp はビューポート高さ比 (コンテンツ空間へは
        /// ContentHeightRatio で割って換算)。合計が ContentHeightRatio を超えないこと。
        /// </summary>
        private RectTransform AddCard(string name, string title, float heightVp)
        {
            _cursor -= SectionGap / ContentHeightRatio;
            float h = heightVp / ContentHeightRatio;

            var card = UIFactory.Panel(_content, name, UITheme.WithAlpha(UITheme.BackgroundSecondary, 0.92f));
            UIFactory.Place(card, 0.5f, _cursor - h * 0.5f, 0.92f, h);
            _cursor -= h;

            // 枠線 (Swift: GameCard の gridBorder ストローク)。角丸は uGUI 標準では不可のため直線 4 辺。
            var border = UITheme.WithAlpha(UITheme.GridBorder, 0.5f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderT", border).transform, 0.5f, 1f, 1f, 0.015f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderB", border).transform, 0.5f, 0f, 1f, 0.015f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderL", border).transform, 0f, 0.5f, 0.006f, 1f);
            UIFactory.Place((RectTransform)UIFactory.ColorRect(card, "BorderR", border).transform, 1f, 0.5f, 0.006f, 1f);

            var t = UIFactory.Label(card, "CardTitle", title, 38, UITheme.GoldText,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)t.transform, 0.5f, 0.90f, 0.88f, 0.14f);

            return card;
        }

        /// <summary>行の左キャプション (Swift: fantasyCaption / text 0.7)。</summary>
        private static void CreateRowCaption(RectTransform card, string name, string text, float cy)
        {
            var t = UIFactory.Label(card, name, text, 32,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)t.transform, 0.30f, cy, 0.52f, 0.14f);
        }

        /// <summary>行の右値 (Swift: fantasyNumber / available 等)。</summary>
        private static Text CreateRowValue(RectTransform card, string name, string text, float cy, Color color)
        {
            var t = UIFactory.Label(card, name, text, 36, color, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place((RectTransform)t.transform, 0.76f, cy, 0.40f, 0.14f);
            return t;
        }

        /// <summary>区切り線 (Swift: Divider().background(gridBorder 0.3))。</summary>
        private static void CreateDivider(RectTransform card, float cy)
        {
            var line = UIFactory.ColorRect(card, "Divider", UITheme.WithAlpha(UITheme.GridBorder, 0.3f));
            UIFactory.Place((RectTransform)line.transform, 0.5f, cy, 0.88f, 0.008f);
        }

        /// <summary>
        /// タイトル + 説明 + ON/OFF ボタンのトグル行。
        /// Swift の Toggle (スイッチ) は uGUI Toggle の視覚部品構築コストが高いため、
        /// HomeScreen DangerZone と同じ「状態表示ボタン」方式に簡略化 (意図的差分)。
        /// </summary>
        private void BuildToggleRow(RectTransform card, string name, string title, string desc,
            float rowCy, System.Action onTap, out Image toggleBg, out Text toggleLabel)
        {
            var t = UIFactory.Label(card, name + "Title", title, 36, UITheme.GoldText, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)t.transform, 0.30f, rowCy, 0.52f, 0.10f);

            var d = UIFactory.Label(card, name + "Desc", desc, 26,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)d.transform, 0.32f, rowCy - 0.095f, 0.56f, 0.10f);

            var btn = UIFactory.TextButton(card, name + "Toggle", "", 32,
                UITheme.Background, UITheme.TextColor, onTap);
            UIFactory.Place((RectTransform)btn.transform, 0.84f, rowCy - 0.025f, 0.22f, 0.12f);

            toggleBg = btn.GetComponent<Image>();
            toggleLabel = btn.GetComponentInChildren<Text>();
        }

        // MARK: - uGUI Slider のコード構築 (Swift: Slider(value:in:) tint available)

        /// <summary>
        /// カード内に水平スライダーを構築する。トラック/フィルはアンカー比率のみで構成。
        /// ハンドル幅の 40 は「参照解像度 (1170x2532) 上の px」= CanvasScaler が画面比で
        /// スケールするため実質比率単位 (Slider がハンドル位置をアンカーで駆動する仕様上、
        /// 幅だけは sizeDelta でしか与えられない)。
        /// </summary>
        private Slider BuildVolumeSlider(RectTransform card, string name, float cy,
            UnityEngine.Events.UnityAction<float> onChanged)
        {
            // ルートは透明 Image 付き (raycastTarget=true) = トラック上のどこをタップしても
            // Slider がドラッグ/ジャンプを受けられるようにする
            var root = UIFactory.Panel(card, name, Color.clear);
            UIFactory.Place(root, 0.5f, cy, 0.88f, 0.12f);

            // トラック (背景バー)
            var track = UIFactory.ColorRect(root, "Track", UITheme.WithAlpha(UITheme.Background, 0.95f));
            UIFactory.Place((RectTransform)track.transform, 0.5f, 0.5f, 1f, 0.35f);

            // フィル (Slider が anchorMax.x を value で駆動する)
            var fillArea = UIFactory.Panel(root, "FillArea");
            UIFactory.Place(fillArea, 0.5f, 0.5f, 1f, 0.35f);
            var fill = UIFactory.ColorRect(fillArea, "Fill", UITheme.Available);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f); // x は Slider が上書きする
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // ハンドル (ゴールドのつまみ)
            var handleArea = UIFactory.Panel(root, "HandleArea");
            UIFactory.Place(handleArea, 0.5f, 0.5f, 1f, 1f);
            var handle = UIFactory.ColorRect(handleArea, "Handle", UITheme.GoldText);
            handle.raycastTarget = true; // ColorRect は false 固定のため、つまみだけ有効化
            var handleRt = (RectTransform)handle.transform;
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f); // x は Slider が上書きする
            handleRt.sizeDelta = new Vector2(40f, 0f);
            handleRt.anchoredPosition = Vector2.zero;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        // MARK: - ScrollRect のコード構築 (Swift: ScrollView 相当)

        /// <summary>
        /// 縦スクロールビューを構築する。コンテンツの高さは SetContentHeight で
        /// 「ビューポートの何倍か」をアンカーで与える (固定 px を使わない)。
        /// </summary>
        private static ScrollRect BuildScrollView(RectTransform parent, string name, out RectTransform content)
        {
            // ルートに透明 Image (raycastTarget=true) = 余白部分でもドラッグを受ける
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

        /// <summary>
        /// コンテンツの縦アンカーをビューポートの k 倍に広げる (上端揃え・固定 px なし)。
        /// ScrollRect は anchoredPosition を動かすだけなのでアンカー駆動のサイズと共存できる。
        /// </summary>
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
