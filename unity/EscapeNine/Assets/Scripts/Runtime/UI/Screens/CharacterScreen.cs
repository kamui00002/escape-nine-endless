// CharacterScreen.cs
// Swift 正本: Views/Character/CharacterSelectionView.swift (CharacterCardView 含む)
//             + Views/Components/GameHeader.swift (戻る + タイトルのヘッダー)
//             + Views/Components/GameCard.swift (選択中 = ゴールド枠のカード装飾)
//
// 5 キャラ (hero/thief/wizard/elf/knight) のカードを縦スクロールで並べる。
// キャラ名・スキル名・説明・使用回数は EscapeNine.Core の Character/Skill から取得し、
// 値 (回数・解放階層・価格) をこのファイルに複製しない (GameConfig 一元管理ルール)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// キャラクター選択画面。SwiftUI 版の ScrollView + CharacterCardView 列を
    /// uGUI の ScrollRect + 比率配置カードに置き換える。
    /// 購入ボタンは Phase 3 (Unity IAP) までスタブ動作 (Editor のみ即時解放)。
    /// </summary>
    public sealed class CharacterScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.CharacterSelect;

        // App.Awake → Router.Register の双方から BuildUI が呼ばれるため二重構築を防ぐ
        // (HomeScreen と同じガード)。
        private bool _built;

        private const int CardCount = 5;

        /// <summary>
        /// スクロールコンテンツの全高 (ビューポート何個分か)。
        /// 5 枚のカードが 1 画面に約 2.2 枚見える比率 = Swift 版 ScrollView の見え方に合わせた値。
        /// </summary>
        private const float ContentHeightRatio = 2.3f;

        private ScrollRect _scroll;
        private RectTransform _content;

        private readonly List<CardWidgets> _cards = new List<CardWidgets>();

        // トースト (購入スタブの案内用。ResultScreen と同じ方式)
        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;

        /// <summary>
        /// カード 1 枚分の動的 UI 参照。解放/選択状態が変わるたび RefreshCards() で書き換える
        /// (SwiftUI の @Published バインディング相当を明示的な再描画で代替)。
        /// </summary>
        private sealed class CardWidgets
        {
            public CharacterType Type;
            public Image Border;            // GameCard の stroke: 選択中 = ゴールド / 通常 = gridBorder
            public GameObject SelectedBadge; // 「選択中」バッジ
            public TextMeshProUGUI PriceLabel;         // 未解放の有料キャラのみ表示 (¥240)
            public Image Sprite;            // 未解放時は alpha 0.4 に落とす
            public GameObject LockOverlay;  // 黒半透明 + 「未解放」
            public Button ActionButton;
            public Image ActionBg;
            public TextMeshProUGUI ActionLabel;
            public TextMeshProUGUI ThiefProgress;      // 盗賊のみ: 「現在: X階層 / 必要: 10階層」
        }

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return;
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

            // 背景はノッチ下まで全面 (Swift: Color(hex: background).ignoresSafeArea())。
            // HD-2D (2026-07-07): 単色から縦グラデ+下端ヴィネットの軽量版へ。
            UIFactory.SimpleDepthBackground(transform);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildScrollArea(safe);
            BuildCards();
            BuildToast(safe);
        }

        // MARK: - Header (Swift: GameHeader(title: "キャラクター選択"))

        private void BuildHeader(RectTransform parent)
        {
            // Swift はグラデーション (background → backgroundSecondary) の帯。
            // グラデーション描画は Phase 4/juice 送りとし、単色 backgroundSecondary で代替。
            var band = UIFactory.ColorRect(parent, "HeaderBand", UITheme.BackgroundSecondary);
            UIFactory.Place((RectTransform)band.transform, 0.5f, 0.965f, 1f, 0.07f);

            // 戻るボタン (Swift: chevron.left + 戻る → SF Symbol は使えないため "<" テキストで代替)。
            // Swift の dismiss() は NavigationStack の pop = Home へ戻ることと等価。
            // cx は他画面 (0.12) より左寄せ (2026-07-04 重なり監査で検出): このタイトル
            // 「キャラクター選択」は他画面より幅広 (0.6) のため、既定の cx=0.12/幅 0.18 だと
            // 右端 (0.21) がタイトル左端 (0.5-0.3=0.2) に食い込む。幅は変えず (テキスト折返し回避)
            // 位置だけ左へ振る。
            UIFactory.SecondaryButton(parent, "BackButton", "< 戻る", 0.10f, 0.965f, 0.18f, 0.042f,
                OnBackTapped, 34);

            var title = UIFactory.Label(parent, "TitleLabel", "キャラクター選択", 50, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.965f, 0.6f, 0.05f);
        }

        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - ScrollArea (Swift: ScrollView)

        private void BuildScrollArea(RectTransform parent)
        {
            // ドラッグを受けるため透明 Image 付き Panel にする
            // (uGUI の raycast は alpha=0 でも当たるので、見た目に影響せずスクロール操作だけ拾える)。
            var area = UIFactory.Panel(parent, "ScrollArea", Color.clear);
            UIFactory.Place(area, 0.5f, 0.465f, 1f, 0.93f);

            // はみ出したカードをヘッダー/画面外でクリップ
            area.gameObject.AddComponent<RectMask2D>();

            _scroll = area.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            // SwiftUI ScrollView の端バウンスに寄せる (uGUI 標準の Elastic)
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.scrollSensitivity = 30f; // Editor のマウスホイール確認用

            // コンテンツ: アンカーを 0..1 の外側へ広げて「ビューポートの N 倍」の高さを比率だけで確保する。
            // sizeDelta (固定 px) を使わないための工夫で、offset は常に 0 のまま。
            // ScrollRect が動かすのは anchoredPosition のみなので比率レイアウト原則と両立する。
            _content = UIFactory.Panel(area, "Content");
            _content.pivot = new Vector2(0.5f, 1f); // 上端基準 = 先頭カードから表示
            _content.anchorMin = new Vector2(0f, 1f - ContentHeightRatio);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            _scroll.viewport = area;
            _scroll.content = _content;
        }

        // MARK: - Cards (Swift: ForEach(CharacterType.allCases) { CharacterCardView })

        private void BuildCards()
        {
            // 並び順は Swift の CharacterType.allCases と同じ (enum 定義順)
            var types = new[]
            {
                CharacterType.Hero, CharacterType.Thief, CharacterType.Wizard,
                CharacterType.Elf, CharacterType.Knight
            };

            float slot = 1f / CardCount; // コンテンツ内でカード 1 枚が占める縦割合
            for (int i = 0; i < types.Length; i++)
            {
                float cy = 1f - (i + 0.5f) * slot; // 上から順に配置
                _cards.Add(BuildCard(_content, types[i], cy, slot * 0.95f));
            }
        }

        private CardWidgets BuildCard(RectTransform parent, CharacterType type, float cy, float h)
        {
            // キャラ定義は Core から取得 (名前・スキル・回数・価格の値を UI 側に複製しない)
            var character = Character.GetCharacter(type);
            var skill = character.Skill;

            var w = new CardWidgets { Type = type };

            // 枠 (GameCard の stroke): 外側パネル = 枠色 / 内側パネル = カード地、の 2 枚重ねで表現
            // (uGUI 標準機能だけで角丸なしの枠線を作る定番手法)
            var border = UIFactory.Panel(parent, $"Card_{type}", UITheme.WithAlpha(UITheme.GridBorder, 0.5f));
            UIFactory.Place(border, 0.5f, cy, 0.92f, h);
            w.Border = border.GetComponent<Image>();

            // HD-2D (2026-07-07): 塗りを PanelFillTop/Bottom の縦グラデに。border 側の選択色反転
            // トリック (RefreshCards が w.Border.color を書き換え) はそのまま維持するため触らない。
            var cardImg = UIFactory.FillImage(border, "Body",
                UIFactory.VerticalGradientSprite(UITheme.PanelFillTop, UITheme.PanelFillBottom, 64));
            var card = (RectTransform)cardImg.transform;
            UIFactory.Place(card, 0.5f, 0.5f, 0.992f, 0.988f);

            // --- ヘッダー行: 名前 + 選択中バッジ + (未解放時) 価格 ---
            var name = UIFactory.Label(card, "NameLabel", character.Name, 46, UITheme.TextColor,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)name.transform, 0.22f, 0.92f, 0.36f, 0.09f);

            var badge = UIFactory.ColorRect(card, "SelectedBadge", UITheme.WithAlpha(UITheme.Available, 0.2f));
            UIFactory.Place((RectTransform)badge.transform, 0.53f, 0.92f, 0.18f, 0.07f);
            var badgeLabel = UIFactory.Label(badge.transform, "Label", "選択中", 30, UITheme.Available);
            UIFactory.Place((RectTransform)badgeLabel.transform, 0.5f, 0.5f, 1f, 1f);
            w.SelectedBadge = badge.gameObject;

            if (!character.IsFree)
            {
                // 有料キャラ表示は解放状態に依らず常時 (Swift と同じ)
                var paid = UIFactory.Label(card, "PaidCaption", "有料キャラクター", 28, UITheme.Warning,
                    TextAnchor.MiddleLeft);
                UIFactory.Place((RectTransform)paid.transform, 0.22f, 0.84f, 0.36f, 0.06f);
            }

            // 価格 (Swift: characterType.price → Core 経由で GameConfig.PremiumCharacterPrice)。
            // 表示/非表示は RefreshCards が解放状態で切り替える。
            w.PriceLabel = UIFactory.Label(card, "PriceLabel",
                character.Price.HasValue ? "¥" + character.Price.Value : "", 42, UITheme.Available,
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place((RectTransform)w.PriceLabel.transform, 0.80f, 0.92f, 0.32f, 0.09f);

            // --- キャラ画像 (Swift: 円形グラデ背景 + Circle クリップ → 単色矩形に簡略化 = Phase 4/juice) ---
            var spriteBg = UIFactory.ColorRect(card, "SpriteBg", UITheme.WithAlpha(UITheme.Main, 0.15f));
            UIFactory.Place((RectTransform)spriteBg.transform, 0.5f, 0.63f, 0.30f, 0.27f);

            w.Sprite = UIFactory.SpriteImage(card, "Sprite", UIFactory.LoadSprite(character.SpriteName));
            w.Sprite.raycastTarget = false; // 飾り (敵タップ等の用途はゲーム画面のみ)
            UIFactory.Place((RectTransform)w.Sprite.transform, 0.5f, 0.63f, 0.27f, 0.24f);

            // ロックオーバーレイ (Swift: 黒 0.6 の円 + lock.fill。SF Symbol と絵文字は
            // legacy Text で描画できないためテキスト「未解放」で代替)
            var lockOverlay = UIFactory.ColorRect(card, "LockOverlay", UITheme.WithAlpha(Color.black, 0.6f));
            UIFactory.Place((RectTransform)lockOverlay.transform, 0.5f, 0.63f, 0.30f, 0.27f);
            var lockLabel = UIFactory.Label(lockOverlay.transform, "Label", "未解放", 36, UITheme.Warning,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)lockLabel.transform, 0.5f, 0.5f, 1f, 1f);
            w.LockOverlay = lockOverlay.gameObject;

            // --- スキル情報ボックス (Swift: background 0.5 の角丸ボックス) ---
            var box = UIFactory.ColorRect(card, "SkillBox", UITheme.WithAlpha(UITheme.Background, 0.5f));
            UIFactory.Place((RectTransform)box.transform, 0.5f, 0.35f, 0.92f, 0.24f);

            var skillName = UIFactory.Label(box.transform, "SkillName", $"スキル: {skill.Name}", 36,
                UITheme.GoldText, TextAnchor.MiddleLeft); // Swift: textSecondary = GoldText
            UIFactory.Place((RectTransform)skillName.transform, 0.5f, 0.80f, 0.94f, 0.30f);

            var skillDesc = UIFactory.Label(box.transform, "SkillDesc", skill.Description, 30,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)skillDesc.transform, 0.5f, 0.50f, 0.94f, 0.30f);

            var skillUsage = UIFactory.Label(box.transform, "SkillUsage", $"使用回数: {skill.MaxUsage}回", 28,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)skillUsage.transform, 0.5f, 0.20f, 0.94f, 0.30f);

            // --- アクションボタン (ラベル・色・活性は RefreshCards が状態に応じて差し替え) ---
            w.ActionButton = UIFactory.TextButton(card, "ActionButton", "", 40,
                UITheme.Background, UITheme.TextColor, () => OnCardActionTapped(type));
            UIFactory.Place((RectTransform)w.ActionButton.transform, 0.5f, 0.145f, 0.92f, 0.11f);
            // HD-2D (2026-07-07): 選択中/購入/解放条件の3状態で色を動的に塗り替える (RefreshCards) ため
            // Card 二重掛けはせず EmbossTrim のみ足す。
            UIFactory.EmbossTrim(w.ActionButton.transform, "ActionEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            w.ActionBg = w.ActionButton.GetComponent<Image>();
            w.ActionLabel = w.ActionButton.GetComponentInChildren<TextMeshProUGUI>();

            // 盗賊のみ: 解放条件の進捗表示 (Swift: 「現在: X階層 / 必要: 10階層」)
            if (type == CharacterType.Thief)
            {
                w.ThiefProgress = UIFactory.Label(card, "ThiefProgress", "", 26,
                    UITheme.WithAlpha(UITheme.TextColor, 0.7f));
                UIFactory.Place((RectTransform)w.ThiefProgress.transform, 0.5f, 0.045f, 0.92f, 0.06f);
            }

            return w;
        }

        // MARK: - トースト (購入スタブ案内用。ResultScreen と同じ実装パターン)

        private void BuildToast(RectTransform parent)
        {
            _toast = UIFactory.Panel(parent, "Toast", UITheme.WithAlpha(Color.black, 0.75f));
            UIFactory.Place(_toast, 0.5f, 0.10f, 0.8f, 0.05f);
            _toastLabel = UIFactory.Label(_toast, "Label", "", 30, UITheme.GoldText);
            UIFactory.Place((RectTransform)_toastLabel.transform, 0.5f, 0.5f, 1f, 1f);
            _toast.gameObject.SetActive(false);
        }

        private void ShowToast(string message)
        {
            _toastLabel.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toast.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            _toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private void HideToast()
        {
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
                _toastRoutine = null;
            }
            if (_toast != null) _toast.gameObject.SetActive(false);
        }

        // MARK: - ライフサイクル

        public override void OnShow(object payload)
        {
            // Swift: @StateObject PlayerViewModel() が表示のたび UserDefaults を読むのに対応。
            // (ゲーム中の盗賊自動解放 / Shop での購入を反映するため再読込必須)
            App.I.Player.Reload();
            RefreshCards();

            // SwiftUI の ScrollView は毎回先頭から表示されるため、スクロール位置を先頭へ戻す
            if (_scroll != null && _content != null)
            {
                _scroll.velocity = Vector2.zero;
                _content.anchoredPosition = Vector2.zero; // offset=0 生成時の初期値 = 先頭
            }
        }

        public override void OnHide()
        {
            HideToast();
        }

        // MARK: - 状態反映 (Swift の @Published バインディング相当を一括再描画)

        private void RefreshCards()
        {
            var player = App.I.Player;

            // Swift: #if DEBUG の debugUnlockAllCharacters。Unity では Editor/Development Build のみ有効
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool debugUnlocked = player.DebugUnlockAllCharacters;
#else
            bool debugUnlocked = false;
#endif

            foreach (var w in _cards)
            {
                bool isUnlocked = player.IsCharacterUnlocked(w.Type) || debugUnlocked;
                bool isSelected = player.SelectedCharacter == w.Type;

                // GameCard(isHighlighted:) の枠色: 選択中はゴールド強調
                w.Border.color = isSelected
                    ? UITheme.Available
                    : UITheme.WithAlpha(UITheme.GridBorder, 0.5f);

                w.SelectedBadge.SetActive(isSelected);
                w.PriceLabel.gameObject.SetActive(!isUnlocked && w.Type.Price().HasValue);

                // 未解放はスプライトを暗く (Swift: .opacity(0.4))
                w.Sprite.color = isUnlocked ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                w.LockOverlay.SetActive(!isUnlocked);

                if (w.ThiefProgress != null)
                {
                    w.ThiefProgress.gameObject.SetActive(!isUnlocked);
                    if (!isUnlocked)
                    {
                        w.ThiefProgress.text =
                            $"現在: {player.HighestFloor}階層 / 必要: {GameConfig.ThiefUnlockFloor}階層";
                    }
                }

                // アクションボタンの 3 状態 (Swift: actionButton の @ViewBuilder 分岐)
                if (isUnlocked)
                {
                    w.ActionLabel.text = isSelected ? "選択中" : "選択する";
                    w.ActionBg.color = isSelected ? UITheme.Main : UITheme.Background; // primary / secondary
                    w.ActionLabel.color = isSelected ? UITheme.Background : UITheme.TextColor;
                    w.ActionButton.interactable = true;
                }
                else if (w.Type == CharacterType.Thief)
                {
                    // 解放階層は GameConfig を正とする (文言の「10」を複製しない)
                    w.ActionLabel.text = $"{GameConfig.ThiefUnlockFloor}階層クリアで解放";
                    w.ActionBg.color = UITheme.WithAlpha(UITheme.Warning, 0.6f);
                    w.ActionLabel.color = Color.white;
                    w.ActionButton.interactable = false; // Swift: .disabled(true)
                }
                else
                {
                    w.ActionLabel.text = "購入する";
                    w.ActionBg.color = UITheme.Warning; // Swift: GameButton style .danger
                    w.ActionLabel.color = Color.white;
                    w.ActionButton.interactable = true;
                }
            }
        }

        // MARK: - 操作ハンドラ

        private void OnCardActionTapped(CharacterType type)
        {
            App.I.Audio.PlaySfx("button_tap"); // Swift: onSelect/onPurchase 冒頭の buttonTap
            var player = App.I.Player;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool debugUnlocked = player.DebugUnlockAllCharacters;
#else
            bool debugUnlocked = false;
#endif

            if (player.IsCharacterUnlocked(type) || debugUnlocked)
            {
                // 選択 (Swift: playerViewModel.selectCharacter)。debug 全解放時も
                // PlayerState.SelectCharacter が内部で許可する。
                player.SelectCharacter(type);
                RefreshCards();
                return;
            }

            // 未解放でここに来るのは有料キャラのみ (盗賊は interactable=false で到達しない)
            PurchaseCharacterStub(type);
        }

        /// <summary>
        /// 購入スタブ。
        /// TODO(Phase 3): Unity IAP (StoreKit 相当) と接続する。
        ///   Swift 正本: PurchaseManager.purchaseCharacter → StoreKitService.purchase →
        ///   成功時に unlockCharacter + selectCharacter。決済検証・購入 UI(ローディング/アラート) も
        ///   Phase 3 で移植する (Swift: .purchaseAlert() / .purchaseLoadingOverlay())。
        /// </summary>
        private void PurchaseCharacterStub(CharacterType type)
        {
#if UNITY_EDITOR
            // Editor では即時解放して購入後フロー (解放 → 選択 → 表示更新) をデバッグ可能にする
            string productId = ProductIdFor(type);
            if (string.IsNullOrEmpty(productId)) return;
            App.I.Player.AddPurchasedProduct(productId); // キャラ解放も内部で行われる
            App.I.Player.SelectCharacter(type);
            RefreshCards();
            ShowToast("(デバッグ) 購入して選択しました");
#else
            // 実機では準備中トーストのみ (誤課金の期待を持たせないための明示メッセージ)
            ShowToast("購入機能は準備中です。アップデートをお待ちください");
#endif
        }

        /// <summary>有料キャラ → StoreKit 商品 ID の対応 (Swift: ProductID)。無料キャラは null。</summary>
        private static string ProductIdFor(CharacterType type)
        {
            switch (type)
            {
                case CharacterType.Wizard: return PlayerState.ProductWizard;
                case CharacterType.Elf: return PlayerState.ProductElf;
                case CharacterType.Knight: return PlayerState.ProductKnight;
                default: return null;
            }
        }
    }
}
