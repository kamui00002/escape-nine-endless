// ShopScreen.cs
// Swift 正本: Views/Shop/ShopView.swift (ShopItemCard 含む)
//
// 4 商品 (魔法使い / エルフ / ナイト / 広告削除) と「購入を復元」ボタンを表示する。
// キャラの説明文・回数は EscapeNine.Core の Character/Skill から組み立て、
// 価格は GameConfig.PremiumCharacterPrice を正とする (値の複製禁止)。
// 購入・復元は Phase 3 (Unity IAP) までスタブ動作 (Editor のみ即時反映)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。
// Swift 版は ScrollView だが、4 商品 + 復元ボタンは縦 1 画面に収まるため
// 静的配置に簡略化 (Home/Result と同方式。商品が増えたら CharacterScreen の ScrollRect 方式を移植)。

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    /// <summary>
    /// ショップ画面。SwiftUI 版 ShopView の ShopItemCard 列を比率配置の行カードに置き換える。
    /// 購入状態 (価格ボタン ↔ 購入済バッジ) は OnShow / 購入操作のたびに再描画する。
    /// </summary>
    public sealed class ShopScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Shop;

        // App.Awake → Router.Register の双方から BuildUI が呼ばれるため二重構築を防ぐ
        private bool _built;

        /// <summary>
        /// 広告削除の価格表記。Swift: StoreKitService.priceString(.removeAds) のフォールバック "¥480"。
        /// GameConfig にはキャラ価格 (PremiumCharacterPrice) しか定数が無く、Core は変更禁止のため
        /// ここにフォールバック表記のみ持つ。
        /// TODO(Phase 3): Unity IAP の localizedPrice (Swift: product.displayPrice 相当) に置き換える。
        /// </summary>
        private const string AdRemovalPriceText = "¥480";

        private readonly List<ItemWidgets> _items = new List<ItemWidgets>();

        // トースト (購入スタブ案内用。ResultScreen と同じ方式)
        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;

        /// <summary>商品行 1 つ分の動的 UI 参照。購入状態が変わるたび RefreshItems() で書き換える。</summary>
        private sealed class ItemWidgets
        {
            public string ProductId;
            public Image Border;             // 購入済 = success / 未購入 = gridBorder (Swift の stroke)
            public GameObject PriceButtonRoot;
            public GameObject PurchasedBadge;
        }

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return;
            _built = true;

            // 画面ルートを親いっぱいに固定
            var root = GetComponent<RectTransform>();
            if (root != null)
            {
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            UIFactory.SimpleDepthBackground(transform);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);

            // --- キャラクターセクション (Swift: Text("キャラクター") + ShopItemCard x3) ---
            var sectionCharacters = UIFactory.Label(safe, "SectionCharacters", "キャラクター", 40,
                UITheme.GoldText, TextAnchor.MiddleLeft, FontStyle.Bold); // Swift: textSecondary
            UIFactory.Place((RectTransform)sectionCharacters.transform, 0.5f, 0.875f, 0.92f, 0.035f);

            // Swift は絵文字アイコン (🔮/🏹/🛡️) だが legacy Text はカラー絵文字を描画できないため、
            // キャラ商品はドット絵スプライトをアイコンに使う (意図的差分)。
            BuildCharacterItem(safe, 0.795f, CharacterType.Wizard, PlayerState.ProductWizard);
            BuildCharacterItem(safe, 0.685f, CharacterType.Elf, PlayerState.ProductElf);
            BuildCharacterItem(safe, 0.575f, CharacterType.Knight, PlayerState.ProductKnight);

            // --- その他セクション (Swift: Text("その他") + 広告削除) ---
            var sectionOther = UIFactory.Label(safe, "SectionOther", "その他", 40,
                UITheme.GoldText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)sectionOther.transform, 0.5f, 0.50f, 0.92f, 0.035f);

            // 広告削除アイコンは 🚫 の代替としてテキスト "AD" (Warning 色) を使う
            BuildShopItem(safe, 0.42f, null, "AD", "広告削除",
                "すべての広告を非表示にします", AdRemovalPriceText, PlayerState.ProductRemoveAds);

            BuildRestoreButton(safe);
            BuildToast(safe);
        }

        // MARK: - Header (Swift: ShopView 独自ヘッダー = GameHeader と同構造)

        private void BuildHeader(RectTransform parent)
        {
            // グラデーション帯 (background → backgroundSecondary) は Phase 4/juice 送り → 単色で代替
            var band = UIFactory.ColorRect(parent, "HeaderBand", UITheme.BackgroundSecondary);
            UIFactory.Place((RectTransform)band.transform, 0.5f, 0.965f, 1f, 0.07f);

            // 戻る (Swift: chevron.left → SF Symbol 不可のため "<" テキスト。dismiss() = Home へ)
            // HD-2D (2026-07-07): Home のサブボタンと同じ質感 (ButtonFill 塗り + EmbossTrim) に統一。
            UIFactory.SecondaryButton(parent, "BackButton", "< 戻る", 0.12f, 0.965f, 0.18f, 0.042f,
                OnBackTapped, 34);

            var title = UIFactory.Label(parent, "TitleLabel", "ショップ", 50, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.965f, 0.5f, 0.05f);
        }

        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - 商品行 (Swift: ShopItemCard)

        /// <summary>
        /// キャラ商品行。タイトル・スキル説明・回数・価格を Core から組み立てる
        /// (Swift の ShopView は説明文に回数を直書きしているが、値の複製を避けるため Core を正とする)。
        /// </summary>
        private void BuildCharacterItem(RectTransform parent, float cy, CharacterType type, string productId)
        {
            var character = Character.GetCharacter(type);
            var skill = character.Skill;

            // 例: "透明化スキル: 鬼に当たっても無敵（7回）" — Swift の表記フォーマットを踏襲
            string description = $"{skill.Name}スキル: {skill.Description}（{skill.MaxUsage}回）";
            string price = "¥" + GameConfig.PremiumCharacterPrice; // Swift: priceString フォールバック "¥240"

            BuildShopItem(parent, cy, character.SpriteName, null, character.Name, description, price, productId);
        }

        /// <summary>
        /// 商品行の共通構築。spriteName があればスプライトアイコン、なければ iconText を表示。
        /// </summary>
        private void BuildShopItem(RectTransform parent, float cy, string spriteName, string iconText,
            string title, string description, string price, string productId)
        {
            var w = new ItemWidgets { ProductId = productId };

            // 枠 + 地の 2 枚重ね (Swift: RoundedRectangle fill + stroke)
            var border = UIFactory.Panel(parent, $"Item_{productId}", UITheme.WithAlpha(UITheme.GridBorder, 0.3f));
            UIFactory.Place(border, 0.5f, cy, 0.92f, 0.095f);
            w.Border = border.GetComponent<Image>();

            // HD-2D (2026-07-07): 塗りを PanelFillTop/Bottom の縦グラデに (旧 BackgroundSecondary の
            // フラット塗りだと計器/カードらしい立体感が無かった)。border 側の色反転トリック (购入済み/
            // 未購入で GridBorder⇔Success を切替、RefreshItems 参照) はそのまま維持するため触らない。
            var bodyImg = UIFactory.FillImage(border, "Body",
                UIFactory.VerticalGradientSprite(UITheme.PanelFillTop, UITheme.PanelFillBottom, 64));
            var body = (RectTransform)bodyImg.transform;
            UIFactory.Place(body, 0.5f, 0.5f, 0.994f, 0.97f);

            // --- アイコン (左端。Swift: 絵文字 36pt + background 0.5 の角丸ボックス) ---
            var iconBox = UIFactory.ColorRect(body, "IconBox", UITheme.WithAlpha(UITheme.Background, 0.5f));
            UIFactory.Place((RectTransform)iconBox.transform, 0.10f, 0.5f, 0.14f, 0.66f);

            if (!string.IsNullOrEmpty(spriteName))
            {
                var icon = UIFactory.SpriteImage(body, "Icon", UIFactory.LoadSprite(spriteName));
                icon.raycastTarget = false; // 飾り
                UIFactory.Place((RectTransform)icon.transform, 0.10f, 0.5f, 0.12f, 0.58f);
            }
            else
            {
                var icon = UIFactory.Label(iconBox.transform, "Icon", iconText, 44, UITheme.Warning,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                UIFactory.Place((RectTransform)icon.transform, 0.5f, 0.5f, 1f, 1f);
            }

            // --- タイトル / 説明 (中央左寄せ。説明は価格ボタンと重ならない幅に制限) ---
            var titleLabel = UIFactory.Label(body, "Title", title, 38, UITheme.TextColor,
                TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)titleLabel.transform, 0.44f, 0.70f, 0.50f, 0.30f);

            var descLabel = UIFactory.Label(body, "Description", description, 26,
                UITheme.WithAlpha(UITheme.TextColor, 0.7f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)descLabel.transform, 0.44f, 0.32f, 0.50f, 0.36f);

            // --- 右端: 価格ボタン ↔ 購入済バッジ (排他表示。切替は RefreshItems) ---
            // Swift はゴールドグラデ (available → main) + 白文字。グラデは Phase 4 送りで単色 available。
            var priceButton = UIFactory.TextButton(body, "PriceButton", price, 36,
                UITheme.Available, Color.white, () => OnPurchaseTapped(productId));
            UIFactory.Place((RectTransform)priceButton.transform, 0.86f, 0.5f, 0.20f, 0.52f);
            // HD-2D (2026-07-07): 既に body カード内部にあり二重の影は過剰なため Card 化はせず、
            // EmbossTrim のみで「彫られた木の看板」の質感を軽く足す (他ボタンとの統一感)。
            UIFactory.EmbossTrim(priceButton.transform, "PriceEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            w.PriceButtonRoot = priceButton.gameObject;

            var badge = UIFactory.ColorRect(body, "PurchasedBadge", UITheme.WithAlpha(UITheme.Success, 0.15f));
            UIFactory.Place((RectTransform)badge.transform, 0.86f, 0.5f, 0.20f, 0.52f);
            var badgeLabel = UIFactory.Label(badge.transform, "Label", "購入済", 32, UITheme.Success);
            UIFactory.Place((RectTransform)badgeLabel.transform, 0.5f, 0.5f, 1f, 1f);
            w.PurchasedBadge = badge.gameObject;

            _items.Add(w);
        }

        // MARK: - 購入を復元 (Swift: restorePurchases ボタン)

        private void BuildRestoreButton(RectTransform parent)
        {
            // HD-2D (2026-07-07): Home のサブボタンと同じ質感に統一。
            UIFactory.SecondaryButton(parent, "RestoreButton", "購入を復元", 0.5f, 0.30f, 0.92f, 0.055f,
                OnRestoreTapped, 34);
        }

        // MARK: - トースト

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
            // 他画面 (CharacterSelect の Editor 即時解放等) での購入反映のため再読込
            App.I.Player.Reload();
            RefreshItems();
        }

        public override void OnHide()
        {
            HideToast();
        }

        // MARK: - 状態反映

        private void RefreshItems()
        {
            var player = App.I.Player;
            foreach (var w in _items)
            {
                // Swift: isCharacterPurchased = purchasedProductIDs 照合 / isAdRemoved = adRemoved フラグ。
                // 広告削除は旧データ互換のため両方を見る。
                bool purchased = w.ProductId == PlayerState.ProductRemoveAds
                    ? (player.AdRemoved || player.IsPurchased(w.ProductId))
                    : player.IsPurchased(w.ProductId);

                w.PriceButtonRoot.SetActive(!purchased);
                w.PurchasedBadge.SetActive(purchased);
                w.Border.color = purchased
                    ? UITheme.WithAlpha(UITheme.Success, 0.3f)
                    : UITheme.WithAlpha(UITheme.GridBorder, 0.3f);
            }
        }

        // MARK: - 操作ハンドラ

        /// <summary>
        /// 購入。IIapService (Phase 3 groundwork) 経由で行う。
        ///   Swift 正本: PurchaseManager.purchaseCharacter / purchaseAdRemoval →
        ///   StoreKitService.purchase (決済・レシート検証・Keychain 保存)。
        ///   本物 SDK 未導入の間は StubIapService が Editor/DevBuild でのみ擬似成功させる
        ///   (Release では常に失敗、success=true は Editor/DevBuild でしか起こらない)。
        ///   購入 UI (.purchaseAlert() / .purchaseLoadingOverlay()) は Phase 3 (実 SDK 導入) で移植する。
        /// </summary>
        private void OnPurchaseTapped(string productId)
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Iap.Purchase(productId, success =>
            {
                if (success)
                {
                    RefreshItems();
                    ShowToast("(デバッグ) 購入処理を完了しました");
                }
                else
                {
                    ShowToast("購入機能は準備中です。アップデートをお待ちください");
                }
            });
        }

        /// <summary>
        /// 購入の復元。IIapService.Restore 経由で行う。
        ///   Swift 正本: purchaseManager.restorePurchases (AppStore.sync +
        ///   Transaction.currentEntitlements 走査)。
        ///   現状 (StubIapService) はローカル PlayerPrefs 保存のみで「復元」の対象が存在しないため
        ///   常に no-op → 案内トースト。
        /// </summary>
        private void OnRestoreTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Iap.Restore(() =>
            {
                RefreshItems();
                ShowToast("購入の復元は準備中です");
            });
        }
    }
}
