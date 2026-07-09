using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ImageToLevelConverterWindow : EditorWindow
{
    private enum ConversionMode
    {
        SingleImage,
        Tiles
    }

    private class ConversionItem
    {
        public Texture2D sourceTexture;
        public string sourceAssetPath;
        public string levelName;
        public int tileX;
        public int tileY;
        public string spriteName;
        public int gridWidth = 32;
        public int gridHeight = 32;
        public int colorCount = 2;
        public bool autoColorCount = true;
        public int previewScale = 8;
        public bool autoBackgroundColor = true;
        public Color backgroundColor = Color.white;
        public SandColorPalette targetPalette;
        public Color32 detectedBackgroundColor;
        public bool isGenerated;
        public bool includeInLevels = true;

        public Texture2D previewTexture;
        public Texture2D sourcePreviewTexture;
        public Color32[] palette = new Color32[0];
        public byte[] cells = new byte[0];
        public BucketSpawn[] buckets = new BucketSpawn[0];

        public void DisposePreview()
        {
            if (previewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(previewTexture);
                previewTexture = null;
            }
        }

        public void DisposeSourcePreview()
        {
            if (sourcePreviewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(sourcePreviewTexture);
                sourcePreviewTexture = null;
            }
        }

        public void DisposeAll()
        {
            DisposePreview();
            DisposeSourcePreview();
        }
    }

    private readonly List<ConversionItem> items = new List<ConversionItem>();
    private int selectedIndex = -1;
    private Vector2 listScroll;
    private Vector2 detailScroll;
    private ConversionMode conversionMode = ConversionMode.SingleImage;

    private ConversionItem tileSourceItem;
    private bool tileResultsReady;

    private string tileLevelName = "TiledLevel";
    private int tilesX = 2;
    private int tilesY = 2;
    private int tileCellWidth = 16;
    private int tileCellHeight = 16;

    private Texture2D composedPreviewTexture;
    private Color32[] composedPalette = new Color32[0];
    private byte[] composedCells = new byte[0];
    private BucketSpawn[] composedBuckets = new BucketSpawn[0];
    private bool composedIsGenerated;

    private byte colorReplaceSourceIndex;
    private ColorReplaceScope colorReplaceScope = ColorReplaceScope.None;
    private int colorReplaceTileIndex = -1;

    private enum ColorReplaceScope
    {
        None,
        Composed,
        Tile
    }

    private int defaultGridWidth = 32;
    private int defaultGridHeight = 32;
    private int defaultColorCount = 2;
    private bool defaultAutoColorCount = true;
    private int defaultPreviewScale = 8;
    private Color defaultBackgroundColor = Color.white;
    private bool defaultAutoBackgroundColor = true;
    private SandColorPalette defaultTargetPalette;
    private LevelDatabase targetLevelDatabase;

    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
    private const string DefaultLevelDatabasePath = "Assets/Levels/LevelDatabase.asset";

    [MenuItem("Window/Sand/Image to Level")]
    public static void ShowWindow()
    {
        ImageToLevelConverterWindow window = GetWindow<ImageToLevelConverterWindow>("Image to Level");
        window.minSize = new Vector2(960f, 640f);
    }

    private void OnEnable()
    {
        if (targetLevelDatabase == null)
        {
            targetLevelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DefaultLevelDatabasePath);
        }

        ApplySegmentDefaultsForNextLevel();
    }

    private void OnDisable()
    {
        ClearAllItems();
        DisposeComposedPreview();
    }

    private void OnGUI()
    {
        DrawModeSelector();
        EditorGUILayout.Space(4f);
        DrawToolbar();
        EditorGUILayout.Space(4f);
        DrawDefaultSettings();
        EditorGUILayout.Space(8f);

        EditorGUILayout.BeginHorizontal();
        DrawItemList();
        if (conversionMode == ConversionMode.Tiles)
        {
            DrawTileCompositionDetail();
        }
        else
        {
            DrawSelectedItemDetail();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModeSelector()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Mode", GUILayout.Width(40f));
        ConversionMode newMode = (ConversionMode)EditorGUILayout.EnumPopup(conversionMode);
        if (newMode != conversionMode)
        {
            conversionMode = newMode;
            ClearAllItems();
        }

        if (conversionMode == ConversionMode.Tiles)
        {
            EditorGUILayout.LabelField(
                "Use Unity Sprite Editor: Texture Type Sprite, Mode Multiple.",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button(conversionMode == ConversionMode.Tiles ? "Load Image" : "Load Folder", EditorStyles.toolbarButton))
        {
            if (conversionMode == ConversionMode.Tiles)
            {
                LoadTileSourceImage();
            }
            else
            {
                LoadFolder();
            }
        }

        if (GUILayout.Button("Add Texture", EditorStyles.toolbarButton))
        {
            AddSingleTexture();
        }

        using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= items.Count))
        {
            if (GUILayout.Button("Remove", EditorStyles.toolbarButton))
            {
                RemoveSelectedItem();
            }
        }

        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(conversionMode == ConversionMode.Tiles ? tileSourceItem == null : items.Count == 0))
        {
            if (GUILayout.Button(conversionMode == ConversionMode.Tiles ? "Compose Level" : "Generate All", EditorStyles.toolbarButton))
            {
                if (conversionMode == ConversionMode.Tiles)
                {
                    ComposeTiles();
                }
                else
                {
                    GenerateAll();
                }
            }

            if (GUILayout.Button(conversionMode == ConversionMode.Tiles ? "Save All" : "Save All", EditorStyles.toolbarButton))
            {
                if (conversionMode == ConversionMode.Tiles)
                {
                    SaveComposedLevel(showDialog: true);
                }
                else
                {
                    SaveAllGenerated();
                }
            }
        }

        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Remove all loaded images?", "Clear", "Cancel"))
            {
                ClearAllItems();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDefaultSettings()
    {
        EditorGUILayout.LabelField("Default Settings (for new imports)", EditorStyles.boldLabel);

        if (conversionMode == ConversionMode.Tiles)
        {
        EditorGUILayout.BeginHorizontal();
            tileLevelName = EditorGUILayout.TextField("Level Name", tileLevelName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            tileCellWidth = EditorGUILayout.IntSlider(
                "Cells Per Tile W",
                tileCellWidth,
                LevelData.MinTileCellDimension,
                LevelData.MaxTileCellDimension);
            tileCellHeight = EditorGUILayout.IntSlider(
                "Cells Per Tile H",
                tileCellHeight,
                LevelData.MinTileCellDimension,
                LevelData.MaxTileCellDimension);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                string.Format(
                    "Result grid: {0}×{1} cells (from Unity sprite sheet)",
                    tilesX * tileCellWidth,
                    tilesY * tileCellHeight),
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            defaultGridWidth = EditorGUILayout.IntSlider(
                "Grid Width",
                defaultGridWidth,
                LevelData.MinGridDimension,
                LevelData.MaxGridDimension);
            defaultGridHeight = EditorGUILayout.IntSlider(
                "Grid Height",
                defaultGridHeight,
                LevelData.MinGridDimension,
                LevelData.MaxGridDimension);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        defaultTargetPalette = (SandColorPalette)EditorGUILayout.ObjectField(
            "Color Palette",
            defaultTargetPalette,
            typeof(SandColorPalette),
            false);
        targetLevelDatabase = (LevelDatabase)EditorGUILayout.ObjectField(
            "Level Database",
            targetLevelDatabase,
            typeof(LevelDatabase),
            false);
        if (GUI.changed)
        {
            ApplySegmentDefaultsForNextLevel();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawColorCountField(ref defaultColorCount, ref defaultAutoColorCount, defaultTargetPalette);
        defaultPreviewScale = EditorGUILayout.IntSlider("Preview Scale", defaultPreviewScale, 4, 16);
        defaultAutoBackgroundColor = EditorGUILayout.Toggle("Auto Background", defaultAutoBackgroundColor);
        using (new EditorGUI.DisabledScope(defaultAutoBackgroundColor))
        {
            defaultBackgroundColor = EditorGUILayout.ColorField("Background", defaultBackgroundColor);
        }
        if (defaultTargetPalette != null)
        {
            DrawTargetPalettePreview(defaultTargetPalette);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (conversionMode == ConversionMode.SingleImage)
        {
            using (new EditorGUI.DisabledScope(selectedIndex < 0 || selectedIndex >= items.Count))
            {
                if (GUILayout.Button("Apply Defaults to Selected", GUILayout.Height(22f)))
                {
                    ApplyDefaultsToItem(items[selectedIndex]);
                    GeneratePreview(items[selectedIndex]);
                }
            }
        }

        using (new EditorGUI.DisabledScope(conversionMode == ConversionMode.Tiles ? tileSourceItem == null : items.Count == 0))
        {
            if (conversionMode == ConversionMode.SingleImage)
            {
                if (GUILayout.Button("Apply Defaults to All", GUILayout.Height(22f)))
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        ApplyDefaultsToItem(items[i]);
                    }
                }

                if (GUILayout.Button("Generate All Images", GUILayout.Height(30f)))
                {
                    GenerateAll();
                }
            }
            else
            {
                if (GUILayout.Button("Compose Level", GUILayout.Height(30f)))
                {
                    ComposeTiles();
                }
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawItemList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(260f));
        if (conversionMode == ConversionMode.Tiles)
        {
            int tileCount = items.Count;
            int includedCount = CountIncludedItems();
            EditorGUILayout.LabelField(
                string.Format("Source + {0} tile(s) ({1} included)", tileCount, includedCount),
                EditorStyles.boldLabel);
            DrawIncludeAllButtons();

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
            if (tileSourceItem != null)
            {
                DrawTileListRow(tileSourceItem, -1, "Source", tileSourceItem.isGenerated ? "ready" : "—", showIncludeToggle: false);
            }

            for (int i = 0; i < items.Count; i++)
            {
                ConversionItem item = items[i];
                string status = item.isGenerated
                    ? string.Format("({0},{1}) {2}c", item.tileX, item.tileY, item.palette != null ? item.palette.Length : 0)
                    : string.Format("({0},{1})", item.tileX, item.tileY);
                DrawTileListRow(item, i, item.levelName, status, showIncludeToggle: true);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        int includedImages = CountIncludedItems();
        EditorGUILayout.LabelField(
            string.Format("Images ({0}, {1} included)", items.Count, includedImages),
            EditorStyles.boldLabel);
        DrawIncludeAllButtons();

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
        for (int i = 0; i < items.Count; i++)
        {
            DrawSingleImageListRow(items[i], i);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawIncludeAllButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All", EditorStyles.miniButtonLeft))
        {
            SetAllItemsIncluded(true);
        }

        if (GUILayout.Button("None", EditorStyles.miniButtonRight))
        {
            SetAllItemsIncluded(false);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SetAllItemsIncluded(bool included)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].includeInLevels = included;
        }

        Repaint();
    }

    private int CountIncludedItems()
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].includeInLevels)
            {
                count++;
            }
        }

        return count;
    }

    private void DrawSingleImageListRow(ConversionItem item, int index)
    {
        bool isSelected = index == selectedIndex;
        Rect rowRect = GUILayoutUtility.GetRect(248f, 48f);
        if (Event.current.type == EventType.Repaint)
        {
            if (!item.includeInLevels)
            {
                EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.15f));
            }

            GUIStyle style = isSelected ? "SelectionRect" : GUIStyle.none;
            style.Draw(rowRect, false, false, isSelected, false);
        }

        Rect toggleRect = new Rect(rowRect.x + 4f, rowRect.y + 15f, 18f, 18f);
        bool include = EditorGUI.Toggle(toggleRect, item.includeInLevels);
        if (include != item.includeInLevels)
        {
            item.includeInLevels = include;
            Repaint();
        }

        if (Event.current.type == EventType.MouseDown
            && rowRect.Contains(Event.current.mousePosition)
            && !toggleRect.Contains(Event.current.mousePosition))
        {
            selectedIndex = index;
            GUI.FocusControl(null);
            Repaint();
        }

        Rect thumbRect = new Rect(rowRect.x + 26f, rowRect.y + 4f, 40f, 40f);
        if (item.sourceTexture != null)
        {
            GUI.DrawTexture(thumbRect, item.sourceTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f));
        }

        Rect labelRect = new Rect(rowRect.x + 72f, rowRect.y + 4f, rowRect.width - 76f, 40f);
        string status = item.isGenerated ? "OK" : "—";
        if (!item.includeInLevels)
        {
            status += "  (excluded)";
        }

        GUI.Label(labelRect, string.Format("{0}\n{1} px  [{2}]", item.levelName, GetTextureSizeLabel(item.sourceTexture), status));
        GUILayout.Space(4f);
    }

    private void DrawTileListRow(ConversionItem item, int index, string title, string status, bool showIncludeToggle)
    {
        bool isSelected = selectedIndex == index;
        Rect rowRect = GUILayoutUtility.GetRect(248f, 48f);
        if (Event.current.type == EventType.Repaint)
        {
            if (showIncludeToggle && !item.includeInLevels)
            {
                EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.15f));
            }

            GUIStyle style = isSelected ? "SelectionRect" : GUIStyle.none;
            style.Draw(rowRect, false, false, isSelected, false);
        }

        Rect toggleRect = Rect.zero;
        if (showIncludeToggle)
        {
            toggleRect = new Rect(rowRect.x + 4f, rowRect.y + 15f, 18f, 18f);
            bool include = EditorGUI.Toggle(toggleRect, item.includeInLevels);
            if (include != item.includeInLevels)
            {
                item.includeInLevels = include;
                Repaint();
            }
        }

        if (Event.current.type == EventType.MouseDown
            && rowRect.Contains(Event.current.mousePosition)
            && (!showIncludeToggle || !toggleRect.Contains(Event.current.mousePosition)))
        {
            if (colorReplaceScope == ColorReplaceScope.Tile && colorReplaceTileIndex != index)
            {
                ClearColorReplaceSelection();
            }

            selectedIndex = index;
            GUI.FocusControl(null);
            Repaint();
        }

        float contentX = showIncludeToggle ? rowRect.x + 26f : rowRect.x + 4f;
        Rect thumbRect = new Rect(contentX, rowRect.y + 4f, 40f, 40f);
        Texture2D thumb = item.previewTexture != null
            ? item.previewTexture
            : (item.sourcePreviewTexture != null ? item.sourcePreviewTexture : item.sourceTexture);
        if (thumb != null)
        {
            GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f));
        }

        float labelX = showIncludeToggle ? rowRect.x + 72f : rowRect.x + 50f;
        Rect labelRect = new Rect(labelX, rowRect.y + 4f, rowRect.width - (labelX - rowRect.x), 40f);
        if (showIncludeToggle && !item.includeInLevels)
        {
            status += "  (excluded)";
        }

        GUI.Label(labelRect, string.Format("{0}\n{1}  [{2}]", title, GetTextureSizeLabel(thumb), status));
        GUILayout.Space(4f);
    }

    private void DrawTileCompositionDetail()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        if (tileSourceItem == null)
        {
            EditorGUILayout.HelpBox(
                "Load a sprite sheet (Texture Type: Sprite, Mode: Multiple). "
                + "Tiles are taken from Unity Sprite Editor slices — each sprite is processed with its own palette.",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            DrawSelectedTileDetail(items[selectedIndex]);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        ConversionItem source = tileSourceItem;
        EditorGUILayout.LabelField("Sprite Sheet", EditorStyles.boldLabel);
        source.sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", source.sourceTexture, typeof(Texture2D), false);
        tileLevelName = EditorGUILayout.TextField("Level Name", tileLevelName);

        if (source.sourceTexture != null)
        {
            EditorGUILayout.LabelField(
                string.Format("Texture size: {0}×{1} px", source.sourceTexture.width, source.sourceTexture.height),
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                string.Format("Unity sprite grid: {0}×{1} ({2} sprites)", tilesX, tilesY, items.Count > 0 ? items.Count : tilesX * tilesY),
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Output Grid", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        tileCellWidth = EditorGUILayout.IntSlider(
            "Cells Per Tile W",
            tileCellWidth,
            LevelData.MinTileCellDimension,
            LevelData.MaxTileCellDimension);
        tileCellHeight = EditorGUILayout.IntSlider(
            "Cells Per Tile H",
            tileCellHeight,
            LevelData.MinTileCellDimension,
            LevelData.MaxTileCellDimension);
        EditorGUILayout.EndHorizontal();

        int resultWidth = tilesX * tileCellWidth;
        int resultHeight = tilesY * tileCellHeight;
        EditorGUILayout.LabelField(
            string.Format("Level grid: {0}×{1} cells", resultWidth, resultHeight),
            EditorStyles.miniLabel);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Per-Tile Processing", EditorStyles.boldLabel);
        source.targetPalette = (SandColorPalette)EditorGUILayout.ObjectField(
            "Palette Override",
            source.targetPalette,
            typeof(SandColorPalette),
            false);
        SandColorPalette activePalette = GetActivePalette(source);
        DrawColorCountField(ref source.colorCount, ref source.autoColorCount, activePalette);
        source.autoBackgroundColor = EditorGUILayout.Toggle("Auto Background", source.autoBackgroundColor);
        using (new EditorGUI.DisabledScope(source.autoBackgroundColor))
        {
            source.backgroundColor = EditorGUILayout.ColorField("Background Color", source.backgroundColor);
        }

        if (activePalette != null)
        {
            DrawTargetPalettePreview(activePalette);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Compose Level", GUILayout.Height(28f)))
        {
            ComposeTiles();
        }

        using (new EditorGUI.DisabledScope(!composedIsGenerated))
        {
            if (GUILayout.Button("Save All Assets", GUILayout.Height(28f)))
            {
                SaveComposedLevel(showDialog: true);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (source.sourceTexture != null)
        {
            EditorGUILayout.Space(4f);
            DrawTileSourceAndComposedPreview(source, resultWidth, resultHeight);
        }
        else
        {
            DrawComposedPreviewSection(resultWidth, resultHeight, null);
        }

        DrawComposedPaletteSection();

        if (composedIsGenerated && composedPalette != null && composedPalette.Length > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                string.Format("Merged level palette: {0} color(s) from all tiles", composedPalette.Length),
                EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedTileDetail(ConversionItem item)
    {
        int tileIndex = items.IndexOf(item);
        if (colorReplaceScope == ColorReplaceScope.Tile && colorReplaceTileIndex != tileIndex)
        {
            ClearColorReplaceSelection();
        }

        EditorGUILayout.LabelField("Sprite Tile", EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(item.spriteName))
        {
            EditorGUILayout.LabelField("Sprite: " + item.spriteName, EditorStyles.miniLabel);
        }

        EditorGUILayout.LabelField(string.Format("Grid position ({0}, {1})", item.tileX, item.tileY), EditorStyles.miniLabel);
        item.includeInLevels = EditorGUILayout.Toggle("Include In Level List", item.includeInLevels);

        EnsureTileSourcePreview(item);
        DrawSelectedTileProcessingSettings();

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(tileSourceItem == null || item.sourcePreviewTexture == null))
        {
            if (GUILayout.Button("Regenerate Tile", GUILayout.Height(28f)))
            {
                RegenerateSingleTile(item);
            }
        }

        using (new EditorGUI.DisabledScope(!item.isGenerated))
        {
            if (GUILayout.Button("Save Tile Asset", GUILayout.Height(28f)))
            {
                if (SaveLevelAsset(item, out LevelData savedLevel, showDialog: true))
                {
                    RegisterLevelsInDatabase(new[] { savedLevel });
                    AssetDatabase.SaveAssets();
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        if (item.isGenerated && item.previewTexture != null)
        {
            EditorGUILayout.LabelField(
                string.Format("Tile palette: {0} color(s)", item.palette != null ? item.palette.Length : 0),
                EditorStyles.miniLabel);
            DrawTileSourceAndGeneratedPreview(item);
            DrawTilePaletteWithColorReplace(item);
        }
        else if (item.sourcePreviewTexture != null)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Source", EditorStyles.miniLabel);
            DrawTexturePreview(item.sourcePreviewTexture, 200f, allowUpscale: false);
            EditorGUILayout.HelpBox(
                item.includeInLevels
                    ? "Tile is included. Compose level to generate preview."
                    : "Tile is excluded and will be skipped during compose and save.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                item.includeInLevels
                    ? "Tile is included. Compose level to generate preview."
                    : "Tile is excluded and will be skipped during compose and save.",
                MessageType.Info);
        }
    }

    private void DrawTileSourceAndComposedPreview(ConversionItem source, int gridWidth, int gridHeight)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        const float maxDisplaySize = 280f;
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Source", EditorStyles.miniLabel);
        if (source.sourceTexture != null)
        {
            DrawTexturePreview(source.sourceTexture, maxDisplaySize, allowUpscale: false);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawComposedPreviewSection(gridWidth, gridHeight, composedPreviewTexture, maxDisplaySize, inline: true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTileSourceAndGeneratedPreview(ConversionItem item)
    {
        const float maxDisplaySize = 200f;
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Source", EditorStyles.miniLabel);
        if (item.sourcePreviewTexture != null)
        {
            DrawTexturePreview(item.sourcePreviewTexture, maxDisplaySize, allowUpscale: false);
        }
        else
        {
            EditorGUILayout.HelpBox("Source preview unavailable.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField(
            string.Format("Generated ({0}x{1})", item.gridWidth, item.gridHeight),
            EditorStyles.miniLabel);
        DrawTexturePreview(item.previewTexture, maxDisplaySize, allowUpscale: false);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawComposedPreviewSection(
        int gridWidth,
        int gridHeight,
        Texture2D previewTexture = null,
        float maxDisplaySize = 320f,
        bool inline = false)
    {
        if (!inline)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Composed Preview", EditorStyles.boldLabel);
        }
        else
        {
            EditorGUILayout.LabelField("Composed", EditorStyles.miniLabel);
        }

        Texture2D texture = previewTexture != null ? previewTexture : composedPreviewTexture;
        if (texture != null)
        {
            if (!inline)
            {
                EditorGUILayout.LabelField(
                    string.Format(
                        "Grid {0}x{1}, texture {2}x{3}",
                        gridWidth,
                        gridHeight,
                        texture.width,
                        texture.height),
                    EditorStyles.miniLabel);
            }

            DrawTexturePreview(texture, maxDisplaySize, allowUpscale: false);
        }
        else
        {
            float aspect = gridHeight > 0 ? gridWidth / (float)gridHeight : 1f;
            float placeholderWidth = aspect >= 1f ? maxDisplaySize : maxDisplaySize * aspect;
            float placeholderHeight = aspect >= 1f ? maxDisplaySize / aspect : maxDisplaySize;
            if (!inline)
            {
                EditorGUILayout.LabelField(
                    string.Format("Grid {0}x{1}", gridWidth, gridHeight),
                    EditorStyles.miniLabel);
            }

            Rect placeholder = GUILayoutUtility.GetRect(placeholderWidth, placeholderHeight);
            EditorGUI.DrawRect(placeholder, new Color(0.15f, 0.15f, 0.15f));
            GUI.Label(placeholder, "Compose level to see result", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawComposedPaletteSection()
    {
        if (!composedIsGenerated || composedPalette == null || composedPalette.Length == 0)
        {
            return;
        }

        SandColorPalette targetPalette = tileSourceItem != null ? GetActivePalette(tileSourceItem) : defaultTargetPalette;
        byte[] usedColors = LevelData.FilterUsedPaletteColors(composedCells, composedPalette.Length);

        EditorGUILayout.Space();
        DrawPaletteWithColorReplace(
            usedColors,
            composedPalette,
            colorIndex => CountCells(composedCells, colorIndex),
            colorIndex => GetBucketCount(composedBuckets, colorIndex),
            ColorReplaceScope.Composed,
            -1,
            ReplaceComposedColor,
            targetPalette);
    }

    private void DrawTilePaletteWithColorReplace(ConversionItem item)
    {
        if (item.palette == null || item.palette.Length == 0)
        {
            return;
        }

        SandColorPalette targetPalette = tileSourceItem != null ? GetActivePalette(tileSourceItem) : defaultTargetPalette;
        byte[] usedColors = LevelData.FilterUsedPaletteColors(item.cells, item.palette.Length);
        int tileIndex = items.IndexOf(item);

        EditorGUILayout.Space();
        DrawPaletteWithColorReplace(
            usedColors,
            item.palette,
            colorIndex => CountCells(item.cells, colorIndex),
            colorIndex => GetBucketCount(item.buckets, colorIndex),
            ColorReplaceScope.Tile,
            tileIndex,
            (fromIndex, toColor) => ReplaceTileColor(item, fromIndex, toColor),
            targetPalette);
    }

    private void DrawPaletteWithColorReplace(
        byte[] usedColors,
        Color32[] currentPalette,
        System.Func<byte, int> getCellCount,
        System.Func<byte, int> getBucketCount,
        ColorReplaceScope scope,
        int tileIndex,
        System.Action<byte, Color32> onReplace,
        SandColorPalette targetPalette = null)
    {
        if (usedColors == null || usedColors.Length == 0)
        {
            return;
        }

        if (targetPalette == null)
        {
            targetPalette = tileSourceItem != null ? GetActivePalette(tileSourceItem) : defaultTargetPalette;
        }

        EditorGUILayout.LabelField("Palette & Buckets", EditorStyles.boldLabel);
        if (targetPalette != null)
        {
            EditorGUILayout.LabelField("Click a color to replace it. Right-click copies hex.", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a Color Palette to enable color replacement.", MessageType.Info);
        }

        for (int i = 0; i < usedColors.Length; i++)
        {
            byte colorIndex = usedColors[i];
            int cellCount = getCellCount(colorIndex);
            int bucketCount = getBucketCount(colorIndex);
            Color32 color = currentPalette[colorIndex - 1];
            bool isSelected = colorReplaceScope == scope
                && colorReplaceSourceIndex == colorIndex
                && (scope != ColorReplaceScope.Tile || colorReplaceTileIndex == tileIndex);

            EditorGUILayout.BeginHorizontal();
            if (DrawSelectableColorSwatch(color, isSelected, 24f, out bool clicked))
            {
                if (clicked && targetPalette != null)
                {
                    if (isSelected)
                    {
                        ClearColorReplaceSelection();
                    }
                    else
                    {
                        colorReplaceSourceIndex = colorIndex;
                        colorReplaceScope = scope;
                        colorReplaceTileIndex = tileIndex;
                    }

                    Repaint();
                }
            }

            EditorGUILayout.LabelField(
                string.Format("Color {0}: {1} cells, {2} buckets  ({3})", colorIndex, cellCount, bucketCount, FormatColorForClipboard(color)),
                GUILayout.MinWidth(120f));
            EditorGUILayout.EndHorizontal();
        }

        if (targetPalette != null
            && colorReplaceScope == scope
            && colorReplaceSourceIndex > 0
            && (scope != ColorReplaceScope.Tile || colorReplaceTileIndex == tileIndex))
        {
            DrawHorizontalReplacementPalette(targetPalette, colorReplaceSourceIndex, onReplace);
        }
    }

    private void DrawHorizontalReplacementPalette(
        SandColorPalette targetPalette,
        byte fromColorIndex,
        System.Action<byte, Color32> onReplace)
    {
        Color32[] paletteColors = targetPalette.GetColors();
        if (paletteColors.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            string.Format("Replace color {0} with:", fromColorIndex),
            GUILayout.Width(130f));

        for (int i = 0; i < paletteColors.Length; i++)
        {
            Color32 paletteColor = paletteColors[i];
            Rect rect = GUILayoutUtility.GetRect(28f, 28f, GUILayout.Width(28f), GUILayout.Height(28f));
            EditorGUI.DrawRect(rect, paletteColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                onReplace(fromColorIndex, paletteColor);
                ClearColorReplaceSelection();
                Event.current.Use();
            }
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(56f)))
        {
            ClearColorReplaceSelection();
        }

        EditorGUILayout.EndHorizontal();
    }

    private bool DrawSelectableColorSwatch(Color32 color, bool selected, float size, out bool clicked)
    {
        clicked = false;
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(rect, color);

        if (selected)
        {
            Color highlight = new Color(1f, 1f, 1f, 0.95f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), highlight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), highlight);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), highlight);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), highlight);
        }
        else
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0f, 0f, 0f, 0.35f));
        }

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 1)
            {
                CopyColorToClipboard(color);
                Event.current.Use();
            }
            else if (Event.current.button == 0)
            {
                clicked = true;
                Event.current.Use();
            }
        }

        return true;
    }

    private void ClearColorReplaceSelection()
    {
        colorReplaceSourceIndex = 0;
        colorReplaceScope = ColorReplaceScope.None;
        colorReplaceTileIndex = -1;
    }

    private void ReplaceComposedColor(byte fromIndex, Color32 toColor)
    {
        if (!composedIsGenerated || fromIndex == 0 || composedCells == null)
        {
            return;
        }

        byte toIndex = ResolveOrAddPaletteColor(ref composedPalette, toColor);
        if (toIndex == 0 || fromIndex == toIndex)
        {
            return;
        }

        ReplaceColorIndices(composedCells, fromIndex, toIndex);
        composedBuckets = BuildBucketSpawnsFromCells(composedCells, composedPalette.Length);
        RefreshComposedPreview();
        Repaint();
    }

    private void ReplaceTileColor(ConversionItem item, byte fromIndex, Color32 toColor)
    {
        if (!item.isGenerated || fromIndex == 0 || item.cells == null)
        {
            return;
        }

        byte toIndex = ResolveOrAddPaletteColor(ref item.palette, toColor);
        if (toIndex == 0 || fromIndex == toIndex)
        {
            return;
        }

        ReplaceColorIndices(item.cells, fromIndex, toIndex);
        item.buckets = BuildBucketSpawnsFromCells(item.cells, item.palette.Length);
        item.DisposePreview();
        item.previewTexture = ImageProcessor.BuildTexture(
            ImageProcessor.BuildFromCells(item.cells, item.palette, item.gridWidth, item.gridHeight),
            defaultPreviewScale);
        Repaint();
    }

    private static void ReplaceColorIndices(byte[] cells, byte fromIndex, byte toIndex)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == fromIndex)
            {
                cells[i] = toIndex;
            }
        }
    }

    private static byte ResolveOrAddPaletteColor(ref Color32[] palette, Color32 color)
    {
        if (palette == null)
        {
            palette = new Color32[0];
        }

        for (int i = 0; i < palette.Length; i++)
        {
            if (PaletteColorUtility.ColorsEqual(palette[i], color))
            {
                return (byte)(i + 1);
            }
        }

        if (palette.Length >= SandColorPalette.MaxColorCount)
        {
            int nearest = ColorQuantizer.FindNearestPaletteIndex(color, palette);
            return (byte)(nearest + 1);
        }

        Color32[] expanded = new Color32[palette.Length + 1];
        for (int i = 0; i < palette.Length; i++)
        {
            expanded[i] = palette[i];
        }

        expanded[palette.Length] = color;
        palette = expanded;
        return (byte)palette.Length;
    }

    private void RefreshComposedPreview()
    {
        int gridWidth = tilesX * tileCellWidth;
        int gridHeight = tilesY * tileCellHeight;
        DisposeComposedPreview();
        composedPreviewTexture = ImageProcessor.BuildTexture(
            ImageProcessor.BuildFromCells(composedCells, composedPalette, gridWidth, gridHeight),
            defaultPreviewScale);
    }

    private TileLevelComposer.Settings GetTileComposerSettings()
    {
        return TileLevelComposer.CreateSettings(
            tilesX,
            tilesY,
            tileCellWidth,
            tileCellHeight,
            defaultColorCount,
            defaultAutoColorCount,
            128,
            defaultAutoBackgroundColor,
            defaultBackgroundColor,
            defaultTargetPalette);
    }

    private void DrawSelectedTileProcessingSettings()
    {
        if (tileSourceItem == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Processing (shared for all tiles)", EditorStyles.miniLabel);
        SandColorPalette activePalette = GetActivePalette(tileSourceItem);
        if (activePalette != null)
        {
            EditorGUILayout.LabelField("Palette: " + activePalette.name, EditorStyles.miniLabel);
        }

        DrawColorCountField(ref tileSourceItem.colorCount, ref tileSourceItem.autoColorCount, activePalette);
        tileSourceItem.autoBackgroundColor = EditorGUILayout.Toggle("Auto Background", tileSourceItem.autoBackgroundColor);
        using (new EditorGUI.DisabledScope(tileSourceItem.autoBackgroundColor))
        {
            tileSourceItem.backgroundColor = EditorGUILayout.ColorField("Background Color", tileSourceItem.backgroundColor);
        }
    }

    private bool TryGetSpriteTile(ConversionItem item, out UnitySpriteTileLoader.SpriteTileInfo spriteTile)
    {
        spriteTile = default;
        if (tileSourceItem == null || string.IsNullOrEmpty(tileSourceItem.sourceAssetPath))
        {
            return false;
        }

        if (!UnitySpriteTileLoader.TryLoadTiles(
                tileSourceItem.sourceAssetPath,
                out UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles,
                out _,
                out _))
        {
            return false;
        }

        for (int i = 0; i < spriteTiles.Length; i++)
        {
            if (spriteTiles[i].tileX != item.tileX || spriteTiles[i].tileY != item.tileY)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(item.spriteName) && spriteTiles[i].spriteName != item.spriteName)
            {
                continue;
            }

            spriteTile = spriteTiles[i];
            return true;
        }

        return false;
    }

    private void ApplyTileResultToItem(ConversionItem item, ImageProcessor.Result result)
    {
        item.gridWidth = result.width;
        item.gridHeight = result.height;
        item.palette = result.palette;
        item.cells = result.cellIndices;
        item.detectedBackgroundColor = result.letterboxColor;
        item.buckets = BuildBucketSpawnsFromCells(item.cells, item.palette.Length);
        item.DisposePreview();
        item.previewTexture = ImageProcessor.BuildTexture(result, defaultPreviewScale);
        item.isGenerated = true;
    }

    private void RegenerateSingleTile(ConversionItem item)
    {
        if (tileSourceItem == null)
        {
            return;
        }

        if (!TryGetSpriteTile(item, out UnitySpriteTileLoader.SpriteTileInfo spriteTile))
        {
            EditorUtility.DisplayDialog("Regenerate Tile", "Could not load sprite tile from the source sheet.", "OK");
            return;
        }

        TileLevelComposer.Settings settings = GetTileComposerSettings();
        TileLevelComposer.TileInput tileInput = TileLevelComposer.CreateTileInput(
            spriteTile.pixels,
            spriteTile.pixelWidth,
            spriteTile.pixelHeight,
            item.tileX,
            item.tileY,
            tileSourceItem.colorCount,
            tileSourceItem.autoColorCount,
            128,
            tileSourceItem.autoBackgroundColor,
            tileSourceItem.backgroundColor,
            GetActivePalette(tileSourceItem));

        ImageProcessor.Result result = TileLevelComposer.ProcessSingleTile(tileInput, settings);
        ApplyTileResultToItem(item, result);

        if (tileSourceItem.autoColorCount && result.palette != null)
        {
            tileSourceItem.colorCount = result.palette.Length;
        }

        if (composedIsGenerated && composedCells != null)
        {
            int gridWidth = tilesX * tileCellWidth;
            int gridHeight = tilesY * tileCellHeight;
            if (composedCells.Length == gridWidth * gridHeight)
            {
                TileLevelComposer.MergeTileResult(
                    result,
                    item.tileX,
                    item.tileY,
                    settings,
                    ref composedCells,
                    ref composedPalette);
                composedBuckets = BuildBucketSpawnsFromCells(composedCells, composedPalette.Length);
                RefreshComposedPreview();
            }
        }

        Repaint();
    }

    private void EnsureTileSourcePreview(ConversionItem item)
    {
        if (item.sourcePreviewTexture != null || tileSourceItem == null)
        {
            return;
        }

        string assetPath = tileSourceItem.sourceAssetPath;
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        if (!UnitySpriteTileLoader.TryLoadTiles(
                assetPath,
                out UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles,
                out _,
                out _))
        {
            return;
        }

        for (int i = 0; i < spriteTiles.Length; i++)
        {
            UnitySpriteTileLoader.SpriteTileInfo spriteTile = spriteTiles[i];
            if (spriteTile.tileX != item.tileX || spriteTile.tileY != item.tileY)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(item.spriteName) && spriteTile.spriteName != item.spriteName)
            {
                continue;
            }

            item.sourcePreviewTexture = BuildTextureFromPixels(
                spriteTile.pixels,
                spriteTile.pixelWidth,
                spriteTile.pixelHeight);
            return;
        }
    }

    private void BuildAllTileSourcePreviews()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].sourcePreviewTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(items[i].sourcePreviewTexture);
                items[i].sourcePreviewTexture = null;
            }

            EnsureTileSourcePreview(items[i]);
        }
    }

    private static Texture2D BuildTextureFromPixels(Color32[] pixels, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private void DrawSelectedItemDetail()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        if (selectedIndex < 0 || selectedIndex >= items.Count)
        {
            EditorGUILayout.HelpBox("Load a folder or add textures. Select an image to edit its settings.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        ConversionItem item = items[selectedIndex];
        EditorGUILayout.LabelField("Selected Image", EditorStyles.boldLabel);
        item.includeInLevels = EditorGUILayout.Toggle("Include In Level List", item.includeInLevels);
        item.sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Texture", item.sourceTexture, typeof(Texture2D), false);
        item.levelName = EditorGUILayout.TextField("Level Name", item.levelName);
        item.gridWidth = EditorGUILayout.IntSlider(
            "Grid Width",
            item.gridWidth,
            LevelData.MinGridDimension,
            LevelData.MaxGridDimension);
        item.gridHeight = EditorGUILayout.IntSlider(
            "Grid Height",
            item.gridHeight,
            LevelData.MinGridDimension,
            LevelData.MaxGridDimension);
        item.targetPalette = (SandColorPalette)EditorGUILayout.ObjectField(
            "Palette Override",
            item.targetPalette,
            typeof(SandColorPalette),
            false);
        SandColorPalette activePalette = GetActivePalette(item);
        if (item.targetPalette == null && defaultTargetPalette != null)
        {
            EditorGUILayout.LabelField("Using default palette: " + defaultTargetPalette.name, EditorStyles.miniLabel);
        }
        else if (activePalette == null)
        {
            string colorInfo = item.autoColorCount && item.isGenerated
                ? string.Format("Detected {0} colors from image", item.palette != null ? item.palette.Length : 0)
                : "No palette — colors detected from image";
            EditorGUILayout.LabelField(colorInfo, EditorStyles.miniLabel);
        }
        else
        {
            string colorInfo = item.autoColorCount && item.isGenerated
                ? string.Format("Detected {0} colors (palette has {1})", item.palette != null ? item.palette.Length : 0, activePalette.GetColorCount())
                : string.Format("Using up to {0} colors from palette ({1} available)", item.colorCount, activePalette.GetColorCount());
            EditorGUILayout.LabelField(colorInfo, EditorStyles.miniLabel);
        }

        DrawColorCountField(ref item.colorCount, ref item.autoColorCount, activePalette);

        if (activePalette != null)
        {
            DrawTargetPalettePreview(activePalette);
        }

        item.previewScale = EditorGUILayout.IntSlider("Preview Scale", item.previewScale, 4, 16);
        item.autoBackgroundColor = EditorGUILayout.Toggle("Auto Background", item.autoBackgroundColor);
        using (new EditorGUI.DisabledScope(item.autoBackgroundColor))
        {
            item.backgroundColor = EditorGUILayout.ColorField("Background Color", item.backgroundColor);
        }

        if (item.isGenerated)
        {
            EditorGUILayout.LabelField("Detected Background", EditorStyles.miniLabel);
            Rect detectedRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
            EditorGUI.DrawRect(detectedRect, item.detectedBackgroundColor);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(item.sourceTexture == null))
        {
            if (GUILayout.Button("Regenerate Preview", GUILayout.Height(28f)))
            {
                GeneratePreview(item);
            }
        }

        using (new EditorGUI.DisabledScope(!item.isGenerated))
        {
            if (GUILayout.Button("Save Level Asset", GUILayout.Height(28f)))
            {
                SaveLevelAsset(item);
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawPreviewSection(item);
        DrawPaletteSection(item);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void LoadTileSourceImage()
    {
        string path = EditorUtility.OpenFilePanelWithFilters(
            "Select Source Image",
            Application.dataPath,
            new[] { "Image files", "png,jpg,jpeg" });
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string assetPath = ToProjectRelativePath(path);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Load Image", "Image must be inside the Unity project (under Assets).", "OK");
            return;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Load Image", "Could not load texture at:\n" + assetPath, "OK");
            return;
        }

        SetTileSource(texture, assetPath);
        Repaint();
    }

    private void SetTileSource(Texture2D texture, string assetPath)
    {
        ClearTileResults();
        if (tileSourceItem != null)
        {
            tileSourceItem.DisposeAll();
        }

        tileSourceItem = CreateItemFromTexture(texture, assetPath);
        tileLevelName = tileSourceItem.levelName;
        selectedIndex = -1;
        composedIsGenerated = false;
        DisposeComposedPreview();
        RefreshSpriteGridFromSource(assetPath);
    }

    private void RefreshSpriteGridFromSource(string assetPath)
    {
        if (!UnitySpriteTileLoader.TryLoadTiles(assetPath, out UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles, out UnitySpriteTileLoader.GridInfo grid, out string error))
        {
            tilesX = 1;
            tilesY = 1;
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Sprite Sheet", error, "OK");
            }

            return;
        }

        tilesX = grid.tilesX;
        tilesY = grid.tilesY;
        PopulateTileSelectionList(spriteTiles);
    }

    private void PopulateTileSelectionList(UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles)
    {
        Dictionary<string, bool> includeFlags = CaptureTileIncludeFlags();

        for (int i = 0; i < items.Count; i++)
        {
            items[i].DisposeAll();
        }

        items.Clear();
        tileResultsReady = false;

        for (int i = 0; i < spriteTiles.Length; i++)
        {
            UnitySpriteTileLoader.SpriteTileInfo spriteTile = spriteTiles[i];
            string key = GetTileKey(spriteTile.spriteName, spriteTile.tileX, spriteTile.tileY);
            bool include = true;
            if (includeFlags.TryGetValue(key, out bool storedInclude))
            {
                include = storedInclude;
            }

            items.Add(new ConversionItem
            {
                levelName = string.IsNullOrEmpty(spriteTile.spriteName)
                    ? string.Format("Tile {0},{1}", spriteTile.tileX, spriteTile.tileY)
                    : spriteTile.spriteName,
                spriteName = spriteTile.spriteName,
                tileX = spriteTile.tileX,
                tileY = spriteTile.tileY,
                sourceAssetPath = tileSourceItem != null ? tileSourceItem.sourceAssetPath : string.Empty,
                includeInLevels = include,
                sourcePreviewTexture = BuildTextureFromPixels(
                    spriteTile.pixels,
                    spriteTile.pixelWidth,
                    spriteTile.pixelHeight)
            });
        }
    }

    private Dictionary<string, bool> CaptureTileIncludeFlags()
    {
        Dictionary<string, bool> flags = new Dictionary<string, bool>();
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            flags[GetTileKey(item.spriteName, item.tileX, item.tileY)] = item.includeInLevels;
        }

        return flags;
    }

    private static string GetTileKey(string spriteName, int tileX, int tileY)
    {
        return !string.IsNullOrEmpty(spriteName) ? spriteName : tileX + "," + tileY;
    }

    private bool IsTileIncluded(string spriteName, int tileX, int tileY)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            if (item.tileX == tileX && item.tileY == tileY)
            {
                if (!string.IsNullOrEmpty(spriteName) && item.spriteName != spriteName)
                {
                    continue;
                }

                return item.includeInLevels;
            }
        }

        return true;
    }

    private UnitySpriteTileLoader.SpriteTileInfo[] FilterIncludedSprites(UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles)
    {
        List<UnitySpriteTileLoader.SpriteTileInfo> included = new List<UnitySpriteTileLoader.SpriteTileInfo>(spriteTiles.Length);
        for (int i = 0; i < spriteTiles.Length; i++)
        {
            UnitySpriteTileLoader.SpriteTileInfo spriteTile = spriteTiles[i];
            if (IsTileIncluded(spriteTile.spriteName, spriteTile.tileX, spriteTile.tileY))
            {
                included.Add(spriteTile);
            }
        }

        return included.ToArray();
    }

    private ConversionItem FindTileItem(string spriteName, int tileX, int tileY)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            if (item.tileX == tileX && item.tileY == tileY)
            {
                if (!string.IsNullOrEmpty(spriteName) && item.spriteName != spriteName)
                {
                    continue;
                }

                return item;
            }
        }

        return null;
    }

    private void ComposeTiles()
    {
        if (tileSourceItem == null || tileSourceItem.sourceTexture == null)
        {
            return;
        }

        string assetPath = tileSourceItem.sourceAssetPath;
        if (!UnitySpriteTileLoader.TryLoadTiles(assetPath, out UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles, out UnitySpriteTileLoader.GridInfo grid, out string error))
        {
            EditorUtility.DisplayDialog("Compose Level", error, "OK");
            return;
        }

        tilesX = grid.tilesX;
        tilesY = grid.tilesY;

        UnitySpriteTileLoader.SpriteTileInfo[] includedSprites = FilterIncludedSprites(spriteTiles);
        if (includedSprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Compose Level", "Select at least one tile to include.", "OK");
            return;
        }

        TileLevelComposer.Settings settings = GetTileComposerSettings();

        List<TileLevelComposer.TileInput> tileInputs = UnitySpriteTileLoader.BuildTileInputs(
            includedSprites,
            tileSourceItem.colorCount,
            tileSourceItem.autoColorCount,
            128,
            tileSourceItem.autoBackgroundColor,
            tileSourceItem.backgroundColor,
            GetActivePalette(tileSourceItem));

        TileLevelComposer.ComposeResult composeResult = TileLevelComposer.Compose(tileInputs, settings);
        SyncTileResultItems(composeResult, spriteTiles);

        composedPalette = composeResult.level.palette;
        composedCells = composeResult.level.cellIndices;
        composedBuckets = BuildBucketSpawnsFromCells(composedCells, composedPalette.Length);
        if (tileSourceItem.autoColorCount && composedPalette != null)
        {
            tileSourceItem.colorCount = composedPalette.Length;
        }
        DisposeComposedPreview();
        composedPreviewTexture = ImageProcessor.BuildTexture(composeResult.level, defaultPreviewScale);
        composedIsGenerated = true;
        tileSourceItem.isGenerated = true;
        BuildAllTileSourcePreviews();
        Repaint();
    }

    private void SyncTileResultItems(
        TileLevelComposer.ComposeResult composeResult,
        UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles)
    {
        for (int i = 0; i < composeResult.tiles.Length; i++)
        {
            TileLevelComposer.TileProcessResult tileResult = composeResult.tiles[i];
            string spriteName = FindSpriteName(spriteTiles, tileResult.tileX, tileResult.tileY);
            ConversionItem tileItem = FindTileItem(spriteName, tileResult.tileX, tileResult.tileY);
            if (tileItem == null)
            {
                tileItem = new ConversionItem
                {
                    levelName = string.IsNullOrEmpty(spriteName)
                        ? string.Format("Tile {0},{1}", tileResult.tileX, tileResult.tileY)
                        : spriteName,
                    spriteName = spriteName,
                    tileX = tileResult.tileX,
                    tileY = tileResult.tileY,
                    sourceAssetPath = tileSourceItem != null ? tileSourceItem.sourceAssetPath : string.Empty,
                    includeInLevels = true
                };
                items.Add(tileItem);
            }

            tileItem.gridWidth = tileResult.result.width;
            tileItem.gridHeight = tileResult.result.height;
            tileItem.palette = tileResult.result.palette;
            tileItem.cells = tileResult.result.cellIndices;
            tileItem.detectedBackgroundColor = tileResult.result.letterboxColor;
            tileItem.buckets = BuildBucketSpawnsFromCells(tileItem.cells, tileItem.palette.Length);
            tileItem.DisposePreview();
            tileItem.previewTexture = ImageProcessor.BuildTexture(tileResult.result, defaultPreviewScale);
            tileItem.isGenerated = true;
        }

        tileResultsReady = true;
    }

    private static string FindSpriteName(UnitySpriteTileLoader.SpriteTileInfo[] spriteTiles, int tileX, int tileY)
    {
        if (spriteTiles == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < spriteTiles.Length; i++)
        {
            if (spriteTiles[i].tileX == tileX && spriteTiles[i].tileY == tileY)
            {
                return spriteTiles[i].spriteName;
            }
        }

        return string.Empty;
    }

    private void ClearTileResults()
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].DisposeAll();
        }

        items.Clear();
        tileResultsReady = false;
    }

    private void SaveComposedLevel(bool showDialog)
    {
        if (!composedIsGenerated || composedCells == null || composedCells.Length == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Save Level", "Compose level before saving.", "OK");
            }

            return;
        }

        if (composedPreviewTexture == null)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Save Level", "Compose level before saving.", "OK");
            }

            return;
        }

        int gridWidth = tilesX * tileCellWidth;
        int gridHeight = tilesY * tileCellHeight;
        int expectedCellCount = gridWidth * gridHeight;
        if (composedCells.Length != expectedCellCount)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Save Level",
                    string.Format(
                        "Cell data size ({0}) does not match grid ({1}x{2}). Recompose before saving.",
                        composedCells.Length,
                        gridWidth,
                        gridHeight),
                    "OK");
            }

            return;
        }

        string levelsFolder = EnsureLevelsFolder();
        string safeName = SanitizeAssetName(string.IsNullOrWhiteSpace(tileLevelName) ? "TiledLevel" : tileLevelName.Trim());
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(levelsFolder, safeName + ".asset"));

        LevelData levelData = CreateInstance<LevelData>();
        levelData.gridWidth = gridWidth;
        levelData.gridHeight = gridHeight;
        levelData.palette = composedPalette;
        levelData.cells = composedCells;
        levelData.buckets = composedBuckets;
        levelData.usesTiles = true;
        levelData.usesUnitySpriteSheet = true;
        levelData.spriteSheetAssetPath = tileSourceItem != null ? tileSourceItem.sourceAssetPath : string.Empty;
        levelData.tilesX = tilesX;
        levelData.tilesY = tilesY;
        levelData.tileCellWidth = tileCellWidth;
        levelData.tileCellHeight = tileCellHeight;
        levelData.tileSources = BuildTileSources();
        ApplyBucketLayoutRules(levelData, GetPredictedLevelIndexForNewLevel());

        AssetDatabase.CreateAsset(levelData, assetPath);

        WriteAndLinkPreview(levelData, composedPreviewTexture, assetPath);

        string tilesFolder = EnsureAssetSubfolder(levelsFolder, safeName);
        List<LevelData> savedLevels = new List<LevelData> { levelData };
        int savedTiles = SaveGeneratedTileItems(tilesFolder, savedLevels);

        RegisterLevelsInDatabase(savedLevels);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Save All",
                string.Format(
                    "Saved composed level:\n{0}\n\nSaved {1} tile level(s) to:\n{2}\n\nAdded {3} level(s) to {4}.",
                    assetPath,
                    savedTiles,
                    tilesFolder,
                    savedLevels.Count,
                    targetLevelDatabase != null ? targetLevelDatabase.name : "Level Database (not assigned)"),
                "OK");
            Selection.activeObject = levelData;
        }
    }

    private int SaveGeneratedTileItems(string targetFolder, List<LevelData> savedLevels)
    {
        int savedCount = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            if (!item.includeInLevels || !item.isGenerated)
            {
                continue;
            }

            if (SaveLevelAsset(item, out LevelData savedLevel, showDialog: false, targetFolder: targetFolder))
            {
                savedLevels.Add(savedLevel);
                savedCount++;
            }
        }

        return savedCount;
    }

    private TileSourceData[] BuildTileSources()
    {
        if (tileSourceItem == null)
        {
            return new TileSourceData[0];
        }

        SandColorPalette palette = GetActivePalette(tileSourceItem);
        string palettePath = palette != null ? AssetDatabase.GetAssetPath(palette) : string.Empty;
        List<TileSourceData> sources = new List<TileSourceData>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            if (!item.includeInLevels)
            {
                continue;
            }

            sources.Add(new TileSourceData
            {
                assetPath = tileSourceItem.sourceAssetPath,
                spriteName = item.spriteName,
                tileX = item.tileX,
                tileY = item.tileY,
                colorCount = tileSourceItem.colorCount,
                autoColorCount = tileSourceItem.autoColorCount,
                autoBackgroundColor = tileSourceItem.autoBackgroundColor,
                backgroundColor = tileSourceItem.backgroundColor,
                targetPaletteAssetPath = palettePath,
                tilePalette = item.palette != null ? (Color32[])item.palette.Clone() : new Color32[0]
            });
        }

        return sources.ToArray();
    }

    private void DisposeComposedPreview()
    {
        if (composedPreviewTexture != null)
        {
            DestroyImmediate(composedPreviewTexture);
            composedPreviewTexture = null;
        }
    }

    private void LoadFolder()
    {
        string absoluteFolder = EditorUtility.OpenFolderPanel("Select Image Folder", Application.dataPath, string.Empty);
        if (string.IsNullOrEmpty(absoluteFolder))
        {
            return;
        }

        string projectFolder = ToProjectRelativePath(absoluteFolder);
        if (string.IsNullOrEmpty(projectFolder))
        {
            EditorUtility.DisplayDialog(
                "Load Folder",
                "Folder must be inside the Unity project (under Assets).",
                "OK");
            return;
        }

        string[] files = Directory.GetFiles(absoluteFolder);
        int addedCount = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string extension = Path.GetExtension(files[i]);
            if (!IsImageExtension(extension))
            {
                continue;
            }

            string assetPath = Path.Combine(projectFolder, Path.GetFileName(files[i])).Replace('\\', '/');
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                continue;
            }

            if (TryAddTexture(texture, assetPath))
            {
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            EditorUtility.DisplayDialog("Load Folder", "No supported images found in the selected folder.", "OK");
            return;
        }

        selectedIndex = items.Count - 1;
        Repaint();
    }

    private void AddSingleTexture()
    {
        string path = EditorUtility.OpenFilePanelWithFilters(
            "Select Image",
            Application.dataPath,
            new[] { "Image files", "png,jpg,jpeg" });
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string assetPath = ToProjectRelativePath(path);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Add Texture", "Image must be inside the Unity project (under Assets).", "OK");
            return;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Add Texture", "Could not load texture at:\n" + assetPath, "OK");
            return;
        }

        if (TryAddTexture(texture, assetPath))
        {
            if (conversionMode == ConversionMode.Tiles)
            {
                selectedIndex = -1;
            }
            else
            {
                selectedIndex = items.Count - 1;
            }
            Repaint();
        }
    }

    private bool TryAddTexture(Texture2D texture, string assetPath)
    {
        if (conversionMode == ConversionMode.Tiles)
        {
            SetTileSource(texture, assetPath);
            return true;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].sourceAssetPath == assetPath)
            {
                return false;
            }
        }

        ConversionItem item = CreateItemFromTexture(texture, assetPath);
        items.Add(item);
        return true;
    }

    private ConversionItem CreateItemFromTexture(Texture2D texture, string assetPath)
    {
        ConversionItem item = new ConversionItem
        {
            sourceTexture = texture,
            sourceAssetPath = assetPath,
            levelName = Path.GetFileNameWithoutExtension(assetPath)
        };
        ApplyDefaultsToItem(item);
        return item;
    }

    private void ApplyDefaultsToItem(ConversionItem item)
    {
        item.gridWidth = defaultGridWidth;
        item.gridHeight = defaultGridHeight;
        item.colorCount = defaultColorCount;
        item.autoColorCount = defaultAutoColorCount;
        item.previewScale = defaultPreviewScale;
        item.backgroundColor = defaultBackgroundColor;
        item.autoBackgroundColor = defaultAutoBackgroundColor;
        item.targetPalette = defaultTargetPalette;
    }

    private void RemoveSelectedItem()
    {
        if (conversionMode == ConversionMode.Tiles)
        {
            if (selectedIndex == -1)
            {
                if (tileSourceItem != null)
                {
                    tileSourceItem.DisposeAll();
                    tileSourceItem = null;
                }

                ClearTileResults();
                composedIsGenerated = false;
                DisposeComposedPreview();
                selectedIndex = -1;
                return;
            }

            if (selectedIndex >= 0 && selectedIndex < items.Count)
            {
                items[selectedIndex].DisposeAll();
                items.RemoveAt(selectedIndex);
                selectedIndex = Mathf.Clamp(selectedIndex, -1, items.Count - 1);
                composedIsGenerated = false;
                DisposeComposedPreview();
            }

            return;
        }

        if (selectedIndex < 0 || selectedIndex >= items.Count)
        {
            return;
        }

        items[selectedIndex].DisposeAll();
        items.RemoveAt(selectedIndex);
        selectedIndex = Mathf.Clamp(selectedIndex, 0, items.Count - 1);
        if (items.Count == 0)
        {
            selectedIndex = -1;
        }
    }

    private void ClearAllItems()
    {
        ClearTileResults();
        if (tileSourceItem != null)
        {
            tileSourceItem.DisposeAll();
            tileSourceItem = null;
        }

        selectedIndex = -1;
        composedIsGenerated = false;
        DisposeComposedPreview();
        ClearColorReplaceSelection();
        composedPalette = new Color32[0];
        composedCells = new byte[0];
        composedBuckets = new BucketSpawn[0];
    }

    private void GenerateAll()
    {
        try
        {
            int processed = 0;
            int total = CountIncludedItems();
            for (int i = 0; i < items.Count; i++)
            {
                ConversionItem item = items[i];
                if (!item.includeInLevels || item.sourceTexture == null)
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Generate All Images",
                    item.levelName,
                    total > 0 ? (float)processed / total : 1f);

                GeneratePreview(item);
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
    }

    private void GeneratePreview(ConversionItem item)
    {
        if (item.sourceTexture == null)
        {
            return;
        }

        Texture2D readableTexture = EnsureReadable(item.sourceTexture);
        ImageProcessor.Settings settings = ImageProcessor.Settings.Create(item.gridWidth, item.gridHeight, item.colorCount);
        settings.autoColorCount = item.autoColorCount;
        settings.autoBackgroundColor = item.autoBackgroundColor;
        settings.backgroundColor = item.backgroundColor;
        settings.targetPalette = GetActivePalette(item);
        ImageProcessor.Result result = ImageProcessor.Process(readableTexture, settings);

        if (!ReferenceEquals(readableTexture, item.sourceTexture))
        {
            DestroyImmediate(readableTexture);
        }

        item.detectedBackgroundColor = result.letterboxColor;
        if (item.autoColorCount && result.palette != null)
        {
            item.colorCount = result.palette.Length;
        }

        item.palette = result.palette;
        item.cells = result.cellIndices;
        item.buckets = BuildBucketSpawnsFromCells(item.cells, item.palette.Length);
        item.DisposePreview();
        item.previewTexture = ImageProcessor.BuildTexture(result, item.previewScale);
        item.isGenerated = true;
        Repaint();
    }

    private void SaveAllGenerated()
    {
        List<LevelData> savedLevels = new List<LevelData>();
        int savedCount = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ConversionItem item = items[i];
            if (!item.includeInLevels || !item.isGenerated)
            {
                continue;
            }

            if (SaveLevelAsset(item, out LevelData savedLevel, showDialog: false))
            {
                savedLevels.Add(savedLevel);
                savedCount++;
            }
        }

        RegisterLevelsInDatabase(savedLevels);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Save All",
            string.Format(
                "Saved {0} level(s) to Assets/Levels.\nAdded {1} level(s) to {2}.",
                savedCount,
                savedLevels.Count,
                targetLevelDatabase != null ? targetLevelDatabase.name : "Level Database (not assigned)"),
            "OK");
    }

    private void SaveLevelAsset(ConversionItem item)
    {
        if (SaveLevelAsset(item, out LevelData savedLevel, showDialog: true))
        {
            RegisterLevelsInDatabase(new[] { savedLevel });
            AssetDatabase.SaveAssets();
        }
    }

    private bool SaveLevelAsset(
        ConversionItem item,
        out LevelData savedLevel,
        bool showDialog = true,
        string targetFolder = null)
    {
        savedLevel = null;
        if (!item.includeInLevels)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Save Level", "This image is excluded from the level list.", "OK");
            }

            return false;
        }

        if (!item.isGenerated || item.cells == null || item.cells.Length == 0)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Save Level", "Generate preview before saving.", "OK");
            }

            return false;
        }

        if (item.previewTexture == null)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Save Level", "Generate preview before saving.", "OK");
            }

            return false;
        }

        string levelsFolder = EnsureLevelsFolder();
        string folder = string.IsNullOrEmpty(targetFolder) ? levelsFolder : targetFolder;

        string safeName = SanitizeAssetName(string.IsNullOrWhiteSpace(item.levelName)
            ? Path.GetFileNameWithoutExtension(item.sourceAssetPath)
            : item.levelName.Trim());

        int expectedCellCount = item.gridWidth * item.gridHeight;
        if (item.cells.Length != expectedCellCount)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Save Level",
                    string.Format(
                        "Cell data size ({0}) does not match grid ({1}x{2}). Regenerate preview before saving.",
                        item.cells.Length,
                        item.gridWidth,
                        item.gridHeight),
                    "OK");
            }

            return false;
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, safeName + ".asset"));

        LevelData levelData = CreateInstance<LevelData>();
        levelData.gridWidth = item.gridWidth;
        levelData.gridHeight = item.gridHeight;
        levelData.palette = item.palette;
        levelData.cells = item.cells;
        levelData.buckets = item.buckets;
        ApplyBucketLayoutRules(levelData, GetPredictedLevelIndexForNewLevel());

        AssetDatabase.CreateAsset(levelData, assetPath);

        WriteAndLinkPreview(levelData, item.previewTexture, assetPath);
        savedLevel = levelData;

        if (showDialog)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Save Level", "Level saved to:\n" + assetPath, "OK");
            Selection.activeObject = levelData;
        }

        return true;
    }

    private void DrawPreviewSection(ConversionItem item)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        const float maxDisplaySize = 280f;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Source", EditorStyles.miniLabel);
        if (item.sourceTexture != null)
        {
            DrawTexturePreview(item.sourceTexture, maxDisplaySize, allowUpscale: false);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if (item.previewTexture != null)
        {
            EditorGUILayout.LabelField(
                string.Format(
                    "Generated (grid {0}x{1}, texture {2}x{3})",
                    item.gridWidth,
                    item.gridHeight,
                    item.previewTexture.width,
                    item.previewTexture.height),
                EditorStyles.miniLabel);
            DrawTexturePreview(item.previewTexture, maxDisplaySize, allowUpscale: false);
        }
        else
        {
            float aspect = item.gridHeight > 0 ? item.gridWidth / (float)item.gridHeight : 1f;
            float placeholderWidth = aspect >= 1f ? maxDisplaySize : maxDisplaySize * aspect;
            float placeholderHeight = aspect >= 1f ? maxDisplaySize / aspect : maxDisplaySize;
            EditorGUILayout.LabelField(
                string.Format("Generated (grid {0}x{1})", item.gridWidth, item.gridHeight),
                EditorStyles.miniLabel);
            Rect placeholder = GUILayoutUtility.GetRect(placeholderWidth, placeholderHeight);
            EditorGUI.DrawRect(placeholder, new Color(0.15f, 0.15f, 0.15f));
            GUI.Label(placeholder, "Generate preview to see result", EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPaletteSection(ConversionItem item)
    {
        if (!item.isGenerated || item.palette == null || item.palette.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Palette & Buckets (click color to copy)", EditorStyles.boldLabel);

        byte[] usedColors = LevelData.FilterUsedPaletteColors(item.cells, item.palette.Length);
        for (int i = 0; i < usedColors.Length; i++)
        {
            byte colorIndex = usedColors[i];
            int grainCount = CountGrains(item, colorIndex);
            int bucketCount = GetBucketCount(item, colorIndex);
            Color32 color = item.palette[colorIndex - 1];

            EditorGUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
            EditorGUI.DrawRect(colorRect, color);
            EditorGUIUtility.AddCursorRect(colorRect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && colorRect.Contains(Event.current.mousePosition))
            {
                CopyColorToClipboard(color);
                Event.current.Use();
            }

            EditorGUILayout.LabelField(
                new GUIContent(
                    string.Format("Color {0}: {1} cells, {2} buckets  ({3})", colorIndex, grainCount, bucketCount, FormatColorForClipboard(color)),
                    "Click the color square to copy"),
                GUILayout.MinWidth(120f));
            EditorGUILayout.EndHorizontal();
        }
    }

    private static BucketSpawn[] BuildBucketSpawnsFromCells(byte[] cells, int paletteLength)
    {
        byte[] usedColors = LevelData.FilterUsedPaletteColors(cells, paletteLength);
        BucketSpawn[] spawns = new BucketSpawn[usedColors.Length];
        for (int i = 0; i < usedColors.Length; i++)
        {
            spawns[i] = new BucketSpawn
            {
                colorIndex = usedColors[i],
                count = 1
            };
        }

        return spawns;
    }

    private void CopyColorToClipboard(Color32 color)
    {
        string text = FormatColorForClipboard(color);
        EditorGUIUtility.systemCopyBuffer = text;
        ShowNotification(new GUIContent("Copied: " + text), 1.5);
    }

    private static string FormatColorForClipboard(Color32 color)
    {
        if (color.a == 255)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.r, color.g, color.b);
        }

        return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color.r, color.g, color.b, color.a);
    }

    private SandColorPalette GetActivePalette(ConversionItem item)
    {
        if (item.targetPalette != null)
        {
            return item.targetPalette;
        }

        return defaultTargetPalette;
    }

    private void DrawTargetPalettePreview(SandColorPalette palette)
    {
        Color32[] colors = palette.GetColors();
        if (colors.Length == 0)
        {
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Target Colors");
        for (int i = 0; i < colors.Length; i++)
        {
            Rect rect = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f));
            EditorGUI.DrawRect(rect, colors[i]);
        }
        EditorGUILayout.EndHorizontal();
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string normalizedAbsolute = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string normalizedDataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        if (!normalizedAbsolute.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "Assets" + normalizedAbsolute.Substring(normalizedDataPath.Length);
    }

    private static bool IsImageExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        string lower = extension.ToLowerInvariant();
        for (int i = 0; i < ImageExtensions.Length; i++)
        {
            if (lower == ImageExtensions[i])
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTextureSizeLabel(Texture2D texture)
    {
        if (texture == null)
        {
            return "?";
        }

        return string.Format("{0}x{1}", texture.width, texture.height);
    }

    private static Texture2D EnsureReadable(Texture2D texture)
    {
        try
        {
            texture.GetPixel(0, 0);
            return texture;
        }
        catch
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, renderTexture);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return copy;
        }
    }

    private void RegisterLevelsInDatabase(IList<LevelData> levels)
    {
        if (targetLevelDatabase == null || levels == null || levels.Count == 0)
        {
            return;
        }

        List<LevelData> merged = new List<LevelData>();
        if (targetLevelDatabase.levels != null)
        {
            for (int i = 0; i < targetLevelDatabase.levels.Length; i++)
            {
                LevelData existing = targetLevelDatabase.levels[i];
                if (existing != null)
                {
                    merged.Add(existing);
                }
            }
        }

        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            if (level == null || merged.Contains(level))
            {
                continue;
            }

            int levelIndex = merged.Count;
            ApplyBucketLayoutRules(level, levelIndex);
            EditorUtility.SetDirty(level);
            merged.Add(level);
        }

        targetLevelDatabase.levels = merged.ToArray();
        EditorUtility.SetDirty(targetLevelDatabase);
    }

    private int GetPredictedLevelIndexForNewLevel()
    {
        if (targetLevelDatabase == null || targetLevelDatabase.levels == null)
        {
            return 0;
        }

        return targetLevelDatabase.levels.Length;
    }

    private void ApplyBucketLayoutRules(LevelData levelData, int levelIndex)
    {
        if (levelData == null)
        {
            return;
        }

        LevelSegmentDatabase segmentDatabase = targetLevelDatabase != null
            ? targetLevelDatabase.segmentDatabase
            : null;
        LevelBucketLayoutGenerator.ApplyForLevelIndex(levelData, levelIndex, segmentDatabase);
    }

    private void ApplySegmentDefaultsForNextLevel()
    {
        if (targetLevelDatabase == null)
        {
            return;
        }

        LevelSegmentRules segment = targetLevelDatabase.GetSegmentRulesForLevel(GetPredictedLevelIndexForNewLevel());
        if (segment == null)
        {
            return;
        }

        if (segment.defaultPalette != null)
        {
            defaultTargetPalette = segment.defaultPalette;
        }

        defaultGridWidth = Mathf.Clamp(segment.defaultGridWidth, LevelData.MinGridDimension, LevelData.MaxGridDimension);
        defaultGridHeight = Mathf.Clamp(segment.defaultGridHeight, LevelData.MinGridDimension, LevelData.MaxGridDimension);
        defaultColorCount = Mathf.Clamp(segment.defaultColorCount, 2, SandColorPalette.MaxColorCount);
        defaultAutoColorCount = segment.defaultAutoColorCount;
    }

    private static string EnsureLevelsFolder()
    {
        const string levelsFolder = "Assets/Levels";
        if (!AssetDatabase.IsValidFolder(levelsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        return levelsFolder;
    }

    private static string EnsurePreviewsFolder()
    {
        string levelsFolder = EnsureLevelsFolder();
        return EnsureAssetSubfolder(levelsFolder, "Previews");
    }

    private static string GetPreviewPathForAsset(string assetPath)
    {
        string previewsFolder = EnsurePreviewsFolder();
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        string desiredPath = previewsFolder + "/" + baseName + "_preview.png";
        return MakeUniqueFilePath(desiredPath);
    }

    private static string MakeUniqueFilePath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        string directory = Path.GetDirectoryName(desiredPath);
        string name = Path.GetFileNameWithoutExtension(desiredPath);
        string extension = Path.GetExtension(desiredPath);
        int suffix = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, string.Format("{0}_{1}{2}", name, suffix, extension)).Replace('\\', '/');
            suffix++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    private static string EnsureAssetSubfolder(string parentFolder, string subfolderName)
    {
        string safeSubfolder = SanitizeAssetName(subfolderName);
        string folderPath = parentFolder + "/" + safeSubfolder;
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parentFolder, safeSubfolder);
        }

        return folderPath;
    }

    private static string SanitizeAssetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Level";
        }

        name = name.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            name = name.Replace(invalidChars[i], '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "Level" : name;
    }

    private static void WriteAndLinkPreview(LevelData levelData, Texture2D previewSource, string assetPath)
    {
        if (levelData == null || previewSource == null)
        {
            return;
        }

        string previewPath = GetPreviewPathForAsset(assetPath);
        byte[] pngData = previewSource.EncodeToPNG();
        File.WriteAllBytes(previewPath, pngData);
        AssetDatabase.ImportAsset(previewPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigurePreviewImporter(previewPath);

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(previewPath);
        if (imported != null)
        {
            levelData.previewTexture = imported;
            EditorUtility.SetDirty(levelData);
        }
    }

    private static void ConfigurePreviewImporter(string previewPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(previewPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void DrawTexturePreview(Texture2D texture, float maxSize, bool allowUpscale)
    {
        maxSize = Mathf.Max(64f, maxSize);
        float scale = Mathf.Min(maxSize / texture.width, maxSize / texture.height);
        if (allowUpscale)
        {
            scale = Mathf.Max(1f, Mathf.Floor(scale));
        }
        else if (scale > 1f)
        {
            scale = 1f;
        }

        float displayWidth = texture.width * scale;
        float displayHeight = texture.height * scale;
        Rect rect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
        GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
    }

    private static int CountGrains(ConversionItem item, byte colorIndex)
    {
        int count = 0;
        for (int i = 0; i < item.cells.Length; i++)
        {
            if (item.cells[i] == colorIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetBucketCount(BucketSpawn[] buckets, byte colorIndex)
    {
        for (int i = 0; i < buckets.Length; i++)
        {
            if (buckets[i].colorIndex == colorIndex)
            {
                return buckets[i].count;
            }
        }

        return 0;
    }

    private static int CountCells(byte[] cells, byte colorIndex)
    {
        int count = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == colorIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static void DrawColorCountField(ref int colorCount, ref bool autoColorCount, SandColorPalette activePalette)
    {
        autoColorCount = EditorGUILayout.Toggle("Auto Color Count", autoColorCount);
        int colorMax = activePalette != null
            ? Mathf.Max(2, activePalette.GetColorCount())
            : SandColorPalette.MaxColorCount;
        using (new EditorGUI.DisabledScope(autoColorCount))
        {
            colorCount = EditorGUILayout.IntSlider("Color Count", colorCount, 2, colorMax);
        }

        if (autoColorCount)
        {
            EditorGUILayout.LabelField("Colors are detected automatically from the image", EditorStyles.miniLabel);
        }
    }

    private static int GetBucketCount(ConversionItem item, byte colorIndex)
    {
        for (int i = 0; i < item.buckets.Length; i++)
        {
            if (item.buckets[i].colorIndex == colorIndex)
            {
                return item.buckets[i].count;
            }
        }

        return 0;
    }
}
