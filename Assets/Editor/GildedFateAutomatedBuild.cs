using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GildedFate.Editor
{
    public static class GildedFateAutomatedBuild
    {
        public static void BuildWindows()
        {
            var output=Path.GetFullPath("Builds/VisualCheck/GildedFate.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options=new BuildPlayerOptions
            {
                scenes=new[]{"Assets/Scenes/SampleScene.unity"},
                locationPathName=output,
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development
            };
            var report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Automated visual-check build failed: "+report.summary.result);
        }
    }
}
