using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PolyRush.EditorTools
{
    /// <summary>
    /// One-click builds.
    ///   Menu:          PolyRush > Build Windows (64-bit)
    ///   Command line:  Unity.exe -batchmode -quit -projectPath . -executeMethod PolyRush.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        const string WindowsPath = "Builds/Windows/PolyRush.exe";
        const string WebGLPath = "Builds/WebGL";

        [MenuItem("PolyRush/Build Windows (64-bit)", priority = 20)]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, WindowsPath);
        }

        [MenuItem("PolyRush/Build WebGL (for website, needs WebGL module)", priority = 21)]
        public static void BuildWebGL()
        {
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, WebGLPath);
        }

        [MenuItem("PolyRush/Open Builds Folder", priority = 40)]
        public static void OpenBuilds()
        {
            Directory.CreateDirectory("Builds");
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds"));
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            ProjectSetup.PrepareSceneForBuild();
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                string msg = "PolyRush: the " + target + " build module is not installed. Add it in Unity Hub (Installs > Add modules).";
                Debug.LogError(msg);
                if (!Application.isBatchMode) EditorUtility.DisplayDialog("PolyRush build", msg, "OK");
                else EditorApplication.Exit(1);
                return;
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("PolyRush: build succeeded (" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB) -> " + Path.GetFullPath(path));
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.GetFullPath(path));
            }
            else
            {
                Debug.LogError("PolyRush: build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
