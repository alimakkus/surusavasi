using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Otomatik ekran görüntüsü için Windows sürümü derler (Tools/autoshot/run_autoshot.ps1 kullanır).</summary>
public static class AutoShotBuild
{
    public static void Build()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-out");
        string path = i >= 0 ? args[i + 1] : "Builds/AutoShot/Suru.exe";
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 450; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.resizableWindow = true;
        var r = BuildPipeline.BuildPlayer(scenes, path, BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("[AutoShotBuild] " + r.summary.result + " " + path);
        EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
