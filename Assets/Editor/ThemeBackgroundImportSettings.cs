using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ThemeBackgroundImportSettings : AssetPostprocessor
{
    private const string Root = "Assets/Resources/ThemeBackgrounds/";
    private const string DimensionsPath = Root + "source-dimensions.json";
    [Serializable] private sealed class SourceDimension { public int theme, width, height; }
    [Serializable] private sealed class SourceDimensions { public SourceDimension[] backgrounds; }

    private void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith(Root, StringComparison.Ordinal))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.sRGBTexture=true;
        importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.ToNearest;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear;
        importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.crunchedCompression=true;
        importer.compressionQuality=85;
        importer.isReadable=false;
        ConfigurePlatform(importer, "Standalone", TextureImporterFormat.DXT1Crunched);
        ConfigurePlatform(importer, "WebGL", TextureImporterFormat.DXT1Crunched);
        ConfigurePlatform(importer, "Android", TextureImporterFormat.ETC_RGB4Crunched);
    }

    private static void ConfigurePlatform(TextureImporter importer, string platform, TextureImporterFormat format)
    {
        var settings = importer.GetPlatformTextureSettings(platform);
        settings.name = platform;
        settings.overridden = true;
        settings.maxTextureSize = 2048;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.crunchedCompression = true;
        settings.compressionQuality = 85;
        importer.SetPlatformTextureSettings(settings);
    }

    private static bool IsBackground(string path) => path.StartsWith(Root + "background_", StringComparison.Ordinal)
        && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsBackground))
            SynchronizeSourceDimensions();
    }

    public static void SynchronizeSourceDimensions()
    {
        var entries = Directory.GetFiles(Root, "background_*.png").Select(path => {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt64() != 0x0a1a0a0d474e5089UL) throw new InvalidDataException("Invalid PNG: " + path);
                stream.Position = 16;
                byte[] size = reader.ReadBytes(8);
                int width = size[0] << 24 | size[1] << 16 | size[2] << 8 | size[3];
                int height = size[4] << 24 | size[5] << 16 | size[6] << 8 | size[7];
                return new SourceDimension { theme = int.Parse(Path.GetFileNameWithoutExtension(path).Substring(11)),
                    width = width, height = height };
            }
        }).OrderBy(entry => entry.theme).ToArray();
        string json = JsonUtility.ToJson(new SourceDimensions { backgrounds = entries }, true) + "\n";
        if (File.Exists(DimensionsPath) && File.ReadAllText(DimensionsPath) == json) return;
        File.WriteAllText(DimensionsPath, json);
        AssetDatabase.ImportAsset(DimensionsPath);
    }

    [MenuItem("Window/Sand/Build/Compress Theme Backgrounds")]
    public static void ConfigureAll()
    {
        SynchronizeSourceDimensions();
        foreach (string path in Directory.GetFiles(Root, "background_*.png"))
            AssetDatabase.ImportAsset(path.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
    }
}
