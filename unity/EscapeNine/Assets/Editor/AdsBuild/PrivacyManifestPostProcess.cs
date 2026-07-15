// PrivacyManifestPostProcess.cs
// app レベル PrivacyInfo.xcprivacy を生成してメイン App ターゲットへ追加する iOS ビルド後処理。
//
// 理由 (docs/review-readiness-unity.md R1): 本アプリは AdMob + ATT で実際に「トラッキング」する
// (実機で ATT Authorized → personalized 広告を確認済) ため、Apple は app のプライバシーマニフェストに
//   NSPrivacyTracking = true   かつ   NSPrivacyTrackingDomains ≥ 1 件 (実在の追跡ドメイン)
// を要求する。これが無い / 空だと submit 時 ~20 秒で ITMS-91064「Invalid Binary」自動却下になる
// (Obsidian: phc で build22/23/24 と3回焼き直した実績。空配列もキー削除も「ドメイン0」で同じ却下)。
// Unity 生成の UnityFramework/PrivacyInfo は NSPrivacyTracking=false、GMA pod のは tracking=None のため、
// app レベルで追跡宣言するマニフェストを本 PostProcess が用意する。
//
// 追跡ドメイン: googleads.g.doubleclick.net (AdMob の配信/追跡。phc build24 で自動検証突破の実績ドメイン)。
// PostHog(us.i.posthog.com)/Firebase Auth は ATT の「トラッキング」ではない (匿名分析・認証) ため含めない。
// Unity Ads メディエーションは未導入なので Unity ドメインも不要 (規則は実在ドメイン1件で充足)。
//
// 収集データ: Device ID を ThirdPartyAdvertising 目的で Tracking 収集 (ATT/AdMob の実態。ASC App Privacy の
// 「Device ID → トラッキングに使用=はい」と整合させる)。required-reason API は app が使う UserDefaults(CA92.1)。

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace EscapeNine.AdsBuild.Editor
{
    public static class PrivacyManifestPostProcess
    {
        private const string PrivacyFileName = "PrivacyInfo.xcprivacy";

        // AdsBuildPostProcess (100) より後で走らせる (順序依存は無いが決定的にしておく)。
        [PostProcessBuild(101)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            // 1) app レベル PrivacyInfo.xcprivacy を書き出す。
            string privacyPath = Path.Combine(pathToBuiltProject, PrivacyFileName);
            File.WriteAllText(privacyPath, BuildPrivacyManifestXml());

            // 2) メイン App ターゲット (Unity-iPhone) のリソースに追加する
            //    (UnityFramework ではなく App 本体に置くのが app レベルマニフェストの定位置)。
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            string fileGuid = proj.AddFile(PrivacyFileName, PrivacyFileName, PBXSourceTree.Source);
            proj.AddFileToBuild(mainTargetGuid, fileGuid);

            proj.WriteToFile(projPath);
            UnityEngine.Debug.Log("[PrivacyManifestPostProcess] app レベル PrivacyInfo.xcprivacy を生成し App ターゲットへ追加しました (NSPrivacyTracking=true + AdMob 追跡ドメイン)");
        }

        private static string BuildPrivacyManifestXml()
        {
            // NSPrivacyTracking=true / TrackingDomains 非空 (ITMS-91064 対策の要)。
            // 収集データ: Device ID を Tracking + ThirdPartyAdvertising で。API: UserDefaults(CA92.1)。
            return
"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
"<plist version=\"1.0\">\n" +
"<dict>\n" +
"  <key>NSPrivacyTracking</key>\n" +
"  <true/>\n" +
"  <key>NSPrivacyTrackingDomains</key>\n" +
"  <array>\n" +
"    <string>googleads.g.doubleclick.net</string>\n" +
"  </array>\n" +
"  <key>NSPrivacyCollectedDataTypes</key>\n" +
"  <array>\n" +
"    <dict>\n" +
"      <key>NSPrivacyCollectedDataType</key>\n" +
"      <string>NSPrivacyCollectedDataTypeDeviceID</string>\n" +
"      <key>NSPrivacyCollectedDataTypeLinked</key>\n" +
"      <false/>\n" +
"      <key>NSPrivacyCollectedDataTypeTracking</key>\n" +
"      <true/>\n" +
"      <key>NSPrivacyCollectedDataTypePurposes</key>\n" +
"      <array>\n" +
"        <string>NSPrivacyCollectedDataTypePurposeThirdPartyAdvertising</string>\n" +
"      </array>\n" +
"    </dict>\n" +
"  </array>\n" +
"  <key>NSPrivacyAccessedAPITypes</key>\n" +
"  <array>\n" +
"    <dict>\n" +
"      <key>NSPrivacyAccessedAPIType</key>\n" +
"      <string>NSPrivacyAccessedAPICategoryUserDefaults</string>\n" +
"      <key>NSPrivacyAccessedAPITypeReasons</key>\n" +
"      <array>\n" +
"        <string>CA92.1</string>\n" +
"      </array>\n" +
"    </dict>\n" +
"  </array>\n" +
"</dict>\n" +
"</plist>\n";
        }
    }
}
#endif
