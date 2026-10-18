using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CivilGameJam.Cards.Editor
{
    public static class WindowsDesktopBuild
    {
        [Serializable]
        private sealed class Receipt
        {
            public string result, executable, unityVersion, platform, createdUtc, backend;
            public string[] scenes;
            public double seconds;
            public ulong bytes;
            public int errors, warnings;
            public bool nativeResolution, resizableWindow, developmentBuild;
        }

        [MenuItem("Build/Windows Desktop (İlkBuild)")]
        public static void BuildFirstDesktop()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Build(Path.Combine(desktop, "İlkBuild", "İlkBuild.exe"));
        }

        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-civilBuildOutput");
            if (index < 0 || index + 1 >= args.Length)
                throw new BuildFailedException("Pass -civilBuildOutput with the full Windows executable path.");
            Build(args[index + 1]);
        }

        private static void Build(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Stop Play Mode before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Windows x64 build support is not installed in this Unity editor.");
            output = Path.GetFullPath(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The Windows build output must end in .exe.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("Enable at least one scene in Build Settings.");

            // The authored CanvasScaler fits the UI to the display. Use the monitor's native
            // resolution in borderless fullscreen, with a 1080p fallback for windowed launches.
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });
            var summary = report.summary;
            var receipt = new Receipt
            {
                result = summary.result.ToString(), executable = output,
                unityVersion = Application.unityVersion, platform = "Windows x64",
                createdUtc = DateTime.UtcNow.ToString("O"), scenes = scenes,
                backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
                seconds = summary.totalTime.TotalSeconds, bytes = summary.totalSize,
                errors = summary.totalErrors, warnings = summary.totalWarnings,
                nativeResolution = PlayerSettings.defaultIsNativeResolution,
                resizableWindow = PlayerSettings.resizableWindow, developmentBuild = false
            };
            var receiptPath = Path.Combine(Path.GetDirectoryName(output), "BuildInfo.json");
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
            Debug.Log("WINDOWS_DESKTOP_BUILD " + JsonUtility.ToJson(receipt));
            if (summary.result != BuildResult.Succeeded || !File.Exists(output))
                throw new BuildFailedException("Windows build failed. Inspect the build log and BuildInfo.json.");
        }
    }
}
