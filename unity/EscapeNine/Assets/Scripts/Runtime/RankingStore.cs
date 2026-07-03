// RankingStore.cs
// Swift 正本からの忠実移植: Services/RankingService.swift のローカルランキング部分。
// UserDefaults + JSONEncoder → PlayerPrefs + JsonUtility に置き換え (キー名 "localRankings" は踏襲)。
//
// Game Center / Firebase Firestore への送信は Phase 3 (TODO 参照)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace EscapeNine.Runtime
{
    /// <summary>ランキング1件。Swift: struct RankingEntry</summary>
    [Serializable]
    public sealed class RankingEntry
    {
        // JsonUtility はフィールドのみシリアライズするため public フィールドで持つ
        public string id;
        public int floor;
        public string playerName;
        public string characterType;
        public string timestamp; // ISO-8601 ("o") 形式。Swift の Date に相当

        /// <summary>timestamp を DateTime (UTC) として取得。parse 失敗時は DateTime.MinValue。</summary>
        public DateTime TimestampUtc
        {
            get
            {
                if (DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                {
                    return dt;
                }
                return DateTime.MinValue;
            }
        }

        /// <summary>表示用日時 (端末ローカル時刻)。Swift: formattedDate 相当。</summary>
        public string FormattedDate =>
            TimestampUtc == DateTime.MinValue ? "" : TimestampUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
    }

    public sealed class RankingStore
    {
        private const int MaxEntries = 100;              // Swift: maxEntries
        private const string StorageKey = "localRankings"; // Swift: storageKey

        // JsonUtility はトップレベル配列を扱えないためラッパーで包む
        [Serializable]
        private sealed class RankingListWrapper
        {
            public List<RankingEntry> entries = new List<RankingEntry>();
        }

        private List<RankingEntry> _rankings = new List<RankingEntry>();

        public RankingStore()
        {
            LoadRankings();
        }

        // MARK: - Submit Score

        /// <summary>
        /// スコアを追加して階層降順に整列、上位 MaxEntries 件のみ保持。Swift: submitScore(floor:characterType:)
        /// TODO(Phase 3): GameCenterService.submitScore / FirebaseService.submitScore 相当の
        ///                オンライン送信をここから (または GameController から) 呼ぶ。
        /// </summary>
        public void SubmitScore(int floor, string characterType = "hero")
        {
            var entry = new RankingEntry
            {
                id = Guid.NewGuid().ToString(),
                floor = floor,
                playerName = "あなた", // Swift と同じ固定名 (オンライン表示名は Phase 3)
                characterType = characterType,
                timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            _rankings.Add(entry);

            // OrderByDescending は安定ソート → 同階層は挿入順 (古い記録が先) を維持
            _rankings = _rankings.OrderByDescending(e => e.floor).ToList();
            if (_rankings.Count > MaxEntries)
            {
                _rankings = _rankings.Take(MaxEntries).ToList();
            }

            SaveRankings();
        }

        // MARK: - Get Rankings

        /// <summary>階層降順のランキング一覧。Swift: getRankings()</summary>
        public IReadOnlyList<RankingEntry> GetRankings() => _rankings;

        /// <summary>自己ベスト階層 (記録なしは 0)。Swift: getTopScore()</summary>
        public int GetTopScore() => _rankings.Count > 0 ? _rankings[0].floor : 0;

        // MARK: - Persistence

        private void LoadRankings()
        {
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var wrapper = JsonUtility.FromJson<RankingListWrapper>(json);
                if (wrapper?.entries != null)
                {
                    _rankings = wrapper.entries.OrderByDescending(e => e.floor).ToList();
                }
            }
            catch (Exception e)
            {
                // LogWarning にする理由: EditMode テストでは Error ログがテスト失敗扱いになるため
                // (壊れたデータは「空で立ち上がる」のが期待挙動であってエラーではない)
                Debug.LogWarning($"[RankingStore] ランキング読込に失敗: {e.Message}");
            }
        }

        private void SaveRankings()
        {
            try
            {
                var wrapper = new RankingListWrapper { entries = _rankings };
                PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(wrapper));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RankingStore] ランキング保存に失敗: {e.Message}");
            }
        }

        // MARK: - Clear (Debug)

        /// <summary>全消去。Swift: clearRankings()</summary>
        public void ClearRankings()
        {
            _rankings = new List<RankingEntry>();
            PlayerPrefs.DeleteKey(StorageKey);
        }
    }
}
