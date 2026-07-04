// AchievementScreen.cs
// Swift 正本: Views/Achievement/AchievementPopupView.swift 内の AchievementListView + AchievementRow
// (実績一覧画面。ヘッダーの進捗 "X/9" + プログレスバー + 実績行のスクロールリスト)。
// ポップアップ演出本体 (AchievementPopupView) は ResultScreen.cs へ移植する
// (勝利直後のリザルト画面に出す方が Unity 版の画面遷移と自然に整合するため)。
//
// レイアウトは全て UIFactory.Place の親比率 0..1 (固定 px 禁止)。
// 座標系は Unity 左下原点: cy が大きいほど画面上部 (Swift の top-left と逆)。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EscapeNine.Core;

namespace EscapeNine.Runtime.UI
{
    public sealed class AchievementScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.Achievements;

        /// <summary>Swift: Achievement.allCases。C# enum の宣言順が Swift の宣言順と一致する (Achievement.cs 移植メモ)。</summary>
        private static readonly Achievement[] AllAchievements = (Achievement[])Enum.GetValues(typeof(Achievement));

        /// <summary>1 行あたりの高さ (ビューポート高さ比、行間込み)。</summary>
        private const float RowHeightVp = 0.115f;

        // App.cs は screen.BuildUI() → Router.Register(screen) の順で呼び、Register 内でも
        // BuildUI() が走るため計 2 回呼ばれる。二重構築防止ガード (他画面と同じ対策)。
        private bool _built;

        private TextMeshProUGUI _progressLabel;
        private RectTransform _progressBarFill;
        private ScrollRect _scroll;
        private RectTransform _listContent;

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

            var bg = UIFactory.ColorRect(transform, "Background", UITheme.Background);
            UIFactory.Place((RectTransform)bg.transform, 0.5f, 0.5f, 1f, 1f);

            var safe = UIFactory.Panel(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildHeader(safe);
            BuildProgressBar(safe);
            BuildListArea(safe);
        }

        public override void OnShow(object payload)
        {
            RefreshAll();
        }

        // MARK: - Header (Swift: ヘッダー「実績」+ 進捗 "X/9")

        private void BuildHeader(RectTransform parent)
        {
            var back = UIFactory.TextButton(parent, "BackButton", "< 戻る", 36,
                UITheme.BackgroundSecondary, UITheme.TextColor, OnBackTapped);
            UIFactory.Place((RectTransform)back.transform, 0.12f, 0.955f, 0.18f, 0.045f);

            var title = UIFactory.Label(parent, "TitleLabel", "実績", 64, UITheme.TextColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.5f, 0.955f, 0.5f, 0.05f);

            // 幅を 0.24→0.19 に縮小 (2026-07-04 重なり監査で検出): TitleLabel (cx=0.5, 幅 0.5 →
            // 右端 0.75) と cx=0.86 のまま幅 0.24 だと左端 0.74 が食い込む。cx は右寄せのまま
            // (画面右端との余白は維持)、幅だけ絞って左端をタイトルから離す (表示は短い分数 "X/9" のみで
            // 折返しリスクは無い)。
            _progressLabel = UIFactory.Label(parent, "ProgressLabel", "0/" + AllAchievements.Length, 36,
                UITheme.Available, TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place((RectTransform)_progressLabel.transform, 0.86f, 0.955f, 0.19f, 0.045f);
        }

        private void OnBackTapped()
        {
            App.I.Audio.PlaySfx("button_tap");
            App.I.Router.Show(ScreenId.Home);
        }

        // MARK: - Progress Bar (Swift: GeometryReader の progressBar)

        private void BuildProgressBar(RectTransform parent)
        {
            var track = UIFactory.Panel(parent, "ProgressTrack", UITheme.WithAlpha(Color.white, 0.15f));
            UIFactory.Place(track, 0.5f, 0.895f, 0.92f, 0.012f);

            var fill = UIFactory.ColorRect(track, "ProgressFill", UITheme.Available);
            _progressBarFill = (RectTransform)fill.transform;
            _progressBarFill.anchorMin = new Vector2(0f, 0f);
            _progressBarFill.anchorMax = new Vector2(0f, 1f); // 幅 (右端) は RefreshAll で更新
            _progressBarFill.offsetMin = Vector2.zero;
            _progressBarFill.offsetMax = Vector2.zero;
        }

        // MARK: - List Area (Swift: ScrollView + LazyVStack)

        private void BuildListArea(RectTransform parent)
        {
            _scroll = BuildScrollView(parent, "AchievementScroll", out _listContent);
            UIFactory.Place((RectTransform)_scroll.transform, 0.5f, 0.44f, 1f, 0.78f);
        }

        // MARK: - 再描画

        private void RefreshAll()
        {
            var unlocked = LoadUnlockedAchievements();

            _progressLabel.text = unlocked.Count + "/" + AllAchievements.Length;

            float ratio = AllAchievements.Length > 0 ? (float)unlocked.Count / AllAchievements.Length : 0f;
            _progressBarFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);

            RebuildRows(unlocked);
        }

        /// <summary>
        /// 行を全て作り直す (Swift の List は差分描画だが、実績は 9 件固定なので開くたび全再構築で十分軽い)。
        /// </summary>
        private void RebuildRows(HashSet<Achievement> unlocked)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            int count = AllAchievements.Length;
            float k = Mathf.Max(1f, count * RowHeightVp + 0.02f);
            SetContentHeight(_listContent, k);

            for (int i = 0; i < count; i++)
            {
                float cy = 1f - (0.01f + RowHeightVp * (i + 0.5f)) / k;
                float h = RowHeightVp * 0.9f / k; // 0.9 = 行間ぶんの余白
                var achievement = AllAchievements[i];
                BuildRow(_listContent, i, achievement, unlocked.Contains(achievement), cy, h);
            }

            _scroll.verticalNormalizedPosition = 1f;
            _listContent.anchoredPosition = Vector2.zero;
        }

        /// <summary>実績行 (Swift: AchievementRow)。ロック中は全体を減光する。</summary>
        private void BuildRow(RectTransform parent, int index, Achievement achievement, bool isUnlocked, float cy, float h)
        {
            var row = UIFactory.Panel(parent, "Row" + index,
                UITheme.WithAlpha(UITheme.BackgroundSecondary, isUnlocked ? 1f : 0.5f));
            UIFactory.Place(row, 0.5f, cy, 0.94f, h);

            var title = UIFactory.Label(row, "Title", achievement.Title(), 40,
                isUnlocked ? UITheme.TextColor : UITheme.WithAlpha(UITheme.TextColor, 0.5f),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place((RectTransform)title.transform, 0.40f, 0.68f, 0.74f, 0.5f);

            var desc = UIFactory.Label(row, "Description", achievement.Description(), 28,
                UITheme.WithAlpha(UITheme.TextColor, 0.6f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)desc.transform, 0.40f, 0.28f, 0.74f, 0.4f);

            // Swift: checkmark.circle.fill (success) / lock.fill (減光)。legacy Text は SF Symbols を
            // 描画できないため文字ラベルで代替 (他画面と同じ簡略化方針)。
            var status = UIFactory.Label(row, "Status", isUnlocked ? "解除済み" : "未解除", 32,
                isUnlocked ? UITheme.Success : UITheme.WithAlpha(UITheme.TextColor, 0.35f),
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.Place((RectTransform)status.transform, 0.89f, 0.5f, 0.20f, 0.6f);
        }

        /// <summary>解除済み実績を読む。GameController.UnlockedAchievementsKey と同じ書式 (enum 名 CSV)。</summary>
        private static HashSet<Achievement> LoadUnlockedAchievements()
        {
            string raw = PlayerPrefs.GetString(GameController.UnlockedAchievementsKey, "");
            var result = new HashSet<Achievement>();
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var token in raw.Split(','))
            {
                if (Enum.TryParse(token, out Achievement a)) result.Add(a);
            }
            return result;
        }

        // MARK: - ScrollRect のコード構築 (RankingScreen と同型。クラス間の共有シンボルを
        // 作らないため意図的に各画面へ private 複製している — 他画面担当エージェントとの
        // クラス名衝突を避けるトレードオフ)

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
