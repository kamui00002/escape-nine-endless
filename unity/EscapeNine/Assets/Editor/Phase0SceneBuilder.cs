// Phase0SceneBuilder.cs
// Phase 0 シーンを「クリック無し」でプログラム生成する Editor ツール。
// CLI から呼び出す (完全自動化の要):
//   Unity -batchmode -quit -projectPath <PROJECT> \
//         -executeMethod EscapeNine.EditorTools.Phase0SceneBuilder.Build -logFile -
//
// 生成物: Assets/Scenes/Phase0.unity
//   - "Conductor" (AudioSource + Conductor)  ← Resources/Sounds/BGM/bgm_early を試験ロード
//   - "Phase0Harness" (Phase0Harness, autoStart=true)
//   + Build Settings にシーンを追加

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EscapeNine.Runtime;

namespace EscapeNine.EditorTools
{
    public static class Phase0SceneBuilder
    {
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Phase0.unity";
        private const string BgmResourcePath = "Sounds/BGM/bgm_early"; // Resources 相対 (拡張子なし)

        [MenuItem("EscapeNine/Build Phase 0 Scene")]
        public static void Build()
        {
            Debug.Log("[Phase0] scene build start");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Conductor
            var conductorGo = new GameObject("Conductor");
            var audio = conductorGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            var conductor = conductorGo.AddComponent<Conductor>();

            var clip = Resources.Load<AudioClip>(BgmResourcePath);
            if (clip != null)
            {
                conductor.song = clip;
                audio.clip = clip;
                Debug.Log($"[Phase0] BGM loaded: {BgmResourcePath}");
            }
            else
            {
                Debug.LogWarning(
                    $"[Phase0] BGM が見つかりません: Resources/{BgmResourcePath}.*\n" +
                    "既存 repo の bgm_early を Assets/Resources/Sounds/BGM/ に配置するか、手動で Conductor.song に割り当ててください。");
            }

            // Harness
            var harnessGo = new GameObject("Phase0Harness");
            var harness = harnessGo.AddComponent<Phase0Harness>();
            harness.conductor = conductor;
            harness.autoStart = true;

            // Save
            if (!AssetDatabase.IsValidFolder(SceneDir))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Phase0] scene saved: {ScenePath}");

            // Build Settings 登録
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase0] done");
        }
    }
}
#endif
