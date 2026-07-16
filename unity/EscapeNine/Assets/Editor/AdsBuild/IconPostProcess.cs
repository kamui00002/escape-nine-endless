// IconPostProcess.cs
// App Store 用マーケティングアイコン (1024x1024 / ios-marketing / "Any Appearance") を
// 生成 Xcode プロジェクトの AppIcon.appiconset へ注入する iOS ビルド後処理。
//
// 理由: Unity の iOS Player Settings に App Store 1024 アイコンが未設定のため、生成される
// Images.xcassets/AppIcon.appiconset には 60/76/83.5pt 等の実機用アイコンしか入らず、
// ios-marketing(1024x1024) スロットが欠落する。この状態で archive→altool すると
//   ERROR 91111 "Missing app icon. Include a large app icon as a 1024 by 1024 pixel PNG
//   for the 'Any Appearance' image well ..."
// でアップロード自体が失敗する (2026-07-17 に実際に踏んだ。build22 upload FAILED)。
//
// 素材は Swift 版と同一アイコン (EscapeNine-endless-/.../AppIcon.appiconset/AppIcon.png、
// 1024x1024・アルファ無し=App Store 準拠) を repo 内 Editor/AdsBuild/AppStoreIcon1024.png として
// 同梱し、それを appiconset にコピー + Contents.json に ios-marketing エントリを1件追加する。
// 既存エントリ (iPhone/iPad) は保持し、挿入のみ行う (Unity が player settings に応じて生成する
// 他アイコンを壊さないため)。

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace EscapeNine.AdsBuild.Editor
{
    public static class IconPostProcess
    {
        private const string SourceIconRelPath = "Editor/AdsBuild/AppStoreIcon1024.png"; // Application.dataPath 基準
        private const string MarketingIconFileName = "Icon-Marketing-1024.png";

        // Privacy(101)/Ads(100) より後で走らせる (順序依存は無いが決定的にしておく)。
        [PostProcessBuild(102)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string appIconDir = Path.Combine(
                pathToBuiltProject,
                "Unity-iPhone", "Images.xcassets", "AppIcon.appiconset");

            if (!Directory.Exists(appIconDir))
            {
                UnityEngine.Debug.LogWarning(
                    "[IconPostProcess] AppIcon.appiconset が見つからないため 1024 アイコン注入をスキップ: " + appIconDir);
                return;
            }

            // 1) 1024 アイコン素材を appiconset へコピー。
            string sourceIcon = Path.Combine(UnityEngine.Application.dataPath, SourceIconRelPath);
            if (!File.Exists(sourceIcon))
            {
                UnityEngine.Debug.LogWarning(
                    "[IconPostProcess] 1024 アイコン素材が無いため注入をスキップ: " + sourceIcon);
                return;
            }
            string destIcon = Path.Combine(appIconDir, MarketingIconFileName);
            File.Copy(sourceIcon, destIcon, true);

            // 2) Contents.json に ios-marketing エントリを1件挿入 (既存エントリは保持)。
            string contentsPath = Path.Combine(appIconDir, "Contents.json");
            if (!File.Exists(contentsPath))
            {
                UnityEngine.Debug.LogWarning("[IconPostProcess] Contents.json が無いため注入をスキップ: " + contentsPath);
                return;
            }

            string contents = File.ReadAllText(contentsPath);
            if (contents.Contains("ios-marketing"))
            {
                UnityEngine.Debug.Log("[IconPostProcess] ios-marketing エントリは既存。アイコンのみ差し替え済み。");
                return;
            }

            // "images" 直後の '[' を探し、その直後にマーケティングエントリを挿入する。
            int imagesIdx = contents.IndexOf("\"images\"", System.StringComparison.Ordinal);
            int bracketIdx = imagesIdx >= 0 ? contents.IndexOf('[', imagesIdx) : -1;
            if (bracketIdx < 0)
            {
                UnityEngine.Debug.LogWarning("[IconPostProcess] Contents.json の images 配列が解釈できず注入をスキップ。");
                return;
            }

            string entry =
                "\n\t\t{\n" +
                "\t\t\t\"filename\" : \"" + MarketingIconFileName + "\",\n" +
                "\t\t\t\"idiom\" : \"ios-marketing\",\n" +
                "\t\t\t\"scale\" : \"1x\",\n" +
                "\t\t\t\"size\" : \"1024x1024\"\n" +
                "\t\t},";
            contents = contents.Insert(bracketIdx + 1, entry);
            File.WriteAllText(contentsPath, contents);

            UnityEngine.Debug.Log(
                "[IconPostProcess] App Store 用 1024 アイコン (ios-marketing) を AppIcon.appiconset へ注入しました (ITMS 91111 対策)");
        }
    }
}
#endif
