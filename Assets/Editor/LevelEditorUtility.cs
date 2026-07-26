using System.IO;
using UnityEditor;
using UnityEngine;

public static class LevelEditorUtility
{
    private const string SegmentsFolder = "Assets/Levels/Segments";
    private const string SegmentRulesPath = SegmentsFolder + "/Segment_01.asset";
    private const string SegmentDatabasePath = SegmentsFolder + "/LevelSegmentDatabase.asset";

    public static bool TryFindLevelInDatabase(LevelData level, out LevelDatabase database, out int levelIndex)
    {
        database = null;
        levelIndex = -1;

        if (level == null)
        {
            return false;
        }

        string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
        for (int g = 0; g < guids.Length; g++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[g]);
            LevelDatabase candidate = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
            if (candidate == null || candidate.levels == null)
            {
                continue;
            }

            for (int i = 0; i < candidate.levels.Length; i++)
            {
                if (candidate.levels[i] == level)
                {
                    database = candidate;
                    levelIndex = i;
                    return true;
                }
            }
        }

        return false;
    }

    public static void ApplyBucketLayoutRules(LevelData level)
    {
        if (level == null)
        {
            return;
        }

        if (TryFindLevelInDatabase(level, out LevelDatabase database, out int levelIndex))
        {
            database.ApplyBucketLayoutRules(level, levelIndex);
            return;
        }

        LevelBucketLayoutGenerator.Apply(level, level.bucketSettingsOverride);
    }

    [MenuItem("Window/Sand/Open Level Segments Folder")]
    private static void OpenLevelSegmentsFolder()
    {
        EnsureDefaultSegmentSetup();
        SelectSegmentsFolder();
    }

    [MenuItem("Window/Sand/Repair Segment Assets")]
    private static void RepairSegmentAssetsMenu()
    {
        EnsureDefaultSegmentSetup(forceRecreate: true);
        EditorUtility.DisplayDialog(
            "Segment Assets",
            "Segment assets were recreated in Assets/Levels/Segments.",
            "OK");
    }

    [MenuItem("Assets/Create/Sand/Default Level Segment Setup")]
    private static void CreateDefaultSegmentSetupMenu()
    {
        EnsureDefaultSegmentSetup();
    }

    public static void EnsureDefaultSegmentSetup(bool forceRecreate = false)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Levels"))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }

        if (!AssetDatabase.IsValidFolder(SegmentsFolder))
        {
            AssetDatabase.CreateFolder("Assets/Levels", "Segments");
        }

        if (forceRecreate || SegmentAssetsNeedRepair())
        {
            RemoveBrokenSegmentAssetFiles();
        }

        LevelSegmentRules segmentRules = AssetDatabase.LoadAssetAtPath<LevelSegmentRules>(SegmentRulesPath);
        if (segmentRules == null)
        {
            segmentRules = ScriptableObject.CreateInstance<LevelSegmentRules>();
            segmentRules.segmentId = "segment_01";
            segmentRules.displayName = "Levels 1+";
            segmentRules.startLevelIndex = 0;
            segmentRules.levelCount = 0;
            segmentRules.fillMode = BucketLayoutFillMode.FrequencyByRow;
            segmentRules.defaultGridWidth = 32;
            segmentRules.defaultGridHeight = 32;
            segmentRules.defaultColorCount = 4;
            segmentRules.defaultAutoColorCount = true;
            segmentRules.defaultPalette = AssetDatabase.LoadAssetAtPath<SandColorPalette>("Assets/Settings/SandColorPalette.asset");
            segmentRules.EnsureTemplate();
            AssetDatabase.CreateAsset(segmentRules, SegmentRulesPath);
        }
        else
        {
            segmentRules.EnsureTemplate();
            EditorUtility.SetDirty(segmentRules);
        }

        LevelSegmentDatabase segmentDatabase = AssetDatabase.LoadAssetAtPath<LevelSegmentDatabase>(SegmentDatabasePath);
        if (segmentDatabase == null)
        {
            segmentDatabase = ScriptableObject.CreateInstance<LevelSegmentDatabase>();
            segmentDatabase.segments = new[] { segmentRules };
            AssetDatabase.CreateAsset(segmentDatabase, SegmentDatabasePath);
        }
        else if (segmentDatabase.segments == null || segmentDatabase.segments.Length == 0)
        {
            segmentDatabase.segments = new[] { segmentRules };
            EditorUtility.SetDirty(segmentDatabase);
        }

        LevelDatabase levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>("Assets/Levels/LevelDatabase.asset");
        if (levelDatabase != null)
        {
            if (levelDatabase.segmentDatabase != segmentDatabase)
            {
                levelDatabase.segmentDatabase = segmentDatabase;
                EditorUtility.SetDirty(levelDatabase);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SelectSegmentsFolder();
    }

    private static bool SegmentAssetsNeedRepair()
    {
        if (AssetDatabase.LoadAssetAtPath<LevelSegmentDatabase>(SegmentDatabasePath) != null)
        {
            return false;
        }

        if (File.Exists(SegmentDatabasePath))
        {
            return true;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { SegmentsFolder });
        return guids.Length == 0;
    }

    private static void RemoveBrokenSegmentAssetFiles()
    {
        DeleteAssetIfPresent(SegmentRulesPath);
        DeleteAssetIfPresent(SegmentDatabasePath);
        AssetDatabase.Refresh();
    }

    private static void DeleteAssetIfPresent(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
            return;
        }

        File.Delete(assetPath);
        string metaPath = assetPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    private static void SelectSegmentsFolder()
    {
        Object folderAsset = AssetDatabase.LoadAssetAtPath<Object>(SegmentsFolder);
        if (folderAsset != null)
        {
            Selection.activeObject = folderAsset;
            EditorGUIUtility.PingObject(folderAsset);
        }
    }
}
