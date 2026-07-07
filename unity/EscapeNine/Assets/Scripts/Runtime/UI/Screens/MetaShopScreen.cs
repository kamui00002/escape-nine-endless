// MetaShopScreen.cs
// Unity Phase 5c: メタ進行 UI (docs/unity-phase5-roguelike-design.md §3 メタ進行 / §6.3)。
// Swift 正本には存在しない (Unity固有の追加機能。残光=Unity版だけのメタ通貨)。
//
// 画面名を「遺物庫」にした理由 (HomeScreen.cs 側のボタン命名と揃える): 既存の ShopScreen
// (ScreenId.Shop、IAP でのキャラ購入・広告削除) と役割が別物であるため、「ショップ」を避け
// 「レリックを収集・管理する場所」であることが伝わる名前にした (混同防止)。
//
// スコープ (§3.2 の表のうち本画面が扱うのは「スターターパーク」のみ):
//   - コスメティック購入 / レリックプール拡張 (draft pool 自体のフィルタ) / 6人目キャラは
//     §6.1 の Core 変更が必要になる可能性が高く (RelicDraftService は Core = 編集禁止)、
//     本タスクのスコープ外。非活性の「近日追加」表示のみ置く (§3.2 の「やらないこと」は
//     侵害しない: 残光でキャラ本体やパワーの天井を上げることは一切しない)。
//   - スターターパーク: 残光で解放した Common/Uncommon レリックから1つを「ラン開始時装備」に
//     設定/解除する (§3.2、Rare以上は対象外)。GameController.ApplyStarterPerk() が実際の適用を担う。
//
// 演出方針 (§1.5): 「メタショップは HUD 層の通常画面。舞台は使わない」と明記されているため、
// 他の Phase 5c UI (RelicDraftScreen 等) と異なり BoardStage 演出フックは持たない。
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
    public sealed class MetaShopScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.MetaShop;

        // --- レリック解放コスト (残光、§3.2「早期解放」) ---
        // [要検証] 仮値。設計書 §3.1 の残光算出式自体も [要検証] であり、実プレイの蓄積速度を見て
        // 両方を合わせてチューニングする前提 (docs/unity-phase5-roguelike-design.md §3.2 実装コメント)。
        // Core の RelicConfig.cs は編集禁止のため、価格テーブルは意図的にここ (UI 層) に閉じる。
        private const int CommonUnlockCost = 50;
        private const int UncommonUnlockCost = 120;

        /// <summary>1 行あたりの高さ (ビューポート高さ比、行間込み)。AchievementScreen と同じ技法。</summary>
        private const float RowHeightVp = 0.20f;

        /// <summary>
        /// スターターパーク対象レリック (§3.2: Common/Uncommon のみ)。RelicCatalog.All (18種、Core =
        /// 編集禁止) から静的にフィルタする。表示順は RelicCatalog の定義順 (決定論的、実行毎に変わらない)。
        /// </summary>
        private static readonly RelicDefinition[] EligibleRelics = BuildEligibleRelics();

        private static RelicDefinition[] BuildEligibleRelics()
        {
            var list = new List<RelicDefinition>();
            foreach (var def in RelicCatalog.All)
            {
                if (def.Rarity == RelicRarity.Common || def.Rarity == RelicRarity.Uncommon)
                {
                    list.Add(def);
                }
            }
            return list.ToArray();
        }

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築防止ガード (他画面と同じ対策)。
        private bool _built;

        private TextMeshProUGUI _currencyLabel;
        private TextMeshProUGUI _equippedLabel;
        private ScrollRect _scroll;
        private RectTransform _listContent;

        // トースト (残高不足等の簡易通知。ShopScreen/HomeScreen と同じ方式)
        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;
        private const float ToastDisplaySeconds = 1.6f;

        // MARK: - BuildUI

        public override void BuildUI()
        {
            if (_built) return;
            _built = true;

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
            BuildCurrencySection(safe);
            BuildStarterPerkHeader(safe);
            BuildListArea(safe);
            BuildFutureSection(safe);
            BuildToast(safe);
        }

        public override void OnShow(object payload)
        {
            // 他画面 (Result 画面での AddMetaCurrency 等) での残高変動を反映するため再読込。
            App.I.Player.Reload();
            RefreshAll();
        }

        public override void OnHide()
        {
            HideToast();
        }

        // MARK: - Header

        private void BuildHeader(RectTransform parent)
        {
            UIFactory.SecondaryButton(parent, "BackButton", "< 戻る", 0.12f, 0.955f, 0.18f, 0.045f,
                OnBackTapped, 36);

            var title = UIFactory.Label(parent, "TitleLabel", "遺物庫", 64, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.955f, 0.5f, 0.05f);
        }

        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - Currency (§3.1: 残光の残高。金色表記)

        private void BuildCurrencySection(RectTransform parent)
        {
            _currencyLabel = UIFactory.Label(parent, "CurrencyLabel", "残光: 0", 44, UITheme.GoldText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)_currencyLabel.transform, 0.5f, 0.895f, 0.7f, 0.05f);
        }

        // MARK: - Starter Perk Header (§3.2)

        private void BuildStarterPerkHeader(RectTransform parent)
        {
            var section = UIFactory.Label(parent, "StarterPerkSection", "スターターパーク (Common/Uncommonのみ)",
                30, UITheme.GoldText, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)section.transform, 0.5f, 0.8375f, 0.92f, 0.045f);

            _equippedLabel = UIFactory.Label(parent, "EquippedLabel", "現在の装備: なし", 26,
                UITheme.WithAlpha(UITheme.TextColor, 0.8f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)_equippedLabel.transform, 0.5f, 0.7875f, 0.92f, 0.035f);
        }

        // MARK: - List Area (Swift 版に対応 View なし。AchievementScreen と同じ ScrollRect 技法)

        private void BuildListArea(RectTransform parent)
        {
            _scroll = BuildScrollView(parent, "RelicScroll", out _listContent);
            UIFactory.Place((RectTransform)_scroll.transform, 0.5f, 0.465f, 1f, 0.58f);
        }

        // MARK: - Future Section (§3.2: コスメティック / レリックプール拡張 / 6人目キャラは非活性のみ)

        private void BuildFutureSection(RectTransform parent)
        {
            // HD-2D (2026-07-07): フラット塗りから Card(PanelFill) + BorderTrim へ。
            var panel = UIFactory.Card(parent, "FutureSection", out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(panel, 0.5f, 0.095f, 0.94f, 0.13f);
            UIFactory.BorderTrim(panel, "FutureSectionBorder", UITheme.Accent, 0.3f);

            var label = UIFactory.Label(panel, "FutureLabel",
                "コスメティック・レリックプール拡張・追加キャラクター (近日追加)", 24,
                UITheme.WithAlpha(UITheme.TextColor, 0.45f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.Place((RectTransform)label.transform, 0.5f, 0.5f, 0.9f, 0.6f);
        }

        // MARK: - 再描画

        private void RefreshAll()
        {
            var player = App.I.Player;

            _currencyLabel.text = "残光: " + player.MetaCurrency;
            _equippedLabel.text = "現在の装備: " + CurrentEquippedName(player);

            RebuildRows(player);
        }

        private static string CurrentEquippedName(PlayerState player)
        {
            if (string.IsNullOrEmpty(player.StarterPerkRelicId)) return "なし";
            RelicDefinition? def = RelicCatalog.Find(player.StarterPerkRelicId);
            return def?.Name ?? "なし";
        }

        /// <summary>行を全て作り直す (対象は8種固定なので開くたび全再構築で十分軽い、AchievementScreen と同方針)。</summary>
        private void RebuildRows(PlayerState player)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            int count = EligibleRelics.Length;
            float k = Mathf.Max(1f, count * RowHeightVp + 0.02f);
            SetContentHeight(_listContent, k);

            for (int i = 0; i < count; i++)
            {
                float cy = 1f - (0.01f + RowHeightVp * (i + 0.5f)) / k;
                float h = RowHeightVp * 0.88f / k; // 0.88 = 行間ぶんの余白 (Achievement の 0.9 よりやや広め、行内の情報量が多いため)
                var def = EligibleRelics[i];
                bool unlocked = player.IsRelicUnlocked(def.Id);
                bool equipped = unlocked && player.StarterPerkRelicId == def.Id;
                BuildRelicRow(_listContent, i, def, unlocked, equipped, cy, h);
            }

            _scroll.verticalNormalizedPosition = 1f;
            _listContent.anchoredPosition = Vector2.zero;
        }

        /// <summary>レリック行。未解放は「解放 (残光N)」ボタン、解放済みは「装備/解除」トグル。</summary>
        private void BuildRelicRow(RectTransform parent, int index, RelicDefinition def, bool unlocked, bool equipped, float cy, float h)
        {
            // HD-2D (2026-07-07): フラット塗りの行から Card(PanelFill) へ。行は RefreshAll のたびに
            // 全破棄→再構築されるため (RebuildRows 参照)、Image を後から動的に塗り替えるコードは
            // どこにも無い = Card 化しても契約は壊れない。レアリティ色を BorderTrim の縁取りに使い
            // 「カードらしさ」を強調する (タスク要件)。未解放時の減光は CanvasGroup.alpha で表現する
            // (Card 内部のグラデ/影/ハイライトの複数レイヤーを一括で暗くするため、単一 Image.color の
            // アルファ操作では代替できない)。
            var row = UIFactory.Card(parent, "Row" + index, out _, UITheme.PanelFillTop, UITheme.PanelFillBottom);
            UIFactory.Place(row, 0.5f, cy, 0.94f, h);
            if (!unlocked)
            {
                row.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            }

            Color rarityColor = RarityColor(def.Rarity);
            UIFactory.BorderTrim(row, "Row" + index + "Border", rarityColor, unlocked ? 0.75f : 0.4f);

            if (equipped)
            {
                // 装備中の行は上辺だけ success 色で上書き強調 (BorderTrim のレアリティ縁取りより手前に
                // 生成することで、同じ上辺位置でも success 色が勝つ)
                var border = UIFactory.ColorRect(row, "EquippedBorder", UITheme.Success);
                UIFactory.Place((RectTransform)border.transform, 0.5f, 0.98f, 1f, 0.02f);
            }

            var rarityLabel = UIFactory.Label(row, "Rarity", RarityText(def.Rarity), 20, RarityColor(def.Rarity),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)rarityLabel.transform, 0.095f, 0.78f, 0.15f, 0.28f);

            var nameLabel = UIFactory.Label(row, "Name", def.Name, 32,
                unlocked ? UITheme.TextColor : UITheme.WithAlpha(UITheme.TextColor, 0.55f),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)nameLabel.transform, 0.36f, 0.70f, 0.54f, 0.32f);

            var descLabel = UIFactory.Label(row, "Description", def.Description, 20,
                UITheme.WithAlpha(UITheme.TextColor, unlocked ? 0.7f : 0.4f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)descLabel.transform, 0.36f, 0.30f, 0.54f, 0.42f);

            if (!unlocked)
            {
                BuildUnlockButton(row, def);
            }
            else
            {
                BuildEquipToggleButton(row, def, equipped);
            }
        }

        private void BuildUnlockButton(RectTransform row, RelicDefinition def)
        {
            int cost = UnlockCost(def.Rarity);
            bool affordable = App.I.Player.MetaCurrency >= cost;

            // HD-2D (2026-07-07): 塗りを ButtonFill に (行が Card 化されたため Card 二重掛けはせず
            // EmbossTrim のみ足す)。afford/not-afford の意味は文字色 (fg) 側が担うため塗り変更の影響はない。
            var btn = UIFactory.TextButton(row, "UnlockButton", $"解放\n残光{cost}", 22,
                UITheme.ButtonFill, affordable ? UITheme.Available : UITheme.WithAlpha(UITheme.TextColor, 0.35f),
                () => OnUnlockTapped(def.Id, cost));
            UIFactory.Place((RectTransform)btn.transform, 0.865f, 0.5f, 0.22f, 0.72f);
            UIFactory.EmbossTrim(btn.transform, "UnlockEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
            btn.interactable = affordable; // 残高不足時はタップしても反応しない (二重防御。TryUnlockRelic 側でも判定)
        }

        private void BuildEquipToggleButton(RectTransform row, RelicDefinition def, bool equipped)
        {
            string label = equipped ? "解除" : "装備";
            // HD-2D (2026-07-07): 未装備時の塗りを Background → ButtonFill に (行の Card 化に伴い視認性確保)。
            Color bg = equipped ? UITheme.Success : UITheme.ButtonFill;
            Color fg = equipped ? UITheme.Background : UITheme.TextColor;

            var btn = UIFactory.TextButton(row, "EquipButton", label, 28, bg, fg,
                () => OnEquipTapped(def.Id, equipped));
            UIFactory.Place((RectTransform)btn.transform, 0.865f, 0.5f, 0.22f, 0.6f);
            UIFactory.EmbossTrim(btn.transform, "EquipEmboss", UITheme.ButtonHighlightLine, UITheme.Accent);
        }

        private static string RarityText(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return "Common";
                case RelicRarity.Uncommon: return "Uncommon";
                default: return rarity.ToString(); // 到達しない想定 (EligibleRelics で Common/Uncommon のみに絞済み)
            }
        }

        private static Color RarityColor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return UITheme.WithAlpha(UITheme.TextColor, 0.6f);
                case RelicRarity.Uncommon: return UITheme.Success;
                default: return UITheme.GoldText;
            }
        }

        private static int UnlockCost(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return CommonUnlockCost;
                case RelicRarity.Uncommon: return UncommonUnlockCost;
                default: return int.MaxValue; // §3.2: Rare以上はスターターパーク対象外 (EligibleRelics で既に除外済みの二重防御)
            }
        }

        // MARK: - 操作ハンドラ

        private void OnUnlockTapped(string relicId, int cost)
        {
            App.I.Audio.PlaySfx("button_tap");
            bool success = App.I.Player.TryUnlockRelic(relicId, cost);
            if (!success)
            {
                ShowToast("残光が足りません");
                return;
            }
            RefreshAll();
        }

        /// <summary>
        /// 装備/解除。1枠のみ (§3.2) のため、新規装備は PlayerState.SetStarterPerk が内部で
        /// 前の装備を自動的に上書きする (他の行の表示は次回 RefreshAll の RebuildRows で追従)。
        /// </summary>
        private void OnEquipTapped(string relicId, bool currentlyEquipped)
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Player.SetStarterPerk(currentlyEquipped ? null : relicId);
            RefreshAll();
        }

        // MARK: - トースト (ShopScreen/HomeScreen と同じ方式)

        private void BuildToast(RectTransform parent)
        {
            // Future セクション (非活性の「近日追加」表示) の上に一時的に重ねる。トーストは
            // 数秒で自動的に消える一過性オーバーレイのため、常設要素との重なりは許容する
            // (ShopScreen/HomeScreen のトーストも同じ「空いている帯に置く」以上の厳密な非重なりは求めていない)。
            _toast = UIFactory.Panel(parent, "Toast", UITheme.WithAlpha(Color.black, 0.85f));
            UIFactory.Place(_toast, 0.5f, 0.095f, 0.7f, 0.045f);
            _toastLabel = UIFactory.Label(_toast, "Label", "", 28, UITheme.GoldText);
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
            yield return new WaitForSeconds(ToastDisplaySeconds);
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

        // MARK: - ScrollRect のコード構築 (AchievementScreen と同型。クラス間の共有シンボルを
        // 作らないため意図的に各画面へ private 複製している — 他画面担当エージェントとの
        // クラス名衝突を避けるトレードオフ。既存コメントを踏襲)

        private static ScrollRect BuildScrollView(RectTransform parent, string name, out RectTransform content)
        {
            var root = UIFactory.Panel(parent, name, Color.clear);

            var viewport = UIFactory.Panel(root, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.Panel(viewport, "Content");

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

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
