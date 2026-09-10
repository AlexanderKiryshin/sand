using System.IO;
using GameAnalyticsSDK;
using UnityEditor;
using UnityEngine;
using YG;

public static class WebAnalyticsSetup
{
    [MenuItem("Window/Sand/Analytics/Create or Select Web Settings")]
    public static void Configure()
    {
        const string path = "Assets/PluginYourGames/Resources/SettingsYG2.asset";
        var yg = AssetDatabase.LoadAssetAtPath<InfoYG>(path);
        if (yg == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            yg = ScriptableObject.CreateInstance<InfoYG>();
            AssetDatabase.CreateAsset(yg, path);
        }
        // Apply also when YG's first-start hook has already created default platform settings.
        // Mirra owns platform SDK, ads and gameplay. Your Games supplies only Metrica.
        yg.Basic.platform = null; yg.Basic.autoApplySettings = false;
        yg.Basic.autoPauseGame = false; yg.Basic.autoGRA = false;
        yg.Basic.editEventSystem = false; yg.Basic.archivingBuild = false;
        yg.Basic.syncInitSDK = false; yg.Basic.loadSceneIfSDKLate = false;
        yg.Basic.logInEditor = false;
        yg.Metrica.enable = true; yg.Metrica.useYandexMetrica = true; yg.Metrica.log = false;
        EditorUtility.SetDirty(yg); AssetDatabase.SaveAssets();
        YG.EditorScr.DefineSymbols.PlatformDefineSymbols();
        const string gaPath = "Assets/Resources/GameAnalytics/Settings.asset";
        var ga = AssetDatabase.LoadAssetAtPath<GameAnalyticsSDK.Setup.Settings>(gaPath);
        if (ga == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(gaPath));
            ga = ScriptableObject.CreateInstance<GameAnalyticsSDK.Setup.Settings>();
            AssetDatabase.CreateAsset(ga, gaPath);
        }
        int platform = ga.Platforms.IndexOf(RuntimePlatform.WebGLPlayer);
        if (platform < 0) { ga.AddPlatform(RuntimePlatform.WebGLPlayer); platform = ga.Platforms.IndexOf(RuntimePlatform.WebGLPlayer); }
        ga.Build[platform] = PlayerSettings.bundleVersion;
        EditorUtility.SetDirty(ga); AssetDatabase.SaveAssets();
        if (!Application.isBatchMode) Selection.objects = new Object[] { yg, ga };
        bool metricaReady = yg.Metrica.enable && yg.Metrica.metricaCounterID > 0;
        bool gaReady = ga.GetGameKey(platform).Length == 32 && ga.GetSecretKey(platform).Length == 40;
        Directory.CreateDirectory("tasks");
        File.WriteAllText("tasks/web-analytics-settings-status.txt", "Your Games Metrica counter: " + (metricaReady ? "configured" : "missing")
            + "\nGameAnalytics WebGL keys: " + (gaReady ? "configured" : "missing") + "\nNo credentials are written to this report.\n");
    }
}
