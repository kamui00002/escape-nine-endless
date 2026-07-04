// UrpPackageInstaller.cs
// なぜ必要か: bootstrap.sh の -createProject が作る素のプロジェクトの manifest.json には
// com.unity.render-pipelines.universal (URP) が含まれない。Phase 4.5 の描画層
// (Editor/UrpBootstrap.cs、Runtime/Stage/StagePostFx.cs 等) は URP 型
// (UniversalRenderPipelineAsset / Bloom / Volume 等) を直接参照するため、パッケージ不在だと
// それらのアセンブリごとコンパイルに失敗する (UrpBootstrap.cs は EditorTools 全体を巻き込む)。
//
// 本クラスは URP 非依存の専用 asmdef (EscapeNine.PackageBootstrap) に置くことで、URP を参照する
// アセンブリがコンパイル不能でも自分だけは必ずコンパイル・実行され、manifest.json に依存を
// 自動追記して自己修復する (UguiPackageInstaller と同じ発想、設計 Wave 0「manifest 編集後
// Client.Resolve() 必須 — 既知の罠」への対策。2026-07-04 /review-full P2)。
//
// 手動で直す場合: <PROJECT>/Packages/manifest.json の dependencies に
//   "com.unity.render-pipelines.universal": "17.3.0"
// を 1 行追加して Unity を再起動する (それだけで本クラスは以後 no-op になる)。

#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EscapeNine.PackageBootstrap
{
    public static class UrpPackageInstaller
    {
        // Unity 6000.3 同梱の URP バージョン (UrpBootstrap.cs / StagePostFx.cs が前提とする API 世代)
        private const string PackageId = "com.unity.render-pipelines.universal";
        private const string PackageVersion = "17.3.0";

        [InitializeOnLoadMethod]
        private static void EnsureUrpDependency()
        {
            try
            {
                string manifestPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
                if (!File.Exists(manifestPath)) return; // 想定外レイアウト (UPM 埋め込み等) は触らない

                string json = File.ReadAllText(manifestPath);
                if (json.Contains("\"" + PackageId + "\"")) return; // 導入済み → no-op

                // "dependencies": { の直後に 1 行挿入する (JSON パーサ非依存の最小変更。
                // Unity が生成する manifest.json はこの形で安定しているため十分)。
                var regex = new Regex("(\"dependencies\"\\s*:\\s*\\{)");
                if (!regex.IsMatch(json))
                {
                    Debug.LogWarning(
                        "[EscapeNine] manifest.json に dependencies ブロックが見つからないため自動追記を中止。" +
                        $"手動で \"{PackageId}\": \"{PackageVersion}\" を追加してください: {manifestPath}");
                    return;
                }

                string patched = regex.Replace(
                    json,
                    "$1\n    \"" + PackageId + "\": \"" + PackageVersion + "\",",
                    1);
                File.WriteAllText(manifestPath, patched);

                Debug.Log(
                    $"[EscapeNine] {PackageId} {PackageVersion} を Packages/manifest.json に自動追加しました。" +
                    "パッケージ解決後の再コンパイルで URP 描画コード (UrpBootstrap / Stage) が有効になります。");

                // 即時解決を要求 (Editor 対話起動時)。batchmode -quit ではプロセス終了までに
                // 完了しない場合があるが、次回起動時に manifest から解決されるため問題ない。
                UnityEditor.PackageManager.Client.Resolve();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[EscapeNine] {PackageId} の自動追加に失敗: {e.Message}\n" +
                    $"Packages/manifest.json の dependencies に \"{PackageId}\": \"{PackageVersion}\" を手動追加してください。");
            }
        }
    }
}
#endif
