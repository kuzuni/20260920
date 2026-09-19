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

    }
}
