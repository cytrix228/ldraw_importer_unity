using UnityEditor;

public static class BuildScript
{
    public static void BuildHeadlessLinux()
    {
        string[] scenes = { "Assets/MyScene.unity" }; // Update with your scene paths
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/LinuxHeadless/conv-ldraw", // Desired output path and name
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.EnableHeadlessMode
        };
        BuildPipeline.BuildPlayer(buildPlayerOptions);
    }
}
