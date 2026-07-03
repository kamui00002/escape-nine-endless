// UguiPackageInstaller.cs
// なぜ必要か: bootstrap.sh の -createProject が作る素のプロジェクトの manifest.json には
// com.unity.ugui (uGUI: Text/Image/Button/ScrollRect/EventSystem) が含まれない。
// 本作の UI 層 (EscapeNine.Runtime の UI/**) は uGUI 前提のため、パッケージ不在だと
// アセンブリごとコンパイルに失敗する。
//
// 本クラスは EscapeNine.Runtime / EditorTools から独立した専用 asmdef
// (EscapeNine.PackageBootstrap — uGUI 非依存) に置くことで、UI 側がコンパイル不能な
// 状態でも自分だけは必ずコンパイル・実行され、manifest.json に依存を自動追記して
// 自己修復する (bootstrap.sh の test-framework 追記と同じ発想の Editor 内蔵版)。
//
// 手動で直す場合: <PROJECT>/Packages/manifest.json の dependencies に
//   "com.unity.ugui": "2.0.0"
// を 1 行追加して Unity を再起動する (それだけで本クラスは以後 no-op になる)。

#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EscapeNine.PackageBootstrap
{
    public static class UguiPackageInstaller
    {
        // Unity 6000.x の built-in パッケージバージョン
        private const string PackageId = "com.unity.ugui";
        private const string PackageVersion = "2.0.0";

        [InitializeOnLoadMethod]
        private static void EnsureUguiDependency()
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
                    "パッケージ解決後の再コンパイルで UI コード (EscapeNine.Runtime) が有効になります。");

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
