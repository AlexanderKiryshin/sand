using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class YoutubeBuild
{
    [Serializable] private class BuildPreferences { public Group[] groupCollection; }
    [Serializable] private class Group { public string key; public Values group; }
    [Serializable] private class Values { public Value[] stringCollection; }
    [Serializable] private class Value { public string key; public string value; }
    [MenuItem("Window/Sand/Build/YouTube Playables")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Select WebGL or start Unity with -buildTarget WebGL.");
        string configuration = File.ReadAllText("Assets/Resources/MirraSDK5/Preferences.json");
        var preferences = JsonUtility.FromJson<BuildPreferences>(configuration);
        var selected = preferences.groupCollection.First(g => g.key == "Build").group.stringCollection
            .First(v => v.key == "Configuration").value;
        if (selected != "YouTubePlayablesConfiguration")
            throw new InvalidOperationException("Select YouTubePlayablesConfiguration for Build.");
        YoutubePortChecks.Run();
        var target = NamedBuildTarget.WebGL;
        if (!PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Contains("YOUTUBE_PLAYABLES"))
            throw new InvalidOperationException("YOUTUBE_PLAYABLES must be set before compilation.");
        PlayerSettings.WebGL.template = "PROJECT:MirraYoutubePlayablesTemplate";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.initialMemorySize = 64;
        PlayerSettings.WebGL.maximumMemorySize = 512;
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        string output = Environment.GetEnvironmentVariable("SAND_YOUTUBE_OUTPUT");
        if (string.IsNullOrEmpty(output)) output = "Builds/YouTube/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new InvalidOperationException("Output already exists: " + output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.WebGL, locationPathName = output,
            options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("YouTube build failed: " + report.summary.result);
        var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories);
        if (files.Length > 8000 || files.Any(f => new FileInfo(f).Length >= 30L * 1024 * 1024)
            || files.Sum(f => new FileInfo(f).Length) >= 250L * 1024 * 1024)
            throw new InvalidOperationException("YouTube bundle exceeds a file/count/total size limit.");
        Directory.CreateDirectory("tasks/youtube");
        File.WriteAllLines("tasks/youtube/build-assets.tsv", new[] { "bytes\tasset" }.Concat(report.packedAssets
            .SelectMany(p => p.contents).GroupBy(p => p.sourceAssetPath)
            .Select(g => new { path = g.Key, size = g.Sum(p => (long)p.packedSize) })
            .OrderByDescending(p => p.size).Select(p => p.size + "\t" + p.path)));
        Debug.Log("YOUTUBE_BUILD_PASS " + output);
    }
}
