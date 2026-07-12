// OnlineRankingService.cs
// 世界ランキング: Firebase Auth REST (匿名認証) + Firestore REST の直叩き実装。
// curl で全往復 (signUp → PATCH 書き込み → runQuery 読み取り) を検証済みの方式をそのまま踏襲する。
//
// 【単一ファサード厳守】Firestore/Firebase Auth への直接アクセスはこのクラス経由のみ
// (AnalyticsService / UnityIapService と同じ流儀。呼び出し側は GameController.PersistRunResult /
// RankingScreen のみを想定)。
//
// UnityWebRequest はコルーチンでの実行が必要なため、本クラス自体を MonoBehaviour にし、
// App (Runtime/App.cs) の GameObject に AddComponent する。
//
// 認証は「匿名UIDの永続化」が最重要: PlayerPrefs (firebaseRefreshToken / firebaseLocalId) に
// refreshToken を保存し、起動のたびに signUp するのではなく refresh_token で同一UIDを再利用する
// (毎起動 signUp すると別UIDが量産され、自分の順位・自己ベストが無意味になる)。
//
// Firestore の typed fields (JSON) は Unity 標準の JsonUtility では組み立て・解析ができないため、
// AnalyticsService の BuildPropsJson と同様に手動で文字列を組み立て/解析する。
//
// 送信・取得は fire-and-forget 志向 (ネットワーク失敗でゲームを止めない)。ただし
// silent failure は禁止 — 失敗は必ず Debug.LogWarning で可視化する。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using EscapeNine.Core;

namespace EscapeNine.Runtime.Ranking
{
    /// <summary>
    /// 世界ランキング1件 (Firestore rankings コレクションの1ドキュメント相当)。
    /// ローカル履歴の RankingStore.RankingEntry (Runtime 名前空間) とはフィールド構成が異なる
    /// (displayName / isMe を持ち、日時は表示しない) ため、意図的に別型として定義する。
    /// </summary>
    public sealed class OnlineRankingEntry
    {
        public string UserId;
        public string DisplayName;
        public int Floor;
        public string CharacterType;

        /// <summary>この端末の匿名UIDと一致するか (RankingScreen がハイライト表示に使う)。</summary>
        public bool IsMe;
    }

    public sealed class OnlineRankingService : MonoBehaviour
    {
        // MARK: - PlayerPrefs keys (匿名認証の永続化。値の意味は上部コメント参照)
        private const string RefreshTokenPrefsKey = "firebaseRefreshToken";
        private const string LocalIdPrefsKey = "firebaseLocalId";
        private const string DisplayNamePrefsKey = "onlineDisplayName";

        // MARK: - Endpoints (Firebase Auth REST / Firestore REST、project は RankingSecrets 参照)
        private const string SignUpUrlTemplate = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={0}";
        private const string RefreshTokenUrlTemplate = "https://securetoken.googleapis.com/v1/token?key={0}";
        private const string FirestoreDocsUrlTemplate =
            "https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents";

        private const int MaxFloor = 100; // rules: floor int 1-100
        private static readonly string[] ValidCharacterTypes = { "hero", "thief", "wizard", "elf", "knight" };

        // 自動生成する表示名の語彙 (各10語程度の英単語。既存ライブデータの雰囲気に寄せる)。
        private static readonly string[] NameAdjectives =
        {
            "Plain", "Odd", "Swift", "Brave", "Quiet", "Lucky", "Bold", "Calm", "Sharp", "Wild"
        };
        private static readonly string[] NameCreatures =
        {
            "Goose", "Treant", "Fox", "Otter", "Falcon", "Wolf", "Heron", "Badger", "Lynx", "Raven"
        };

        // MARK: - 認証状態 (メモリ保持。idToken は約1時間有効)
        private string _idToken;
        private string _uid;
        private bool _authInProgress;
        private readonly List<Action<bool>> _pendingAuthCallbacks = new List<Action<bool>>();

        // MARK: - Public API

        /// <summary>
        /// 認証済み (idToken/UID 保持済み) を保証する。未認証なら refresh_token → (失敗時のみ) signUp の順で
        /// 試行する。複数箇所から同時に呼ばれても signUp/refresh は 1 回しか実行しない (キューイング)。
        /// </summary>
        public void EnsureAuth(Action<bool> onReady)
        {
            if (!string.IsNullOrEmpty(_idToken) && !string.IsNullOrEmpty(_uid))
            {
                onReady?.Invoke(true);
                return;
            }

            if (onReady != null) _pendingAuthCallbacks.Add(onReady);

            if (_authInProgress) return; // 既に進行中の認証に相乗りするだけ (二重 signUp 防止)
            _authInProgress = true;
            StartCoroutine(AuthenticateCoroutine());
        }

        /// <summary>
        /// スコアを送信する (fire-and-forget)。floor は 1-100 にクランプ、characterType は不正なら "hero"。
        /// 呼び出し側 (GameController) は「自己ベスト更新時のみ」呼ぶ想定
        /// (Firestore update ルールが floor>=既存の単調増加を要求するため)。
        /// </summary>
        public void SubmitScore(int floor, string characterType)
        {
            EnsureAuth(ok =>
            {
                if (!ok)
                {
                    Debug.LogWarning("[OnlineRankingService] 未認証のためスコア送信をスキップしました");
                    return;
                }
                StartCoroutine(SubmitScoreCoroutine(floor, characterType, retryOn401: true));
            });
        }

        /// <summary>
        /// floor 降順で上位100件を取得する。取得成功で onResult(entries)、失敗 (未認証/ネットワーク不可等) は
        /// onResult(null) を呼ぶ (silent failure 禁止 — 呼び出し側で null をローカルフォールバックの合図にする)。
        /// </summary>
        public void FetchRankings(Action<List<OnlineRankingEntry>> onResult)
        {
            EnsureAuth(ok =>
            {
                if (!ok)
                {
                    Debug.LogWarning("[OnlineRankingService] 未認証のためランキング取得をスキップしました");
                    onResult?.Invoke(null);
                    return;
                }
                StartCoroutine(FetchRankingsCoroutine(onResult, retryOn401: true));
            });
        }

        // MARK: - 認証コルーチン

        private IEnumerator AuthenticateCoroutine()
        {
            string refreshToken = PlayerPrefs.GetString(RefreshTokenPrefsKey, string.Empty);
            bool authenticated = false;

            if (!string.IsNullOrEmpty(refreshToken))
            {
                yield return StartCoroutine(RefreshTokenCoroutine(refreshToken, success => authenticated = success));
            }

            // refreshToken が無い、または refresh に失敗した場合のみ signUp (毎起動 signUp は禁止)。
            if (!authenticated)
            {
                yield return StartCoroutine(SignUpCoroutine(success => authenticated = success));
            }

            _authInProgress = false;
            var callbacks = new List<Action<bool>>(_pendingAuthCallbacks);
            _pendingAuthCallbacks.Clear();

            if (!authenticated)
            {
                Debug.LogWarning("[OnlineRankingService] 認証に失敗しました (signUp/refresh 両方失敗)");
            }

            foreach (var callback in callbacks)
            {
                callback?.Invoke(authenticated);
            }
        }

        private IEnumerator RefreshTokenCoroutine(string refreshToken, Action<bool> onDone)
        {
            string url = string.Format(CultureInfo.InvariantCulture, RefreshTokenUrlTemplate, RankingSecrets.FirebaseApiKey);
            string body = "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                request.timeout = 10; // スタック時にラッチ解放するため (通信スタックで _authInProgress が固まるのを防ぐ)

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[OnlineRankingService] refresh_token 失敗: {request.error}");
                    onDone?.Invoke(false);
                    yield break;
                }

                string json = request.downloadHandler.text;
                string idToken = ExtractJsonString(json, "id_token");
                string uid = ExtractJsonString(json, "user_id");
                string newRefreshToken = ExtractJsonString(json, "refresh_token");

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(uid))
                {
                    Debug.LogWarning("[OnlineRankingService] refresh_token 応答の解析に失敗しました");
                    onDone?.Invoke(false);
                    yield break;
                }

                _idToken = idToken;
                _uid = uid;
                if (!string.IsNullOrEmpty(newRefreshToken))
                {
                    PlayerPrefs.SetString(RefreshTokenPrefsKey, newRefreshToken);
                }
                PlayerPrefs.SetString(LocalIdPrefsKey, uid);
                PlayerPrefs.Save();
                onDone?.Invoke(true);
            }
        }

        private IEnumerator SignUpCoroutine(Action<bool> onDone)
        {
            string url = string.Format(CultureInfo.InvariantCulture, SignUpUrlTemplate, RankingSecrets.FirebaseApiKey);
            byte[] bodyRaw = Encoding.UTF8.GetBytes("{\"returnSecureToken\":true}");

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10; // スタック時にラッチ解放するため (通信スタックで _authInProgress が固まるのを防ぐ)

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[OnlineRankingService] signUp 失敗: {request.error}");
                    onDone?.Invoke(false);
                    yield break;
                }

                string json = request.downloadHandler.text;
                string idToken = ExtractJsonString(json, "idToken");
                string refreshToken = ExtractJsonString(json, "refreshToken");
                string localId = ExtractJsonString(json, "localId");

                if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(localId))
                {
                    Debug.LogWarning("[OnlineRankingService] signUp 応答の解析に失敗しました");
                    onDone?.Invoke(false);
                    yield break;
                }

                _idToken = idToken;
                _uid = localId;
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    PlayerPrefs.SetString(RefreshTokenPrefsKey, refreshToken);
                }
                PlayerPrefs.SetString(LocalIdPrefsKey, localId);
                PlayerPrefs.Save();
                onDone?.Invoke(true);
            }
        }

        // MARK: - スコア送信コルーチン (Firestore PATCH)

        private IEnumerator SubmitScoreCoroutine(int floor, string characterType, bool retryOn401)
        {
            int clampedFloor = Mathf.Clamp(floor, 1, MaxFloor);
            string characterTypeOrFallback =
                Array.IndexOf(ValidCharacterTypes, characterType) >= 0 ? characterType : "hero";
            string displayName = GetOrCreateDisplayName();
            string timestampIso = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            string url = string.Format(CultureInfo.InvariantCulture, FirestoreDocsUrlTemplate, RankingSecrets.FirebaseProjectId)
                + "/rankings/" + _uid;
            string body = BuildScoreBodyJson(_uid, displayName, clampedFloor, characterTypeOrFallback, timestampIso);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using (var request = new UnityWebRequest(url, "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _idToken);
                request.timeout = 10; // スタック時にラッチ解放するため (通信スタックで _authInProgress が固まるのを防ぐ)

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 401 && retryOn401)
                    {
                        // idToken 期限切れ (~1時間)。1回だけ refresh して再試行する。
                        bool refreshed = false;
                        yield return StartCoroutine(RefreshTokenCoroutine(
                            PlayerPrefs.GetString(RefreshTokenPrefsKey, string.Empty),
                            success => refreshed = success));

                        if (refreshed)
                        {
                            yield return StartCoroutine(SubmitScoreCoroutine(floor, characterType, retryOn401: false));
                            yield break;
                        }
                    }

                    Debug.LogWarning($"[OnlineRankingService] スコア送信失敗: {request.error} (HTTP {request.responseCode})");
                    yield break;
                }

                Debug.Log($"[OnlineRankingService] スコア送信成功: floor={clampedFloor}");
            }
        }

        // MARK: - ランキング取得コルーチン (Firestore runQuery)

        private IEnumerator FetchRankingsCoroutine(Action<List<OnlineRankingEntry>> onResult, bool retryOn401)
        {
            string url = string.Format(CultureInfo.InvariantCulture, FirestoreDocsUrlTemplate, RankingSecrets.FirebaseProjectId)
                + ":runQuery";
            const string body =
                "{\"structuredQuery\":{\"from\":[{\"collectionId\":\"rankings\"}]," +
                "\"orderBy\":[{\"field\":{\"fieldPath\":\"floor\"},\"direction\":\"DESCENDING\"}]," +
                "\"limit\":100}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _idToken);
                request.timeout = 10; // スタック時にラッチ解放するため (通信スタックで _authInProgress が固まるのを防ぐ)

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (request.responseCode == 401 && retryOn401)
                    {
                        bool refreshed = false;
                        yield return StartCoroutine(RefreshTokenCoroutine(
                            PlayerPrefs.GetString(RefreshTokenPrefsKey, string.Empty),
                            success => refreshed = success));

                        if (refreshed)
                        {
                            yield return StartCoroutine(FetchRankingsCoroutine(onResult, retryOn401: false));
                            yield break;
                        }
                    }

                    Debug.LogWarning($"[OnlineRankingService] ランキング取得失敗: {request.error} (HTTP {request.responseCode})");
                    onResult?.Invoke(null);
                    yield break;
                }

                List<OnlineRankingEntry> entries = ParseRunQueryResponse(request.downloadHandler.text, _uid);
                onResult?.Invoke(entries);
            }
        }

        // MARK: - 表示名 (自前生成、初回のみ)

        private string GetOrCreateDisplayName()
        {
            string existing = PlayerPrefs.GetString(DisplayNamePrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(existing)) return existing;

            var rng = new System.Random();
            string adjective = NameAdjectives[rng.Next(NameAdjectives.Length)];
            string creature = NameCreatures[rng.Next(NameCreatures.Length)];
            int number = rng.Next(0, 1000); // 3桁数字 (000-999)
            string generated = adjective + creature + number.ToString("D3", CultureInfo.InvariantCulture);

            PlayerPrefs.SetString(DisplayNamePrefsKey, generated);
            PlayerPrefs.Save();
            return generated;
        }

        // MARK: - JSON body 組み立て (Firestore typed fields)

        private static string BuildScoreBodyJson(string uid, string displayName, int floor, string characterType, string timestampIso)
        {
            var sb = new StringBuilder();
            sb.Append("{\"fields\":{");
            sb.Append("\"userId\":{\"stringValue\":\"").Append(JsonStringUtil.Escape(uid)).Append("\"},");
            sb.Append("\"displayName\":{\"stringValue\":\"").Append(JsonStringUtil.Escape(displayName)).Append("\"},");
            sb.Append("\"floor\":{\"integerValue\":\"").Append(floor.ToString(CultureInfo.InvariantCulture)).Append("\"},");
            sb.Append("\"characterType\":{\"stringValue\":\"").Append(JsonStringUtil.Escape(characterType)).Append("\"},");
            sb.Append("\"timestamp\":{\"timestampValue\":\"").Append(JsonStringUtil.Escape(timestampIso)).Append("\"}");
            sb.Append("}}");
            return sb.ToString();
        }

        // MARK: - runQuery 応答パース (typed fields、JsonUtility 不可のため手動)

        /// <summary>
        /// runQuery の JSON 配列応答から rankings ドキュメントを抽出する。各要素は
        /// {"document":{"fields":{...}}} または (結果末尾等で) {"readTime":"..."} のみの場合があり、
        /// 後者は document が無いためスキップする。
        /// </summary>
        private static List<OnlineRankingEntry> ParseRunQueryResponse(string json, string myUid)
        {
            var results = new List<OnlineRankingEntry>();
            if (string.IsNullOrEmpty(json)) return results;

            foreach (string element in SplitTopLevelJsonObjects(json))
            {
                string fieldsJson = ExtractBracedSection(element, "\"fields\"");
                if (fieldsJson == null) continue; // document なし (readTime のみ) はスキップ

                string userId = ExtractTypedStringField(fieldsJson, "userId");
                if (string.IsNullOrEmpty(userId)) continue; // 必須フィールド欠落は不正データとしてスキップ

                string displayName = ExtractTypedStringField(fieldsJson, "displayName");
                string characterType = ExtractTypedStringField(fieldsJson, "characterType");
                int floor = ExtractTypedIntField(fieldsJson, "floor");

                results.Add(new OnlineRankingEntry
                {
                    UserId = userId,
                    DisplayName = string.IsNullOrEmpty(displayName) ? "名無し" : displayName,
                    Floor = floor,
                    CharacterType = string.IsNullOrEmpty(characterType) ? "hero" : characterType,
                    IsMe = userId == myUid
                });
            }

            return results;
        }

        /// <summary>
        /// トップレベル JSON 配列 "[ {...}, {...} ]" を要素ごとの "{...}" 文字列に分割する。
        /// 文字列リテラル内の "{" "}" "[" "]" は無視する (簡易だが本用途 (固定スキーマ) では十分)。
        /// </summary>
        private static List<string> SplitTopLevelJsonObjects(string json)
        {
            var elements = new List<string>();
            int arrayStart = json.IndexOf('[');
            if (arrayStart < 0) return elements;

            int depth = 0;
            int elementStart = -1;
            bool inString = false;
            bool escape = false;

            for (int i = arrayStart + 1; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }

                if (c == '{')
                {
                    if (depth == 0) elementStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && elementStart >= 0)
                    {
                        elements.Add(json.Substring(elementStart, i - elementStart + 1));
                        elementStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    break; // 配列終端
                }
            }

            return elements;
        }

        /// <summary>"keyLiteral" (例: "\"fields\"") 直後の "{...}" ブロックを波括弧の対応を辿って抽出する。</summary>
        private static string ExtractBracedSection(string json, string keyLiteral)
        {
            int keyIndex = json.IndexOf(keyLiteral, StringComparison.Ordinal);
            if (keyIndex < 0) return null;

            int braceStart = json.IndexOf('{', keyIndex);
            if (braceStart < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = braceStart; i < json.Length; i++)
            {
                char c = json[i];

                if (inString)
                {
                    if (escape) escape = false;
                    else if (c == '\\') escape = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }

                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(braceStart, i - braceStart + 1);
                    }
                }
            }

            return null;
        }

        /// <summary>Firestore typed value {"stringValue":"..."} を key 指定で抽出する。</summary>
        private static string ExtractTypedStringField(string fieldsJson, string key)
        {
            var match = Regex.Match(fieldsJson,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\{\\s*\"stringValue\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*\\}");
            return match.Success ? JsonStringUtil.Unescape(match.Groups[1].Value) : null;
        }

        /// <summary>Firestore typed value {"integerValue":"..."} を key 指定で抽出する (未検出/解析失敗は 0)。</summary>
        private static int ExtractTypedIntField(string fieldsJson, string key)
        {
            var match = Regex.Match(fieldsJson,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\{\\s*\"integerValue\"\\s*:\\s*\"(-?\\d+)\"\\s*\\}");
            if (!match.Success) return 0;
            return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        /// <summary>フラットな JSON トップレベル文字列値 ("key":"value") を抽出する (Auth REST 応答用)。</summary>
        private static string ExtractJsonString(string json, string key)
        {
            var match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return match.Success ? JsonStringUtil.Unescape(match.Groups[1].Value) : null;
        }
    }
}
