// Copy into Assets/Editor in a disposable Unity project and run ShopLayoutChecks.Run.
// Disable BuildSizeSmokeChecks.cs there: this check uses the same in-memory save flag.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ShopLayoutChecks
{
    const string Key = "Sand.ShopLayoutChecks";
    public static string Output => Environment.GetEnvironmentVariable("SAND_SHOP_LAYOUT_OUTPUT") ?? "Temp/ShopLayoutChecks";
    static ShopLayoutChecks() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("Sand.BuildSizeChecks", true);
        SessionState.SetBool(Key, true);
        SetResolution(1920, 1080);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying
            && UnityEngine.Object.FindObjectOfType<ShopLayoutRunner>() == null)
            new GameObject("ShopLayoutRunner").AddComponent<ShopLayoutRunner>();
    }
    public static void SetResolution(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
        int index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null)
            + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            new object[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "Shop layout check" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
    }
    public static void Finish(bool success, string report)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "status.txt"), (success ? "PASS\n" : "FAIL\n") + report);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(success ? 0 : 1);
    }
}

public sealed class ShopLayoutRunner : MonoBehaviour
{
    string report = "";
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        var checks = Check();
        while (true)
        {
            object step;
            try { if (!checks.MoveNext()) break; step = checks.Current; }
            catch (Exception error) { ShopLayoutChecks.Finish(false, report + error); yield break; }
            yield return step;
        }
        ShopLayoutChecks.Finish(true, report);
    }
    IEnumerator Check()
    {
        Directory.CreateDirectory(ShopLayoutChecks.Output);
        var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720), new Vector2Int(540, 960), new Vector2Int(768, 1266) };
        for (int index = 0; index < sizes.Length; index++)
        {
            var size = sizes[index];
            if (index > 0)
            {
                ShopLayoutChecks.SetResolution(size.x, size.y);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Time.timeScale = 1;
                SceneManager.LoadScene("GameScene");
            }
            for (int frame = 0; frame < 40; frame++) yield return null;
            Require(GameProgressStore.IsPreview, "Expected isolated saves.");
            Require(Screen.width == size.x && Screen.height == size.y, "Wrong Game View resolution: " + Screen.width + "x" + Screen.height);
            var tabs = FindObjectsOfType<CosmeticShopTabs>(true).Single();
            tabs.OpenCategory(ThemeCatalog.Part.Background);
            for (int frame = 0; frame < 5; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            var card = (RectTransform)GameObject.Find("ShopCard").transform;
            var viewport = (RectTransform)card.Find("ThemeCard/Viewport");
            var scroll = viewport.GetComponent<ScrollRect>();
            var tiles = scroll.content.GetComponentsInChildren<Button>()
                .Select(button => (RectTransform)button.transform)
                .OrderByDescending(tile => tile.anchoredPosition.y).ThenBy(tile => tile.anchoredPosition.x).ToArray();
            var canvas = card.GetComponentInParent<Canvas>();
            string label = size.x + "x" + size.y;
            report += label + ": shop=" + card.rect.size + ", scale=" + canvas.scaleFactor + ", viewport=" + viewport.rect.size + ", tile=" + tiles[0].rect.size + "\n";
            Debug.Log("SHOP_LAYOUT " + report);
            ScreenCapture.CaptureScreenshot(Path.Combine(ShopLayoutChecks.Output, label + ".png"));
            for (int frame = 0; frame < 4; frame++) yield return null;
            Require(viewport.rect.height >= tiles[0].rect.height + 24, "Background scroll viewport clips even a single complete row at " + label);
            Require(viewport.rect.width >= card.rect.width * .85f, "Background scroll viewport is too narrow at " + label);
            var corners = new Vector3[4];
            card.GetWorldCorners(corners);
            Require(corners[0].y >= -1 && corners[2].y <= Screen.height + 1, "Shop exceeds screen height at " + label);
            foreach (float position in new[] { 1f, 0f })
            {
                scroll.verticalNormalizedPosition = position;
                Canvas.ForceUpdateCanvases();
                var tile = position == 1 ? tiles.First() : tiles.Last();
                tile.GetWorldCorners(corners);
                float bottom = viewport.InverseTransformPoint(corners[0]).y;
                float top = viewport.InverseTransformPoint(corners[2]).y;
                Require(bottom >= viewport.rect.yMin - 1 && top <= viewport.rect.yMax + 1, "First/last background row is clipped at " + label);
            }
            foreach (var part in new[] { ThemeCatalog.Part.Conveyor, ThemeCatalog.Part.Casings })
            {
                tabs.Select((int)part + 2);
                Require(viewport.rect.height >= 230, "Other cosmetics lost scroll space at " + label);
            }
            tabs.GetComponent<RewardedShop>().CloseShop();
        }
    }
}
