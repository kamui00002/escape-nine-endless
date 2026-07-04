// OverlapAudit.cs
// Phase 4.5 Wave 1 (TextMeshPro 化) に伴い、従来 script-execute 経由の使い捨てスクリプトだった
// 重なり監査を恒久化したエディタツール。TMP 化で行送り・実効高さが legacy Text と変わるため
// (design doc: docs/unity-phase4-5-visual-upgrade-design.md R4)、preferredHeight のはみ出し分を
// 含めた実効矩形で比較するよう最初から TMP 前提で実装する。
//
// 前提・制約 (呼び出し側が必ず把握すること):
//   - 本来の使い方は Play モード (design doc: PlayMode オートパイロット・スモーク → 重なり監査 の順)。
//     App.Awake → Router.Register が全画面を一括ビルド済みで、CanvasScaler も実際の
//     Game View 解像度で解決されているため、px 単位の重なり判定が意味を持つ。
//   - Edit モード (シーンを開いただけで BuildUI 未実行、または -batchmode -executeMethod
//     経由) でも動くよう、子が 0 件の画面にはこの場で BuildUI() を直接呼ぶフォールバックを
//     持つ。BuildUI() は UIFactory/UITheme の呼び出しのみで実行時 API (App.I 等) に
//     依存しないため Edit モードでも安全に呼べる (逆に OnShow() は App.I に依存する画面が
//     多いため、本ツールは意図的に OnShow を呼ばない)。
//     ただし Edit モード/batchmode には Game View が無く Screen.width/height が実機と
//     食い違うことがあるため、CanvasScaler 依存の px 数値は目安に過ぎない。
//     この経路はレイアウトの構造的な健全性チェック (重なりの有無の傾向) 用と割り切り、
//     このフォールバックで自分がビルドした子は監査直後に DestroyImmediate で必ず破棄する
//     (「.unity に UI を焼かない」規約を壊さないため。Active 状態を戻すだけでは
//     childCount が 0→非0 のまま残ってしまうので不十分)。
//     Play モードでは通常 Router が既にビルド済みで、このフォールバックが働くのは
//     App.Awake 未実行の極めて早いタイミングに限られる。その場合は破棄すると
//     ScreenBase._built フラグとの整合が壊れるため、破棄は行わない
//     (Play モードはそもそもシーンに保存されないため焼き付きリスクが無い)。
//   - シーンは保存しない。一時アクティブ化した画面は監査後に元の Active 状態へ戻す。
//
// CLI 呼び出し例 (構造チェックのみ、px 数値は参考値):
//   Unity -batchmode -quit -projectPath <PROJECT> \
//         -executeMethod EscapeNine.EditorTools.OverlapAudit.RunAndLog -logFile -
// 権威ある実行方法: Play モードで Menu > EscapeNine/Overlap Audit を実行すること。
//
// 2026-07-04 追記 (Wave 1 ゲート 22 件 NG 調査): 「実効可視性」を判定してから重なり比較するよう改良。
// 方針: 本ツールは排他ペアを「両方を強制可視にして監査する」ことはしない。あくまで
// 「その画面状態で実際に見えるもの」だけを重なり判定の対象にする。採用した可視性判定機構は
// コードベースを網羅的に調査して実際に使われている 2 つだけ (使っていない機構は追加しない):
//   (1) GameObject.activeInHierarchy — 既存の GetComponentsInChildren<TMP_Text>(false) が担う。
//   (2) TMP_Text.color.a (アルファ) — 旧 uGUI 盤面 GridCellWidget の FogMark/XMark 用に導入
//       (AlphaVisibilityThreshold)。この 2 ラベルは常に SetActive(true) のまま alpha フェード
//       だけで出現/消失を表現する設計だった。旧 uGUI 盤面は Phase 4.5 W5 で削除済み
//       (3D 盤面 TileView のマークはワールド空間 TMP で ScreenBase 配下に無いため本監査対象外)
//       だが、alpha フェード式の uGUI 要素が今後追加された場合の汎用安全網としてフィルタは保持する。
// なお TMP_Text.enabled のトグルや CanvasGroup による全消し (alpha=0) は本コードベースには
// 存在しない (grep 済み) ため、判定機構には含めない。
//
// 一方、ScreenRouter.Register() は全画面を BuildUI() 直後に gameObject.SetActive(false) で
// 待機させ (ScreenRouter.cs:85)、Show() は SetActive(true) → OnShow(payload) を同一フレームで
// 呼ぶ (ScreenRouter.cs:114-115、間にレンダーパスは挟まらない)。そのため「BuildUI 直後・
// OnShow 未実行」の状態はプレイヤーが実際に見ることが物理的にない状態だが、本ツールは
// 構造監査のため screen.gameObject.SetActive(true) を強制する (AuditScreen 内)。結果、
// ApplyData()/RefreshAll() 等が SetActive で相互排他にする要素ペア (例: 自己ベストバッジ/
// 「ベスト: N階」表記、購入済みバッジ/価格ボタン) が「まだ両方アクティブ」に見えてしまう
// ことがある。これは activeInHierarchy 判定の限界ではなく、OnShow 起動前のスナップショットを
// 見ているという監査手順側の特性なので、コード証拠つきで KnownExclusivePairs に登録し
// 除外する (レイアウトの当て逃げ修正はしない)。


#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using EscapeNine.Runtime.UI;

namespace EscapeNine.EditorTools
{
    public static class OverlapAudit
    {
        /// <summary>両軸でこの px (= Screen Space Overlay Canvas ではワールド単位と等価) を超えて重なったら NG。</summary>
        private const float OverlapThresholdPx = 8f;

        /// <summary>
        /// この値以下の TMP_Text.color.a は「実質見えていない」として重なり判定の対象から除外する。
        /// 元は旧 uGUI 盤面 GridCellWidget の FogMark/XMark (常時 SetActive(true)、alpha フェード
        /// だけで出現/消失) のための閾値 (旧 uGUI 盤面は W5 で削除済み。汎用安全網として保持 —
        /// 詳細はファイル冒頭コメント参照)。0 ではなくわずかに正の値にしているのは、フェード中の
        /// 「ごく僅かに残った alpha」まで拾って偽陽性を出さないため (完全な 0 を要求すると
        /// 浮動小数の丸め誤差で拾い漏れる恐れがある)。
        /// </summary>
        private const float AlphaVisibilityThreshold = 0.02f;

        private const string ResultFileName = "overlap-audit-result.txt";

        /// <summary>
        /// コード調査で「SetActive により相互排他である」と確認済みのラベルペア。
        /// ここに載るのは全て、ApplyData()/RefreshAll() 等 OnShow 経由の再描画メソッド内で
        /// 一方が true のとき他方が必ず false になることをソースで確認したものだけ
        /// (推測での登録は禁止。Evidence に根拠行を明記する)。
        /// SuffixA/SuffixB は GetPath() が返す相対パスの末尾セグメント列 (親からの部分一致)。
        /// RequireSameGrandparent=true の場合、SuffixA/SuffixB を取り除いた残りのパス
        /// (= 共通の親コンテナ) が一致するペアのみにマッチさせる (Shop の商品行のように
        /// 同一パターンが複数インスタンス存在する画面で、無関係な組み合わせを誤って
        /// 除外しないための保険)。
        /// </summary>
        private readonly struct ExclusionRule
        {
            public readonly ScreenId Screen;
            public readonly string SuffixA;
            public readonly string SuffixB;
            public readonly bool RequireSameGrandparent;
            public readonly string Evidence;

            public ExclusionRule(ScreenId screen, string suffixA, string suffixB, string evidence,
                bool requireSameGrandparent = false)
            {
                Screen = screen;
                SuffixA = suffixA;
                SuffixB = suffixB;
                RequireSameGrandparent = requireSameGrandparent;
                Evidence = evidence;
            }
        }

        private static readonly ExclusionRule[] KnownExclusivePairs =
        {
            // 自己ベスト演出 (Swift: personalBestSection)。新記録時のバッジと非更新時の
            // 「ベスト: N階」キャプションは排他表示。
            // 証拠: ResultScreen.cs ApplyData() —
            //   _bestBadge.gameObject.SetActive(_data.IsNewBest);
            //   bool showPrevBest = !_data.IsNewBest && _data.PreviousBest > 0;
            //   _bestCaptionLabel.gameObject.SetActive(showPrevBest);
            new ExclusionRule(ScreenId.Result, "PersonalBestBadge/Label", "PreviousBestCaption",
                "ResultScreen.cs ApplyData() — _bestBadge/_bestCaptionLabel は _data.IsNewBest で相互排他"),

            // タブ切替 (プレイ履歴 / クラウド)。RefreshAll() が選択タブに応じて
            // 一方だけを SetActive(true) にする。
            // 証拠: RankingScreen.cs RefreshAll() —
            //   if (_selectedTab == Tab.Cloud) { ...; _cloudPanel.SetActive(true); return; }
            //   ... _emptyPanel.SetActive(entries.Count == 0);
            new ExclusionRule(ScreenId.Ranking, "EmptyPanel/Title", "CloudPanel/Title",
                "RankingScreen.cs RefreshAll() — _emptyPanel/_cloudPanel は _selectedTab で相互排他"),
            new ExclusionRule(ScreenId.Ranking, "EmptyPanel/Caption", "CloudPanel/Caption",
                "RankingScreen.cs RefreshAll() — _emptyPanel/_cloudPanel は _selectedTab で相互排他"),

            // 商品行の価格ボタン ↔ 購入済バッジ (4 商品共通パターン)。RequireSameGrandparent で
            // 同一商品行内のペアだけにマッチさせる。
            // 証拠: ShopScreen.cs RefreshItems() —
            //   w.PriceButtonRoot.SetActive(!purchased);
            //   w.PurchasedBadge.SetActive(purchased);
            new ExclusionRule(ScreenId.Shop, "PriceButton/Label", "PurchasedBadge/Label",
                "ShopScreen.cs RefreshItems() — PriceButtonRoot/PurchasedBadge は purchased で相互排他",
                requireSameGrandparent: true),

            // 広告削除の購入済バッジ ↔ 購入ボタン。
            // 証拠: SettingsScreen.cs RefreshDynamic() —
            //   if (_adPurchasedBadge != null) _adPurchasedBadge.SetActive(removed);
            //   if (_adBuyButton != null) _adBuyButton.SetActive(!removed);
            new ExclusionRule(ScreenId.Settings, "AdPurchasedBadge/Label", "AdBuyButton/Label",
                "SettingsScreen.cs RefreshDynamic() — _adPurchasedBadge/_adBuyButton は player.AdRemoved で相互排他"),
        };

        [MenuItem("EscapeNine/Overlap Audit")]
        public static void RunAndLog()
        {
            string report = RunAll();
            Debug.Log("[OverlapAudit]\n" + report);
        }

        /// <summary>
        /// 現在ロード中のシーンにある全 ScreenBase を監査し、結果文字列を返す。
        /// 同時にプロジェクト直下 (Directory.GetParent(Application.dataPath)) の
        /// overlap-audit-result.txt へ書き出す (BuildScripts.cs のマーカーファイル方式を踏襲)。
        /// </summary>
        public static string RunAll()
        {
            ScreenBase[] screens = UnityEngine.Object.FindObjectsByType<ScreenBase>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var sb = new StringBuilder();
            sb.AppendLine("Overlap Audit — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine($"実行モード: {(Application.isPlaying ? "Play Mode" : "Edit Mode")}");
            sb.AppendLine($"閾値: 両軸 {OverlapThresholdPx}px 超で NG (祖先子孫ペアは除外)");
            if (!Application.isPlaying)
            {
                sb.AppendLine("注意: Edit モードでの実行は Game View 解像度が実機と食い違う場合があり、"
                    + "px 数値は参考値。構造的な重なりの傾向チェックとして扱い、"
                    + "確定判定は Play モードでの再実行を推奨。");
            }
            sb.AppendLine();

            if (screens.Length == 0)
            {
                sb.AppendLine("ScreenBase が 1 つも見つからない。Assets/Scenes/Main.unity を開いているか確認すること。");
                string emptyResult = sb.ToString();
                WriteResult(emptyResult);
                return emptyResult;
            }

            // FindObjectsByType の順序は不定なため、ScreenId 宣言順 (MainSceneBuilder の生成順と一致) に揃える。
            Array.Sort(screens, (a, b) => a.Id.CompareTo(b.Id));

            int totalConflicts = 0;
            foreach (ScreenBase screen in screens)
            {
                totalConflicts += AuditScreen(screen, sb);
            }

            sb.AppendLine();
            sb.AppendLine(totalConflicts == 0 ? "RESULT: ALL CLEAN" : $"RESULT: NG (重なり {totalConflicts} 件)");
            sb.AppendLine();
            sb.AppendLine("注記: TutorialScreen はページ切替 (ShowPage) が private のため、"
                + "本ツールは BuildUI 直後の状態 (未着手・キャプション類は空文字) しか監査できない。"
                + "6 ページの内容込みの重なりはチュートリアルは手動確認とすること。");

            string result = sb.ToString();
            WriteResult(result);
            return result;
        }

        /// <summary>1 画面を監査し、結果を sb に追記する。戻り値は NG 件数。</summary>
        private static int AuditScreen(ScreenBase screen, StringBuilder sb)
        {
            GameObject go = screen.gameObject;
            bool wasActive = go.activeSelf;

            // Edit モードでシーンを開いただけ (BuildUI 未実行) のフォールバック。
            // Play モードでこの分岐に入るのは App.Awake 未実行の極めて早いタイミングのみ
            // (通常は Router が既にビルド済み)。
            bool builtHere = false;
            if (screen.transform.childCount == 0)
            {
                screen.BuildUI();
                builtHere = true;

                // ScreenBase.BuildUI() は private _built フラグで二重ビルドを防ぐガードを
                // 持つため、同一 Edit セッション (ドメインリロード無し) で本ツールを
                // 2 回目以降に実行すると、前回の DestroyImmediate で子は 0 に戻したが
                // _built==true は残ったままなので BuildUI() が no-op になり得る。
                // その場合 childCount はまだ 0 のままになるため、
                // 「CLEAN (0 labels)」という偽陰性を返さず警告として明示する。
                if (screen.transform.childCount == 0)
                {
                    sb.AppendLine($"[{screen.Id}] WARNING: BuildUI() 後も子が 0 件。"
                        + "同一 Edit セッションで本ツールを複数回実行した場合、"
                        + "ScreenBase._built フラグにより再ビルドされていない可能性がある"
                        + " (Unity 再起動またはドメインリロード後に再実行すること)。");
                    go.SetActive(wasActive);
                    return 0;
                }
            }

            go.SetActive(true);
            Canvas.ForceUpdateCanvases();

            // 現在アクティブ (= 実際に見える) な TMP_Text だけを対象にする。
            // トースト/オーバーレイ等の SetActive(false) 済み要素は、この静的スナップショットの
            // 対象外 (別状態でのみ見えるため、同時に重なることはない)。
            TMP_Text[] labels = screen.GetComponentsInChildren<TMP_Text>(false);

            var boxes = new List<(TMP_Text label, Rect worldAabb)>(labels.Length);
            int alphaHidden = 0;
            foreach (TMP_Text label in labels)
            {
                // 実効可視性フィルタ (2): color.a ≈ 0 は「実際には見えない」として除外する。
                // 元は旧 uGUI 盤面 GridCellWidget の FogMark/XMark のための機構 (W5 で削除済み、
                // 汎用安全網として保持。詳細は本ファイル冒頭コメント参照)。
                if (label.color.a <= AlphaVisibilityThreshold)
                {
                    alphaHidden++;
                    continue;
                }
                boxes.Add((label, WorldAabb(label)));
            }

            var conflicts = new List<string>();
            var excluded = new List<string>();
            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Transform ti = boxes[i].label.transform;
                    Transform tj = boxes[j].label.transform;
                    if (ti.IsChildOf(tj) || tj.IsChildOf(ti)) continue; // 祖先子孫ペアは除外

                    Rect a = boxes[i].worldAabb;
                    Rect b = boxes[j].worldAabb;
                    float overlapW = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float overlapH = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);

                    if (overlapW > OverlapThresholdPx && overlapH > OverlapThresholdPx)
                    {
                        string pathA = GetPath(screen.transform, ti);
                        string pathB = GetPath(screen.transform, tj);

                        // 実効可視性フィルタ (1) の延長: activeInHierarchy 判定だけでは拾えない
                        // 「OnShow 未実行の BuildUI 直後状態」由来の偽陽性を、コード証拠つきの
                        // 既知排他ペアとして除外する (本ファイル冒頭コメント参照。レイアウトは触らない)。
                        if (IsKnownExclusive(screen.Id, pathA, pathB, out string evidence))
                        {
                            excluded.Add(string.Format(
                                "{0} × {1} (排他表示のため除外。根拠: {2})", pathA, pathB, evidence));
                            continue;
                        }

                        conflicts.Add(string.Format(
                            "{0} × {1} (重なり {2:F0}x{3:F0}px)",
                            pathA, pathB, overlapW, overlapH));
                    }
                }
            }

            if (conflicts.Count == 0)
            {
                sb.AppendLine($"[{screen.Id}] CLEAN ({boxes.Count} labels, alpha非表示 {alphaHidden} 件除外)");
            }
            else
            {
                sb.AppendLine($"[{screen.Id}] NG x{conflicts.Count} ({boxes.Count} labels, alpha非表示 {alphaHidden} 件除外)");
                foreach (string c in conflicts) sb.AppendLine("  - " + c);
            }
            if (excluded.Count > 0)
            {
                sb.AppendLine($"[{screen.Id}] 除外 (排他表示、証拠つき確認済み): {excluded.Count} 件");
                foreach (string e in excluded) sb.AppendLine("  - " + e);
            }

            go.SetActive(wasActive);

            // Edit モードで自分が BuildUI() した子は、シーンに UI を焼き付けないよう
            // 監査直後に必ず破棄する (「.unity に UI を焼かない」規約を壊さないため)。
            // Play モードではシーン自体が保存されないため焼き付きリスクが無く、
            // 逆に破棄すると ScreenBase._built フラグとの整合が壊れるため破棄しない。
            if (builtHere && !Application.isPlaying)
            {
                for (int i = screen.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(screen.transform.GetChild(i).gameObject);
                }
            }

            return conflicts.Count;
        }

        /// <summary>
        /// TMP_Text の実効矩形をワールド空間の軸平行境界ボックスへ変換する。
        /// rect を preferredHeight のはみ出し分だけ (上下均等に) 拡張してから 4 隅を変換し、
        /// 変換後の min/max を取る (回転が無い前提の本プロジェクトでは厳密、あっても安全側の近似になる)。
        /// </summary>
        private static Rect WorldAabb(TMP_Text label)
        {
            RectTransform rt = label.rectTransform;
            Rect local = rt.rect;

            float overflow = Mathf.Max(0f, label.preferredHeight - local.height);
            if (overflow > 0f)
            {
                local = new Rect(local.x, local.y - overflow * 0.5f, local.width, local.height + overflow);
            }

            Vector3[] corners = new Vector3[4];
            corners[0] = rt.TransformPoint(new Vector3(local.xMin, local.yMin, 0f));
            corners[1] = rt.TransformPoint(new Vector3(local.xMin, local.yMax, 0f));
            corners[2] = rt.TransformPoint(new Vector3(local.xMax, local.yMax, 0f));
            corners[3] = rt.TransformPoint(new Vector3(local.xMax, local.yMin, 0f));

            float minX = corners[0].x, maxX = corners[0].x;
            float minY = corners[0].y, maxY = corners[0].y;
            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// (pathA, pathB) が KnownExclusivePairs のいずれかにマッチするか判定する (順不同)。
        /// マッチしたら evidence にそのルールの根拠文字列を返す。
        /// </summary>
        private static bool IsKnownExclusive(ScreenId screenId, string pathA, string pathB, out string evidence)
        {
            foreach (ExclusionRule rule in KnownExclusivePairs)
            {
                if (rule.Screen != screenId) continue;
                if (MatchesRule(rule, pathA, pathB) || MatchesRule(rule, pathB, pathA))
                {
                    evidence = rule.Evidence;
                    return true;
                }
            }
            evidence = null;
            return false;
        }

        private static bool MatchesRule(ExclusionRule rule, string pathA, string pathB)
        {
            if (!PathEndsWithSegments(pathA, rule.SuffixA) || !PathEndsWithSegments(pathB, rule.SuffixB))
            {
                return false;
            }

            if (!rule.RequireSameGrandparent) return true;

            string prefixA = StripSuffixSegments(pathA, rule.SuffixA);
            string prefixB = StripSuffixSegments(pathB, rule.SuffixB);
            return prefixA != null && prefixA == prefixB;
        }

        /// <summary>path が (セグメント境界を尊重して) suffix で終わるか判定する。</summary>
        private static bool PathEndsWithSegments(string path, string suffix)
        {
            if (path == suffix) return true;
            return path.EndsWith("/" + suffix, StringComparison.Ordinal);
        }

        /// <summary>path から末尾の suffix セグメント列を取り除いた「共通の親パス」を返す (マッチしない場合 null)。</summary>
        private static string StripSuffixSegments(string path, string suffix)
        {
            if (path == suffix) return string.Empty;
            if (path.EndsWith("/" + suffix, StringComparison.Ordinal))
            {
                return path.Substring(0, path.Length - suffix.Length - 1);
            }
            return null;
        }

        /// <summary>画面ルートからの相対パス (レポートの可読性のため)。</summary>
        private static string GetPath(Transform root, Transform t)
        {
            if (t == root) return t.name;

            var stack = new List<string>();
            Transform cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return string.Join("/", stack);
        }

        private static void WriteResult(string content)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, ResultFileName);
            File.WriteAllText(path, content);
        }
    }
}
#endif
