// DailyChallengeStore.cs
// Swift 正本からの忠実移植: Services/DailyChallengeService.swift
// UTC 日付ベースのシード生成は Core (DailyChallengeGenerator) に移植済み。本クラスは
// 「本日のチャレンジを保持し、完了記録を PlayerPrefs へ永続化する」Runtime 層の責務のみを担う
// (RankingStore / PlayerState と同じ「App が生成し App.I 経由で参照する」サービスの一つ)。
//
// PlayerPrefs キー名は Swift の UserDefaults キー "dailyChallengeHistory" を踏襲する。
// JsonUtility はトップレベル配列/Dictionary を扱えないため、RankingStore と同様に
// ラッパー型 (records: List<ChallengeRecord>) で包む。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using EscapeNine.Core;

namespace EscapeNine.Runtime
{
    public sealed class DailyChallengeStore
    {
        private const string StorageKey = "dailyChallengeHistory"; // Swift: DailyChallengeService.storageKey
        private const int MaxHistoryDays = 30; // Swift: 30日分のみ保持

        [Serializable]
        private sealed class ChallengeRecord
        {
            public string date;
            public bool isCompleted;
            public int achievedFloor = -1; // -1 = 未設定 (Swift: Int? achievedFloor の nil 相当)
        }

        [Serializable]
        private sealed class ChallengeHistoryWrapper
        {
            public List<ChallengeRecord> records = new List<ChallengeRecord>();
        }

        /// <summary>本日 (UTC) のチャレンジ。完了記録が保存済みならここに反映される。Swift: todaysChallenge</summary>
        public DailyChallenge TodaysChallenge { get; private set; }

        /// <summary>
        /// ゲーム開始前に GameController.StartNewRun が読み取り、消費する pending チャレンジ。
        /// Swift: DailyChallengeService.pendingChallenge (DailyChallengeView がセット → GameViewModel.startGame が読む)。
        /// </summary>
        public DailyChallenge PendingChallenge { get; set; }

        /// <summary>今日まだチャレンジしていないか。Swift: canPlayToday</summary>
        public bool CanPlayToday => !TodaysChallenge.IsCompleted;

        public DailyChallengeStore()
        {
            string today = TodayString();
            TodaysChallenge = DailyChallengeGenerator.BuildChallenge(today);
            LoadRecordInto(TodaysChallenge);
        }

        /// <summary>UTC "yyyy-MM-dd"。Swift: todayString() (TimeZone(identifier: "UTC") 固定)</summary>
        private static string TodayString()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static void LoadRecordInto(DailyChallenge challenge)
        {
            var history = LoadAllHistory();
            var record = history.FirstOrDefault(r => r.date == challenge.Date);
            if (record == null) return;

            challenge.IsCompleted = record.isCompleted;
            challenge.AchievedFloor = record.achievedFloor >= 0 ? (int?)record.achievedFloor : null;
        }

        /// <summary>チャレンジ完了を記録する。Swift: markCompleted(achievedFloor:)</summary>
        public void MarkCompleted(int achievedFloor)
        {
            TodaysChallenge.IsCompleted = true;
            TodaysChallenge.AchievedFloor = achievedFloor;
            SaveRecord();
        }

        private void SaveRecord()
        {
            var history = LoadAllHistory();
            var existing = history.FirstOrDefault(r => r.date == TodaysChallenge.Date);
            if (existing == null)
            {
                existing = new ChallengeRecord { date = TodaysChallenge.Date };
                history.Add(existing);
            }
            existing.isCompleted = TodaysChallenge.IsCompleted;
            existing.achievedFloor = TodaysChallenge.AchievedFloor ?? -1;

            // 30日分のみ保持 (Swift: history.count > 30 で最古日付を1件削除)
            if (history.Count > MaxHistoryDays)
            {
                history = history.OrderBy(r => r.date, StringComparer.Ordinal).ToList();
                history.RemoveAt(0);
            }

            var wrapper = new ChallengeHistoryWrapper { records = history };
            try
            {
                PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(wrapper));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyChallengeStore] 完了記録の保存に失敗: {e.Message}");
            }
        }

        private static List<ChallengeRecord> LoadAllHistory()
        {
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return new List<ChallengeRecord>();

            try
            {
                var wrapper = JsonUtility.FromJson<ChallengeHistoryWrapper>(json);
                return wrapper?.records ?? new List<ChallengeRecord>();
            }
            catch (Exception e)
            {
                // LogWarning にする理由: EditMode テストでは Error ログがテスト失敗扱いになるため
                // (壊れたデータは「空で立ち上がる」のが期待挙動であってエラーではない、RankingStore と同方針)
                Debug.LogWarning($"[DailyChallengeStore] 履歴読込に失敗: {e.Message}");
                return new List<ChallengeRecord>();
            }
        }
    }
}
