using System;
using UnityEngine;

/// <summary>One progression policy for authored levels, fresh runs and lesson hints.</summary>
public static class CampaignProgression
{
    public const int LevelCount = 1000, WinterLevelCount = 200;
    public const int IntroFrostMoves = 3;
    public const int MysteryLevel = 12, LinkedLevel = 20, IceLevel = 30, KeysLevel = 40;
    public const int SectionsLevel = 55, CratesLevel = 70, LargeLevel = 85, SwapLevel = 100;
    public const int HiddenSectionsLevel = 120, LoopLevel = 140, HiddenLinkedLevel = 160, MultiSwapLevel = 180;
    private static readonly int[] LessonStarts = { 1, MysteryLevel, LinkedLevel, IceLevel, KeysLevel,
        SectionsLevel, CratesLevel, LargeLevel, SwapLevel, HiddenSectionsLevel, LoopLevel, HiddenLinkedLevel, MultiSwapLevel };
    private static readonly string[] Lessons = { "tutorial_pick", "lesson_mystery", "lesson_linked", "lesson_ice", "lesson_keys",
        "lesson_sections", "lesson_crates", "lesson_large", "lesson_swap", "lesson_hidden_sections", "lesson_loop", "lesson_hidden_linked", "lesson_swap_group" };

    public static bool HasHiddenSections(LevelData level, int number)
        => number >= HiddenSectionsLevel && level.mysteryBuckets && level.sectionBuckets;
    public static bool HasHiddenLinked(LevelData level, int number)
        => number >= HiddenLinkedLevel && level.mysteryBuckets && level.linkedBuckets;
    public static int GetStage(int index) => Mathf.Max(0, index / 2);
    public static bool IsLessonStart(int index) => Array.IndexOf(LessonStarts, index + 1) >= 0;
    public static string GetLessonId(int index)
    {
        int intro = Array.IndexOf(LessonStarts, index + 1);
        return intro >= 0 ? Lessons[intro] : "challenge_" + GetStage(index);
    }
    public static string GetLesson(int index)
    {
        int intro = Array.IndexOf(LessonStarts, index + 1);
        return intro >= 0 ? string.Format(CampaignText.Get(Lessons[intro]), IntroFrostMoves) : string.Empty;
    }
    private static bool HasCrates(int number, bool loop)
        => !loop && number >= CratesLevel && (number - CratesLevel) % 5 == 0
            && (number == CratesLevel || !IsLessonStart(number - 1));
    public static int SectionBucketCount(int number)
        => number < SectionsLevel ? 0 : Mathf.Clamp(2 + (number - SectionsLevel) / 12, 2, 5);
    public static int LinkedPairCount(int number)
        => number < LinkedLevel ? 0 : number <= LinkedLevel + 2 ? 1 : number < SectionsLevel ? 2 : 3;
    public static bool AllBucketsSwapColors(int number) => number >= 200 && (number - 200) % 40 == 0;
    public static int SwapBucketCount(int number, int availableBuckets)
    {
        if (AllBucketsSwapColors(number)) return availableBuckets;
        if (number < SwapLevel) return 0;
        if (number < MultiSwapLevel) return 2;
        int variation = (number - MultiSwapLevel - 1 + 12) % 12;
        return variation == 5 ? 6 : variation == 9 ? 8 : 4;
    }
    public static int FrostMovesFor(int number)
        => number < IceLevel ? 0 : Mathf.Min(IntroFrostMoves + (number - IceLevel) / 10, 8);

    public static void Configure(LevelData level, LevelSegmentRules rules, int index)
    {
        level.campaignLevel = true;
        level.bucketSettingsOverride = rules;
        level.campaignSeed = unchecked(7319 + index * 104729);
        ApplyPersistentRules(level, index);
        rules.startLevelIndex = index; rules.levelCount = 1;
        rules.segmentId = "campaign_" + (index + 1).ToString("000");
        rules.displayName = "Chapter " + (GetStage(index) + 1);
        int targetBuckets = index < 2 ? 10 : 18 + Mathf.Min((index - 2) / 8, 5) * 2;
        rules.maxGrainsPerBucket = Mathf.Max(16, Mathf.CeilToInt(level.CountTotalLevelCells() / (float)targetBuckets));
        level.bucketSettingsOverride = rules;
        SynchronizeLayoutRules(level, index);
    }

    public static void ApplyPersistentRules(LevelData level, int index)
    {
        int number = index + 1;
        bool varied = number > MultiSwapLevel;
        int variation = Mathf.Max(0, number - MultiSwapLevel - 1) % 12;
        level.beltCapacity = ConveyorBelt.MaxOnBelt;
        level.beltSpeedMultiplier = 1f;
        level.mysteryBuckets = number >= MysteryLevel && (!varied || variation == 1 || variation == 6 || variation == 9 || variation == 11);
        level.linkedBuckets = number >= LinkedLevel && (!varied || variation == 2 || variation == 5 || variation == 8 || variation == 11);
        level.frostMoves = FrostMovesFor(number);
        level.keyGates = number >= KeysLevel && (!varied || variation == 1 || variation == 4 || variation == 8 || variation == 11);
        level.sectionBuckets = number >= SectionsLevel && (!varied || variation % 3 != 1);
        level.largeBuckets = number >= LargeLevel && (!varied || variation == 2 || variation == 6 || variation == 10);
        level.swappingColors = number >= SwapLevel && (!varied || variation == 5 || variation == 7 || variation == 9) || AllBucketsSwapColors(number);
        level.loopInventory = number >= LoopLevel && (number <= LoopLevel + 2
            || !IsLessonStart(index) && (number - LoopLevel) % 6 == 3);
        if (level.loopInventory)
        {
            level.keyGates = level.linkedBuckets = level.largeBuckets = false;
            if (number < MultiSwapLevel) level.swappingColors = false;
        }
        level.alternatingCrates = HasCrates(number, level.loopInventory);
        if (level.alternatingCrates) level.keyGates = level.linkedBuckets = false;
        if (AllBucketsSwapColors(number))
        {
            // Dedicated full-stock shuffle challenges alternate with mixed-mechanic levels.
            level.mysteryBuckets = level.linkedBuckets = level.keyGates = level.sectionBuckets = false;
            level.largeBuckets = level.loopInventory = level.alternatingCrates = false;
            level.frostMoves = 0;
        }
        level.EnsureBucketLayout(); level.EnsureConveyorMetadata();
        Array.Clear(level.bucketLayout, 0, level.bucketLayout.Length);
        Array.Clear(level.conveyorDirections, 0, level.conveyorDirections.Length);
        Array.Clear(level.conveyorBucketCounts, 0, level.conveyorBucketCounts.Length);
        level.inventoryLoopCells = null;
        if (level.loopInventory) InventoryLoopRoute.ApplyStructure(level, number);
        SynchronizeLayoutRules(level, index);
    }

    private static void SynchronizeLayoutRules(LevelData level, int index)
    {
        var rules = level.bucketSettingsOverride;
        if (rules == null) return;
        rules.startLevelIndex = index; rules.levelCount = 1;
        rules.segmentId = "campaign_" + (index + 1).ToString("000");
        rules.fillMode = BucketLayoutFillMode.UseTemplateAsIs;
        rules.randomComponents = Array.Empty<BucketLayoutComponent>();
        rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
        rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
        rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
    }
}
