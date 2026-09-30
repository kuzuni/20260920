using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

using System.IO;

namespace BACKND.Database.Editor
{
    [InitializeOnLoad]
    public static class PackageAssetInstaller
    {
        private const string PackageName = "com.backnd.database";
        private const string SourceFolderName = "TheBackend~";
        private const string TargetFolderName = "TheBackend";
        private const string DefineSymbol = "BACKND_SDK_INSTALLED";

        static PackageAssetInstaller()
        {
            EditorApplication.delayCall += TryInstallAssets;
        }

        private static void TryInstallAssets()
        {
            string targetPath = Path.Combine("Assets", TargetFolderName);

            if (IsAlreadyInstalled(targetPath))
            {
                EnsureScriptingDefineSymbol();
                return;
            }

            string sourcePath = FindSourcePath();

            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarning("[BACKND Database] Could not find TheBackend~ folder.");
                return;
            }

            InstallAssets(sourcePath, targetPath);
        }

        private static void EnsureScriptingDefineSymbol()
        {
            var buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            if (!defines.Contains(DefineSymbol))
            {
                AddScriptingDefineSymbol();
            }
        }

        private static bool IsAlreadyInstalled(string targetPath)
        {
            if (!Directory.Exists(targetPath))
                return false;

            string pluginsPath = Path.Combine(targetPath, "Plugins");
            if (!Directory.Exists(pluginsPath))
                return false;

            // 핵심 파일들 확인
            string[] requiredFiles = new string[]
            {
                Path.Combine(pluginsPath, "Backend.dll"),
                Path.Combine(pluginsPath, "LitJSON.dll"),
                Path.Combine(pluginsPath, "Android", "Backend.aar"),
                Path.Combine(pluginsPath, "Editor", "TheBackendSettingEditor.dll"),
                Path.Combine(pluginsPath, "Settings", "TheBackendSettings.dll")
            };

            foreach (string file in requiredFiles)
            {
                if (!File.Exists(file))
                    return false;
            }

            return true;
        }

        private static string FindSourcePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (packageInfo != null)
            {
                string packageSourcePath = Path.Combine(packageInfo.resolvedPath, SourceFolderName);
                if (Directory.Exists(packageSourcePath))
                {
                    return packageSourcePath;
                }
            }

            string assetsSourcePath = Path.Combine("Assets", "BACKND", "Database", SourceFolderName);
            if (Directory.Exists(assetsSourcePath))
            {
                return Path.GetFullPath(assetsSourcePath);
            }

            return null;
        }

        private static void InstallAssets(string sourcePath, string targetPath)
        {
            try
            {
                EditorUtility.DisplayProgressBar("BACKND Database", "Installing plugins...", 0f);

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                CopyDirectory(sourcePath, targetPath);

                EditorUtility.DisplayProgressBar("BACKND Database", "Refreshing asset database...", 0.9f);

                AssetDatabase.Refresh();

                AddScriptingDefineSymbol();

                EditorUtility.ClearProgressBar();

                Debug.Log($"[BACKND Database] Plugin installed successfully: {targetPath}");
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[BACKND Database] Plugin installation failed: {e.Message}");

                EditorUtility.DisplayDialog(
                    "BACKND Database",
                    $"An error occurred during plugin installation.\n\n{e.Message}",
                    "OK"
                );
            }
        }

        private static void AddScriptingDefineSymbol()
        {
            var buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            if (!defines.Contains(DefineSymbol))
            {
                if (string.IsNullOrEmpty(defines))
                {
                    defines = DefineSymbol;
                }
                else
                {
                    defines = defines + ";" + DefineSymbol;
                }

                PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines);
                Debug.Log($"[BACKND Database] Scripting define symbol added: {DefineSymbol}");
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            var dir = new DirectoryInfo(sourceDir);

            foreach (var subDir in dir.GetDirectories())
            {
                if (subDir.Name.StartsWith("."))
                    continue;

                string targetSubDir = Path.Combine(targetDir, subDir.Name);
                Directory.CreateDirectory(targetSubDir);
                CopyDirectory(subDir.FullName, targetSubDir);
            }

            foreach (var file in dir.GetFiles())
            {
                if (file.Name.StartsWith(".") || file.Extension == ".meta")
                    continue;

                string targetFile = Path.Combine(targetDir, file.Name);

                if (!File.Exists(targetFile))
                {
                    file.CopyTo(targetFile);
                }
            }
        }

        [MenuItem("The Backend/Database/Install Plugins")]
        private static void ManualInstall()
        {
            string sourcePath = FindSourcePath();

            if (string.IsNullOrEmpty(sourcePath))
            {
                EditorUtility.DisplayDialog(
                    "BACKND Database",
                    "Could not find TheBackend~ folder.\n\n" +
                    "Please verify that the package is properly installed.",
                    "OK"
                );
                return;
            }

            string targetPath = Path.Combine("Assets", TargetFolderName);

            if (Directory.Exists(targetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "BACKND Database",
                    $"Assets/{TargetFolderName} folder already exists.\n\n" +
                    "Only new files will be added. (Existing files will be preserved)",
                    "Continue",
                    "Cancel"
                );

                if (!overwrite) return;
            }

            InstallAssets(sourcePath, targetPath);
        }

    }
}
