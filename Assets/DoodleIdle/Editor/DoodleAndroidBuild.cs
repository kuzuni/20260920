using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DoodleIdle.Editor
{
    public static class DoodleAndroidBuild
    {
        // Run in an isolated checkout. Signing passwords are process environment only.
        public static void InternalTestBundle() => BuildAndroid(false);

        // Uses the real Android Google/BACKND flow, signed with the local debug key.
        public static void EmulatorLoginApk() => BuildAndroid(true);

        static void BuildAndroid(bool emulator)
        {
            DoodleServiceConfiguration.Sync();
            string config = "Assets/DoodleIdle/Resources/DoodleIdle/BackendSettings.json";
            if (!File.Exists(config)) throw new BuildFailedException("Provide local BackendSettings.json before building.");
            var settings = JsonUtility.FromJson<DoodleBackendSession.Settings>(File.ReadAllText(config));
            if (string.IsNullOrEmpty(settings.clientAppId) || string.IsNullOrEmpty(settings.signatureKey) || string.IsNullOrEmpty(settings.googleWebClientId))
                throw new BuildFailedException("BACKND and Google login configuration is incomplete.");

            string keyStore = emulator ? "" : Required("DOODLE_ANDROID_KEYSTORE");
            string password = emulator ? "" : Required("DOODLE_ANDROID_PASSWORD");
            string alias = emulator ? "" : Required("DOODLE_ANDROID_ALIAS");
            string output = emulator
                ? Environment.GetEnvironmentVariable("DOODLE_EMULATOR_OUTPUT") ?? "Builds/Android/game20260920-emulator.apk"
                : Environment.GetEnvironmentVariable("DOODLE_ANDROID_OUTPUT") ?? "Builds/Android/game20260920-internal.aab";
            if (!emulator && !File.Exists(keyStore)) throw new BuildFailedException("Upload keystore was not found.");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, DoodleIapCatalog.PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Current Unity no longer builds x86-64 Android; the Play emulator runs
            // ARM64 through its bundled native bridge, matching the release ABI.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // BACKND Google Login 2.3.1 constructs an Android Activity in its Java
            // static initializer. It needs the Java Looper supplied by Activity;
            // GameActivity runs Unity on a native thread without that Looper.
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            if (emulator) {
                // Avoid the host Vulkan/native-bridge crash path in the PC emulator.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            }
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.useCustomKeystore = !emulator;
            PlayerSettings.Android.keystoreName = emulator ? "" : Path.GetFullPath(keyStore);
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasPass = password;
            PlayerSettings.Android.bundleVersionCode = int.TryParse(Environment.GetEnvironmentVariable("DOODLE_ANDROID_VERSION_CODE"), out int code) ? code : 1;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.buildAppBundle = !emulator;
            Directory.CreateDirectory("Assets/Plugins/Android");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JAVA_HOME")))
                Environment.SetEnvironmentVariable("JAVA_HOME", Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK"));
            if (!GooglePlayServices.PlayServicesResolver.ResolveSync(true))
                throw new BuildFailedException("Android dependency resolution failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/DoodleIdle/DoodleLogin.unity", "Assets/DoodleIdle/DoodleIdle.unity" },
                    target = BuildTarget.Android, locationPathName = output, options = emulator ? BuildOptions.Development : BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Android build failed: " + report.summary.result);
                Debug.Log("Android build created: " + output);
            }
            finally { PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = ""; }
        }
        static string Required(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new BuildFailedException("Missing build environment variable: " + name);
            return value;
        }
    }
}
