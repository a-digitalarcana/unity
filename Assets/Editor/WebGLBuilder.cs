using UnityEditor;
class WebGLBuilder
{
    static void Build()
    {
        string[] scenes = {"Assets/Scenes/DefaultScene.unity"};

        string pathToDeploy = "Builds/webgl/";

        var report = BuildPipeline.BuildPlayer(scenes, pathToDeploy, BuildTarget.WebGL, BuildOptions.None);
        EditorApplication.Exit(report.summary.totalErrors);
    }
}

