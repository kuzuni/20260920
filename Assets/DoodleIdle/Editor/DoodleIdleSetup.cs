using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleIdle.Editor
{
    public sealed class DoodleTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/DoodleIdle/Resources/DoodleIdle/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
        }
    }

    public static class DoodleIdleSetup
    {
        public const string ScenePath = "Assets/DoodleIdle/DoodleIdle.unity";

        [MenuItem("Doodle Idle/Create or Open Prototype")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) return;
            AssetDatabase.Refresh();
            foreach (string asset in new[] { "Characters", "Dirt" })
                AssetDatabase.ImportAsset("Assets/DoodleIdle/Resources/DoodleIdle/" + asset + ".png", ImportAssetOptions.ForceUpdate);
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Doodle Idle Prototype").AddComponent<DoodleIdleGame>();
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().orthographic = true;
            camera.GetComponent<Camera>().orthographicSize = 8.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Doodle Idle prototype scene ready. Press Play.");
        }

        [MenuItem("Doodle Idle/Build Windows Prototype")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            Directory.CreateDirectory("Builds/DoodleIdle");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/DoodleIdle/DoodleIdle.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Doodle Idle build failed: " + report.summary.result);
            Debug.Log("Doodle Idle Windows build succeeded.");
        }

        [MenuItem("Doodle Idle/Run Combat Smoke Check")]
        public static void SmokeCheck()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            else if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("DoodleSmoke", true);
            SessionState.SetFloat("DoodleSmokeStarted", (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void InstallChecker()
        {
            EditorApplication.update -= Monitor;
            EditorApplication.update += Monitor;
        }

        static void Monitor()
        {
            if (!SessionState.GetBool("DoodleSmoke", false)) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("DoodleSmokeStarted", 0) > 180)
            { Finish("FAIL: Smoke test timed out."); return; }
            if (!EditorApplication.isPlaying) return;
            var game = UnityEngine.Object.FindFirstObjectByType<DoodleIdleGame>();
            if (!game || !game.Ready) return;
            Time.timeScale = 3;
            if (game.Elapsed < 65) return;
            string report = game.Diagnostics();
            bool passed = game.EnemyCount >= 20 && game.EnemyCount <= 80 && game.Kills > 60 && game.Refills > 0 && game.DashCasts >= 10 && game.StonesLaunched > 15 && game.BananaHits > 0 && game.SlashHits > 0 && game.DashHits > 0;
            game.TogglePause();
            passed &= game.paused;
            game.TogglePause();
            passed &= !game.paused;
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/combat-smoke.json", report);
            ScreenCapture.CaptureScreenshot("Documentation/prototype-gameplay.png");
            Finish((passed ? "PASS: " : "FAIL: ") + report);
        }

        static void Finish(string message)
        {
            SessionState.SetBool("DoodleSmoke", false);
            Time.timeScale = 1;
            File.WriteAllText("Documentation/combat-smoke-result.txt", message);
            if (message.StartsWith("PASS")) Debug.Log(message); else Debug.LogError(message);
        }
    }
}
