// ExportCompliancePostProcess.cs
// Info.plist に ITSAppUsesNonExemptEncryption=false を書き込む iOS ビルド後処理。
//
// 理由: Unity 生成の Info.plist にはこのキーが無いため、TestFlight/提出時に
// 「Export Compliance (輸出コンプライアンス) が未回答 = Missing Compliance」となり、
// build を審査提出に添付できない (毎ビルドで手動回答を要求される)。
// 本アプリの通信は Firebase Auth REST / PostHog / AdMob いずれも標準 HTTPS(TLS) のみで、
// 独自・非標準の暗号は使用しない = Apple の「適用除外(exempt)」に該当するため false が正。
// (2026-07-17 build22 の upload 後、Info.plist にキー欠落を確認 → 恒久化。)
//
// ※ 独自暗号を将来追加した場合はこの値の見直しが必要 (その場合は年次自己分類/書類提出が絡む)。

#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace EscapeNine.AdsBuild.Editor
{
    public static class ExportCompliancePostProcess
    {
        [PostProcessBuild(103)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = System.IO.Path.Combine(pathToBuiltProject, "Info.plist");
            if (!System.IO.File.Exists(plistPath))
            {
                UnityEngine.Debug.LogWarning("[ExportCompliancePostProcess] Info.plist が見つからずスキップ: " + plistPath);
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            // 標準 HTTPS のみ = 適用除外。false で TestFlight の Export Compliance 質問を恒久回避。
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            UnityEngine.Debug.Log("[ExportCompliancePostProcess] ITSAppUsesNonExemptEncryption=false を Info.plist に設定 (Export Compliance 恒久回避)");
        }
    }
}
#endif
