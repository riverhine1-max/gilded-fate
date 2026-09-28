// Cloud build entry points for GitHub Actions (copied into Assets/Editor by ci/unity-build.sh).
// Usage: Unity -batchmode -quit -executeMethod GildedFate.Editor.GildedFateCiBuild.BuildWebGL -gfOutput <path>
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GildedFate.Editor
{
    public static class GildedFateCiBuild
    {
        private static readonly string[] Scenes={"Assets/Scenes/SampleScene.unity"};

        private static string Arg(string name,string fallback)
        {
            var args=Environment.GetCommandLineArgs();
            for(var i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];
            return fallback;
        }

        private static void Run(BuildTarget target,BuildTargetGroup group,string defaultPath)
        {
            var output=Path.GetFullPath(Arg("-gfOutput",defaultPath));
            var folder=Path.GetDirectoryName(output);if(!string.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
            if(EditorUserBuildSettings.activeBuildTarget!=target)EditorUserBuildSettings.SwitchActiveBuildTarget(group,target);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(group),group==BuildTargetGroup.WebGL?ScriptingImplementation.IL2CPP:ScriptingImplementation.Mono2x);
            var options=new BuildPlayerOptions{scenes=Scenes,locationPathName=output,target=target,targetGroup=group,options=BuildOptions.None};
            var report=BuildPipeline.BuildPlayer(options);
            var summary=report.summary;
            Debug.Log($"[Gilded Fate CI] {target}: {summary.result} · {summary.totalSize/1048576f:0.0} MB · {summary.totalErrors} errors · {summary.totalTime}");
            if(summary.result!=BuildResult.Succeeded)EditorApplication.Exit(1);
        }

        public static void BuildWindows()=>Run(BuildTarget.StandaloneWindows64,BuildTargetGroup.Standalone,"Builds/Windows/GildedFate.exe");

        public static void BuildLinux()=>Run(BuildTarget.StandaloneLinux64,BuildTargetGroup.Standalone,"Builds/Linux/GildedFate.x86_64");

        public static void BuildWebGL()
        {
            // GitHub Pages cannot send Content-Encoding headers, so let the loader decompress gzip itself.
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.nameFilesAsHashes=false;
            PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.maximumMemorySize=4096;
            Run(BuildTarget.WebGL,BuildTargetGroup.WebGL,"Builds/WebGL");
        }
    }
}
