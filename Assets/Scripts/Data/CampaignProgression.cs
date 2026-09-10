using UnityEngine;

/// <summary>Campaign introductions and combinations share one source of truth.</summary>
public static class CampaignProgression
{
    public const int LevelCount = 300;
    public const int IntroFrostMoves = 3;
    public const int FeederLevel = 12, SideFeedLevel = 14, IceLevel = 18;
    public const int SpeedLevel = 20, SectionsLevel = 25, DoubleFeedLevel = 24, MixLevel = 26;
    public const int KeysLevel = 28, LinkedLevel = 38, LargeLevel = 48, CapacityLevel = 50;
    public const int MysteryLevel = 50, CratesLevel = 58, SwapLevel = 68, LoopLevel = 80, AdvancedLevel = 90;
    private static readonly int[] LessonStarts = { 1, 3, FeederLevel, SideFeedLevel, IceLevel,
        SectionsLevel, KeysLevel, LinkedLevel, LargeLevel, CapacityLevel, CratesLevel, SwapLevel, LoopLevel };
    private static readonly string[] Lessons = { "tutorial_pick", "lesson_walls", "lesson_feed", "lesson_side",
        "lesson_ice", "lesson_sections", "lesson_keys", "lesson_linked",
        "lesson_large", "lesson_slots", "lesson_crates", "lesson_swap", "lesson_loop" };

    public static int GetStage(int index) => Mathf.Max(0, index / 2);
    public static bool IsLessonStart(int index)
        => System.Array.IndexOf(LessonStarts, index + 1) >= 0;
    public static string GetLessonId(int index)
    {
        int intro = System.Array.IndexOf(LessonStarts, index + 1);
        return intro >= 0 ? Lessons[intro] : "challenge_" + GetStage(index);
    }
    private static int CapacityFor(int number)
        => number < CapacityLevel ? 5 : number < AdvancedLevel ? 4 : 3 + ((number - AdvancedLevel) / 2) % 3;

    public static int SectionBucketCount(int number)
    {
        if (number < SectionsLevel) return 0;
        return Mathf.Clamp(2 + (number - SectionsLevel) / 12, 2, 5);
    }

    public static int FrostMovesFor(int number)
    {
        if (number < IceLevel) return 0;
        int frost = IntroFrostMoves + (number - IceLevel) / 10;
        if (number >= AdvancedLevel) frost += 1;
        return Mathf.Min(frost, 8);
    }

    public static void Configure(LevelData level, LevelSegmentRules rules, int index)
    {
        int number = index + 1;
        int pair = GetStage(index);
        int combination = Mathf.Max(0, (number - MixLevel) / 2);
        level.campaignLevel = true;
        level.sectionBuckets = number >= SectionsLevel;
        level.keyGates = number >= KeysLevel;
        level.linkedBuckets = number >= LinkedLevel;
        level.largeBuckets = number >= LargeLevel;
        level.swappingColors = number >= SwapLevel;
        level.loopInventory = number >= LoopLevel && (number < AdvancedLevel || combination % 2 == 0);
        // A stationary covered row and a moving perimeter are separate board variants.
        level.alternatingCrates = number >= CratesLevel && !level.loopInventory;
        level.campaignSeed = 7319 + index * 104729;
        level.beltCapacity = CapacityFor(number);
        level.beltSpeedMultiplier = number >= SpeedLevel && (number < SectionsLevel || combination % 4 == 3) ? 1.2f : 1f;
        level.mysteryBuckets = number >= MysteryLevel;
        if (number > 100)
        {
            // Alternate challenges and breathing room instead of stacking every modifier forever.
            int variation = (number - 101) % 12;
            level.loopInventory = variation == 3 || variation == 9;
            level.alternatingCrates = variation == 0 || variation == 4 || variation == 7 || variation == 10;
            level.sectionBuckets = variation % 3 != 1;
            level.keyGates = variation == 1 || variation == 4 || variation == 8 || variation == 11;
            level.linkedBuckets = variation == 2 || variation == 5 || variation == 8 || variation == 11;
            level.largeBuckets = variation == 2 || variation == 6 || variation == 10;
            level.swappingColors = variation == 5 || variation == 7 || variation == 9;
            level.mysteryBuckets = variation == 1 || variation == 6 || variation == 9 || variation == 11;
        }
        else if (level.loopInventory)
        {
            // The loop introduction needs room for movement and sections on its compact ring.
            level.keyGates = level.linkedBuckets = level.largeBuckets = level.swappingColors = false;
        }
        if (level.alternatingCrates)
        {
            // Crate timing is its own challenge; key dependencies and coupled cups
            // belong to other variants instead of trapping the covered queue.
            level.keyGates = level.linkedBuckets = false;
        }
        bool iceActive = number >= IceLevel && (number < SpeedLevel || number >= MixLevel && (combination & 2) == 0);
        level.frostMoves = iceActive && (number <= 100 || (number - 101) % 4 == 2) ? FrostMovesFor(number) : 0;
        level.EnsureBucketLayout();
        level.EnsureConveyorMetadata();
        System.Array.Clear(level.bucketLayout, 0, level.bucketLayout.Length);
        System.Array.Clear(level.conveyorDirections, 0, level.conveyorDirections.Length);
        System.Array.Clear(level.conveyorBucketCounts, 0, level.conveyorBucketCounts.Length);

        if (level.loopInventory)
        {
            InventoryLoopRoute.ApplyStructure(level, number);
        }
        else
        {
            ApplyUniqueBoardTemplate(level, number, pair, combination);
        }

        rules.startLevelIndex = index;
        rules.levelCount = 1;
        rules.segmentId = "campaign_" + number.ToString("000");
        rules.displayName = "Chapter " + (pair + 1);
        rules.fillMode = BucketLayoutFillMode.UseTemplateAsIs;
        rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
        rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
        rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
        int targetBuckets = number < 3 ? 10 : 18 + Mathf.Min((number - 3) / 8, 5) * 2;
        rules.maxGrainsPerBucket = Mathf.Max(16, Mathf.CeilToInt(level.CountTotalLevelCells() / (float)targetBuckets));
        level.bucketSettingsOverride = rules;
    }

    private static void ApplyUniqueBoardTemplate(LevelData level, int number, int pair, int combination)
    {
        // Wide family space so consecutive levels rarely share the same wall/feeder pattern.
        int layoutFamily = Mathf.Abs(number * 41 + pair * 17 + combination * 9 + level.campaignSeed) % 48;
        if (number >= 3)
        {
            int[] wallChoices = { 6, 7, 8, 11, 12, 13, 16, 17, 18, 21, 22, 23 };
            int wallA = wallChoices[layoutFamily % wallChoices.Length];
            level.bucketLayout[wallA] = LevelData.BucketLayoutWall;
            int wallB = -1;
            if (number >= DoubleFeedLevel || number >= 8 && layoutFamily % 5 == 0)
            {
                wallB = wallChoices[(layoutFamily * 3 + number) % wallChoices.Length];
                if (wallB == wallA) wallB = wallChoices[(wallA + 5) % wallChoices.Length];
                level.bucketLayout[wallB] = LevelData.BucketLayoutWall;
            }
            if (number >= MixLevel && layoutFamily % 4 == 2)
            {
                int wallC = wallChoices[(layoutFamily * 5 + 7) % wallChoices.Length];
                if (wallC != wallA && wallC != wallB) level.bucketLayout[wallC] = LevelData.BucketLayoutWall;
            }
        }

        if (number >= FeederLevel)
        {
            int[] feederSlots = { 5, 7, 9, 10, 11, 14, 15, 17, 19 };
            int slot = feederSlots[(layoutFamily + (number >= SideFeedLevel ? 2 : 0)) % feederSlots.Length];
            if (number >= SideFeedLevel && combination % 3 == 1)
                slot = feederSlots[(layoutFamily + 4) % feederSlots.Length];
            if (LevelData.IsBucketLayoutWall(level.bucketLayout[slot]))
                level.bucketLayout[slot] = 0;
            level.bucketLayout[slot] = LevelData.BucketLayoutConveyorSlot;
            ConveyorDirection dir = slot % 5 == 0 ? ConveyorDirection.Right
                : slot % 5 == 4 ? ConveyorDirection.Left
                : slot / 5 >= 3 ? ConveyorDirection.Front
                : (layoutFamily % 3 == 0 ? ConveyorDirection.Front
                    : layoutFamily % 3 == 1 ? ConveyorDirection.Left : ConveyorDirection.Right);
            level.SetConveyorDirection(slot, dir);
            level.SetConveyorBucketCount(slot, 2 + pair % 3);
        }

        if (number >= DoubleFeedLevel)
        {
            int[] seconds = { 9, 11, 13, 14, 16, 18 };
            int second = seconds[(layoutFamily + combination) % seconds.Length];
            if (LevelData.IsBucketLayoutWall(level.bucketLayout[second]) || LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[second]))
                second = FindFreeFeederSlot(level, second);
            level.bucketLayout[second] = LevelData.BucketLayoutConveyorSlot;
            level.SetConveyorDirection(second, layoutFamily % 2 == 0 ? ConveyorDirection.Front : ConveyorDirection.Left);
            level.SetConveyorBucketCount(second, 3 + pair % 2);
        }

        for (int i = 0; i < level.bucketLayout.Length; i++)
        {
            if (!LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[i])) continue;
            int exit = ConveyorDirectionUtility.GetTargetLayoutIndex(i, level.GetConveyorDirection(i));
            if (exit >= 0 && LevelData.IsBucketLayoutWall(level.bucketLayout[exit])) level.bucketLayout[exit] = 0;
        }

        if (level.alternatingCrates)
        {
            // Leave a connected approach immediately above the covered rear row.
            for (int i = 15; i < level.bucketLayout.Length; i++)
                if (LevelData.IsBucketLayoutWall(level.bucketLayout[i]))
                {
                    level.bucketLayout[i] = 0;
                    for (int slot = 10; slot < 15; slot++)
                        if (level.bucketLayout[slot] == 0) { level.bucketLayout[slot] = LevelData.BucketLayoutWall; break; }
                }
        }
    }

    private static int FindFreeFeederSlot(LevelData level, int preferred)
    {
        if (!LevelData.IsBucketLayoutWall(level.bucketLayout[preferred])
            && !LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[preferred]))
            return preferred;
        for (int i = 5; i < LevelData.BucketLayoutCellCount; i++)
            if (!LevelData.IsBucketLayoutWall(level.bucketLayout[i])
                && !LevelData.IsBucketLayoutConveyorSlot(level.bucketLayout[i]))
                return i;
        return preferred;
    }

    public static string GetLesson(int index)
    {
        int intro = System.Array.IndexOf(LessonStarts, index + 1);
        if (intro >= 0) return string.Format(CampaignText.Get(Lessons[intro]), IntroFrostMoves);
        return string.Empty;
    }
}
