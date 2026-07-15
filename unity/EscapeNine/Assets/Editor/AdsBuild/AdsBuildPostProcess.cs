// AdsBuildPostProcess.cs
// AdMob/ATT 用の iOS ビルド後処理。EscapeNineATT.mm (Runtime/Ads の ATT ネイティブブリッジ) が
// ATTrackingManager を参照するため、AppTrackingTransparency.framework を UnityFramework ターゲットへ
// 明示リンクする。GMA プラグインの Clang モジュール auto-link では拾われず、
//   ld: Undefined symbols: "_OBJC_CLASS_$_ATTrackingManager", referenced from EscapeNineATT.o
// で BUILD FAILED になることを 2026-07-15 の実機ビルドで確認したための恒久対策
// (それまでは xcodeproj gem での手動追加で凌いでいた)。
//
// weak リンク: EscapeNineATT.mm が @available(iOS 14, *) でガードしているため
// (デプロイターゲットは iOS 15 なので実質常在だが、防御的に weak)。
//
// 隔離用の専用 asmdef (EscapeNine.AdsBuild.Editor) に置く理由: UnityEditor.iOS.Xcode
// (PBXProject) への precompiledReference を既存 EscapeNine.EditorTools へ足すと
// overrideReferences 切替で他参照を巻き添えにするリスクがあるため、依存をこの assembly に閉じ込める。

#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace EscapeNine.AdsBuild.Editor
{
    public static class AdsBuildPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // EscapeNineATT.mm は UnityFramework ターゲットでコンパイルされる (リンクエラーも当該ターゲット)。
            string frameworkTargetGuid = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(frameworkTargetGuid, "AppTrackingTransparency.framework", true /* weak */);

            proj.WriteToFile(projPath);
            UnityEngine.Debug.Log("[AdsBuildPostProcess] AppTrackingTransparency.framework を UnityFramework へ weak リンクしました");
        }
    }
}
#endif
