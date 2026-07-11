// AnalyticsService.cs
// Swift 正本: Services/AnalyticsEvents.swift (AnalyticsLogger) の PostHog 送信部分を
// Unity (C#, PostHog REST 直叩き) へ 1:1 移植したもの。
//
// 【単一ファサード厳守】呼び出し側 (GameController / ResultScreen / TutorialScreen /
// UnityIapService) は必ずこのクラスの Log* メソッド経由でイベントを送る。直接 PostHog REST を
// 叩く箇所を増やさない (Swift 側の反面教師: ConversionService.swift が tutorial_complete /
// eg_tutorial_complete の二重送信を産んでいる。Unity 版はこの重複を再現しない)。
//
// UnityWebRequest はコルーチンでの実行が必要なため、本クラス自体を MonoBehaviour にし、
// App (Runtime/App.cs) の GameObject に AddComponent する (AudioDirector/Conductor と同じ流儀)。
//
// distinct_id は PlayerPrefs (analyticsDistinctId) に GUID を初回生成・永続化して再利用する
// (PostHog 固有の識別子。Firebase/Firestore の匿名認証 UID とは別軸)。
//
// 送信は fire-and-forget (ネットワーク失敗・タイムアウトでゲームを止めない/例外を投げない)。
// 失敗時は Debug.LogWarning で必ず可視化する (silent failure 禁止)。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace EscapeNine.Runtime.Analytics
{
    public sealed class AnalyticsService : MonoBehaviour
    {
        private const string DistinctIdPrefsKey = "analyticsDistinctId";

        // MARK: - Event Names (Swift: AnalyticsEvent の rawValue と厳密一致)
        private const string EventGameStarted = "eg_game_started";
        private const string EventFloorCleared = "eg_floor_cleared";
        private const string EventGameOverShown = "eg_game_over_shown";
        private const string EventRetryTapped = "eg_retry_tapped";
        private const string EventHomeTapped = "eg_home_tapped";
        private const string EventTutorialStarted = "eg_tutorial_started";
        private const string EventTutorialStepCompleted = "eg_tutorial_step_completed";
        private const string EventTutorialComplete = "eg_tutorial_complete";
        private const string EventPurchase = "purchase";

        // MARK: - Parameter Keys (Swift: AnalyticsParam と厳密一致、スネークケース)
        private const string ParamFloor = "floor";
        private const string ParamFromFloor = "from_floor";
        private const string ParamIsDailyChallenge = "is_daily_challenge";
        private const string ParamCharacterId = "character_id";
        private const string ParamDefeatReason = "defeat_reason";
        private const string ParamNearMissDistance = "near_miss_distance";
        private const string ParamElapsedSeconds = "elapsed_seconds";
        private const string ParamSecondsUntilTap = "seconds_until_tap";
        private const string ParamClearSeconds = "clear_seconds";
        private const string ParamStepNumber = "step_number";
        private const string ParamSkipped = "skipped";
        private const string ParamProductId = "product_id";
        private const string ParamValue = "value";
        private const string ParamCurrency = "currency";

        private string _distinctId;

        // MARK: - Lifecycle

        private void Awake()
        {
            // App.Awake() から AddComponent された時点で即座に呼ばれる (Unity は Active な
            // GameObject への AddComponent 直後に Awake を同期実行する)。
            _distinctId = PlayerPrefs.GetString(DistinctIdPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(_distinctId))
            {
                _distinctId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(DistinctIdPrefsKey, _distinctId);
                PlayerPrefs.Save();
            }
        }

        // MARK: - Sprint 1 便利メソッド (Swift: AnalyticsLogger.log* と 1:1)

        /// <summary>`eg_game_started` を送信する。ゲーム開始 (StartNewRun) で呼び出す。</summary>
        public void LogGameStarted(int floor, bool isDailyChallenge, string characterId)
        {
            Log(EventGameStarted, new Dictionary<string, object>
            {
                { ParamFloor, floor },
                { ParamIsDailyChallenge, isDailyChallenge },
                { ParamCharacterId, characterId }
            });
        }

        /// <summary>`eg_floor_cleared` を送信する。階層クリア確定時 (次階層へ進む前) に呼び出す。</summary>
        public void LogFloorCleared(int floor, double clearSeconds)
        {
            Log(EventFloorCleared, new Dictionary<string, object>
            {
                { ParamFloor, floor },
                { ParamClearSeconds, clearSeconds }
            });
        }

        /// <summary>`eg_game_over_shown` を送信する。敗北確定 (EndGame(won:false)) 時に呼び出す。</summary>
        public void LogGameOverShown(int floor, string defeatReason, int nearMissDistance, double elapsedSeconds)
        {
            Log(EventGameOverShown, new Dictionary<string, object>
            {
                { ParamFloor, floor },
                { ParamDefeatReason, defeatReason },
                { ParamNearMissDistance, nearMissDistance },
                { ParamElapsedSeconds, elapsedSeconds }
            });
        }

        /// <summary>`eg_retry_tapped` を送信する。リザルト画面の「もう一回」タップ時に呼び出す。</summary>
        public void LogRetryTapped(int fromFloor, double secondsUntilTap)
        {
            Log(EventRetryTapped, new Dictionary<string, object>
            {
                { ParamFromFloor, fromFloor },
                { ParamSecondsUntilTap, secondsUntilTap }
            });
        }

        /// <summary>`eg_home_tapped` を送信する。リザルト画面の「ホーム」タップ時に呼び出す。</summary>
        public void LogHomeTapped(int fromFloor)
        {
            Log(EventHomeTapped, new Dictionary<string, object>
            {
                { ParamFromFloor, fromFloor }
            });
        }

        /// <summary>`eg_tutorial_started` を送信する。チュートリアル Step1 (1 ページ目) 表示時に呼び出す。</summary>
        public void LogTutorialStarted()
        {
            Log(EventTutorialStarted);
        }

        /// <summary>`eg_tutorial_step_completed` を送信する。各ページ完了 (次へ/スキップ) 時に呼び出す。</summary>
        public void LogTutorialStepCompleted(int stepNumber, bool skipped)
        {
            Log(EventTutorialStepCompleted, new Dictionary<string, object>
            {
                { ParamStepNumber, stepNumber },
                { ParamSkipped, skipped }
            });
        }

        /// <summary>`eg_tutorial_complete` を送信する。最終ページ完了 (チュートリアル全体終了) 時に呼び出す。</summary>
        public void LogTutorialComplete(double elapsedSeconds)
        {
            Log(EventTutorialComplete, new Dictionary<string, object>
            {
                { ParamElapsedSeconds, elapsedSeconds }
            });
        }

        /// <summary>`purchase` を送信する。IAP 購入確定 (OnPurchasePending) 時に呼び出す。</summary>
        public void LogPurchase(string productId, double value, string currency)
        {
            Log(EventPurchase, new Dictionary<string, object>
            {
                { ParamProductId, productId },
                { ParamValue, value },
                { ParamCurrency, currency }
            });
        }

        // MARK: - Core (PostHog REST 送信)

        private void Log(string eventName, Dictionary<string, object> properties = null)
        {
            StartCoroutine(SendEvent(eventName, properties));
        }

        private IEnumerator SendEvent(string eventName, Dictionary<string, object> properties)
        {
            string body = BuildBodyJson(eventName, properties);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            string url = AnalyticsConfig.PostHogHost + "/capture/";

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    // ネットワーク失敗・タイムアウトでもゲームを止めない (fire-and-forget)。
                    // ただし silent failure は禁止 — 必ず可視化する。
                    Debug.LogWarning($"[AnalyticsService] 送信失敗: {eventName} ({request.error})");
                }
                else
                {
                    Debug.Log($"[AnalyticsService] 送信成功: {eventName}");
                }
            }
        }

        /// <summary>
        /// PostHog /capture/ の POST body を組み立てる。
        /// Unity の JsonUtility は Dictionary&lt;string,object&gt; を扱えないため、手動で JSON 文字列を組む。
        /// </summary>
        private string BuildBodyJson(string eventName, Dictionary<string, object> properties)
        {
            var sb = new StringBuilder();
            sb.Append("{\"api_key\":\"").Append(EscapeJsonString(AnalyticsConfig.ProjectKey)).Append('"');
            sb.Append(",\"event\":\"").Append(EscapeJsonString(eventName)).Append('"');
            sb.Append(",\"distinct_id\":\"").Append(EscapeJsonString(_distinctId)).Append('"');
            sb.Append(",\"properties\":").Append(BuildPropsJson(properties));
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>properties 辞書を JSON オブジェクト文字列へ変換する (int/bool/double/string を型別処理)。</summary>
        internal static string BuildPropsJson(Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0) return "{}";

            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in properties)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(EscapeJsonString(kv.Key)).Append("\":").Append(SerializeValue(kv.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeValue(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case bool b:
                    return b ? "true" : "false";
                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);
                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);
                case float f:
                    return ((double)f).ToString(CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case string s:
                    return "\"" + EscapeJsonString(s) + "\"";
                default:
                    // 想定外の型は文字列化してエスケープする (黙って落とすより安全側)。
                    return "\"" + EscapeJsonString(value.ToString()) + "\"";
            }
        }

        private static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
