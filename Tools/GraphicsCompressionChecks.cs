// Run from Assets/Editor in a disposable Unity project copy.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GraphicsCompressionChecks
{
    [Serializable] public sealed class Entry
    {
        public string path, format, webglFormat, androidFormat;
        public int width, height, sourceWidth, sourceHeight;
        public long memoryBytes;
        public float sourceAspect;
        public bool crunched;
    }
    [Serializable] public sealed class Report { public Entry[] textures; }
    public static void Optimize()
    {
        try { ThemeBackgroundImportSettings.ConfigureAll(); Run(); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void Run()
    {
        try
        {
            var entries = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/ThemeBackgrounds" })
                .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] { "Assets/Resources/UI/BoosterAtlas.png" })
                .Select(path => {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (texture == null || importer == null) throw new Exception("Missing texture: " + path);
                    importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
                    float aspect = sourceWidth / (float)sourceHeight;
                    if (path.Contains("ThemeBackgrounds/") && Environment.GetEnvironmentVariable("SAND_GRAPHICS_EXPECT_COMPRESSED") == "1")
                    {
                        int theme = int.Parse(Path.GetFileNameWithoutExtension(path).Substring(11));
                        aspect = ThemeBackgroundAssets.GetSourceAspect(theme);
                        if (Mathf.Abs(aspect - sourceWidth / (float)sourceHeight) > .000001f)
                            throw new Exception("Source aspect ratio changed: " + path);
                        if (texture.format != TextureFormat.DXT1Crunched || !importer.crunchedCompression || importer.isReadable)
                            throw new Exception("Background did not import compressed: " + path + " " + texture.format);
                        var webgl = importer.GetPlatformTextureSettings("WebGL");
                        var android = importer.GetPlatformTextureSettings("Android");
                        if (!webgl.overridden || webgl.format != TextureImporterFormat.DXT1Crunched
                            || !android.overridden || android.format != TextureImporterFormat.ETC_RGB4Crunched)
                            throw new Exception("Platform compression settings missing: " + path);
                        string decoded = Environment.GetEnvironmentVariable("SAND_GRAPHICS_DECODED_OUTPUT");
                        if (!string.IsNullOrEmpty(decoded))
                        {
                            Directory.CreateDirectory(decoded);
                            var previous = RenderTexture.active;
                            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                            try
                            {
                                Graphics.Blit(texture, target);
                                RenderTexture.active = target;
                                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                                pixels.Apply();
                                File.WriteAllBytes(Path.Combine(decoded, Path.GetFileName(path)), pixels.EncodeToPNG());
                            }
                            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(pixels); }
                        }
                    }
                    return new Entry { path = path, width = texture.width, height = texture.height,
                        sourceWidth = sourceWidth, sourceHeight = sourceHeight, sourceAspect = aspect,
                        format = texture.format.ToString(), memoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),
                        webglFormat = importer.GetAutomaticFormat("WebGL").ToString(),
                        androidFormat = importer.GetAutomaticFormat("Android").ToString(), crunched = importer.crunchedCompression };
                }).ToArray();
            string output = Environment.GetEnvironmentVariable("SAND_GRAPHICS_AUDIT_OUTPUT");
            if (string.IsNullOrEmpty(output)) throw new Exception("Set SAND_GRAPHICS_AUDIT_OUTPUT.");
            File.WriteAllText(output, JsonUtility.ToJson(new Report { textures = entries }, true));
            Debug.Log("GRAPHICS_COMPRESSION_AUDIT_PASS count=" + entries.Length);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
