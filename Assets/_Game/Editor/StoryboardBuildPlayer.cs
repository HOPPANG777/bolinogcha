using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class StoryboardBuildPlayer
{
    const string ScenePath = "Assets/_Game/Scenes/VZScene.unity";
    const string OutputPath = "Builds/Windows/BolinogchaStoryboard.exe";

    [MenuItem("Tools/Bolinogcha/Build Storyboard Windows Player")]
    public static void BuildWindowsPlayer()
    {
        StoryboardSceneBuilder.PrepareVzStoryboardScene();

        string directory = Path.GetDirectoryName(OutputPath);
        if (string.IsNullOrEmpty(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            //throw new BuildFailedException($"Storyboard Windows build failed: {summary.result}");
        }

        Debug.Log($"Storyboard Windows build complete: {Path.GetFullPath(OutputPath)}");
    }
}
