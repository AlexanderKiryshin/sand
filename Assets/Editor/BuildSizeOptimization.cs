using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildSizeOptimization
{
    public const string AtlasPath = "Assets/Resources/UI/BoosterAtlas.png";

    [MenuItem("Window/Sand/Build/Apply Size Optimizations")]
    public static void Configure()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        if (importer == null) throw new InvalidOperationException("Booster atlas is missing.");
        importer.maxTextureSize = 512;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.maxTextureSize = 512;
        defaults.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(defaults);
        var android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = 512;
        android.format = TextureImporterFormat.ETC2_RGBA8;
        android.compressionQuality = 100;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();

        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
        AssetDatabase.SaveAssets();
        Debug.Log("BUILD_SIZE_SETTINGS_APPLIED");
    }

    // Run with -buildTarget Android in a disposable project copy.
    public static void BuildAndroid()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Start Unity with -buildTarget Android.");
        Configure();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        if (atlas == null || atlas.width != 512 || atlas.height != 512)
            throw new InvalidOperationException("Atlas did not import at 512x512.");
        var removed = new[] { "UI/Boosters/Booster_Magnet", "UI/Boosters/Booster_Magnet.png.tmp",
            "UI/Boosters/Booster_Transparent", "UI/Boosters/Booster_Shuffle", "UI/BusesIcons/Cofffe" };
        foreach (string path in removed)
            if (Resources.Load<Texture2D>(path) != null) throw new InvalidOperationException("Unused texture remains: " + path);
        CampaignAnalyticsChecks.Run();
        LevelStorageChecks.Run();

        string output = Environment.GetEnvironmentVariable("SAND_ANDROID_OUTPUT");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set SAND_ANDROID_OUTPUT to the new APK path.");
        output = Path.GetFullPath(output);
        if (File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite an existing APK: " + output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        EditorUserBuildSettings.buildAppBundle = false;
        var options = new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.CompressWithLz4HC | BuildOptions.DetailedBuildReport
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Android build failed: " + report.summary.result);

        string[] resources = report.packedAssets.SelectMany(p => p.contents)
            .GroupBy(p => p.sourceAssetPath)
            .Select(g => new { path = g.Key, size = g.Sum(p => (long)p.packedSize) })
            .OrderByDescending(p => p.size)
            .Select(p => p.size + "\t" + p.path).ToArray();
        Directory.CreateDirectory("tasks");
        File.WriteAllLines("tasks/build-size-packed-assets.tsv", new[] { "bytes\tasset" }.Concat(resources));
        File.WriteAllText("tasks/build-size-build.txt", "APK: " + output + "\nBytes: " + new FileInfo(output).Length
            + "\nAtlas: " + atlas.width + "x" + atlas.height + " " + atlas.format
            + "\nManaged stripping: " + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android)
            + "\nIL2CPP code generation: " + PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Android) + "\n");
        Debug.Log("BUILD_SIZE_ANDROID_PASS bytes=" + new FileInfo(output).Length);
    }
}
