using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingThing.EditorTools
{
    // RacingThing > Build Windows, or from a terminal:
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod RacingThing.EditorTools.BuildScript.BuildWindows
    public static class BuildScript
    {
        [MenuItem("Racing Thing/Build Windows", priority = 20)]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/" + GameInfo.Title + ".exe");
        }

        [MenuItem("Racing Thing/Build WebGL", priority = 21)]
        public static void BuildWebGL()
        {
            Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/WebGL");
        }

        [MenuItem("Racing Thing/Open Builds Folder", priority = 40)]
        public static void OpenBuilds()
        {
            Directory.CreateDirectory("Builds");
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds"));
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            ProjectSetup.PrepareForBuild();
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                string msg = target + " support isn't installed. Add it in Unity Hub under Installs > Add modules.";
                Debug.LogError(msg);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorUtility.DisplayDialog("Can't build", msg, "OK");
                return;
            }
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
            });
            var result = report.summary;
            if (result.result == BuildResult.Succeeded)
            {
                Debug.Log("Built " + Path.GetFullPath(path) + " (" + (result.totalSize / 1048576f).ToString("0.0") + " MB)");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(Path.GetFullPath(path));
            }
            else
            {
                Debug.LogError("Build failed: " + result.result + ", " + result.totalErrors + " errors");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
