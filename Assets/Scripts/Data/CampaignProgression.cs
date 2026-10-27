using System;
using System.Collections.Generic;
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
    [Flags]
    private enum Mechanic { None = 0, Mystery = 1, Linked = 2, Ice = 4, Keys = 8,
        Sections = 16, Crates = 32, Large = 64, Swap = 128, Loop = 256 }
    private static readonly Mechanic[] Mechanics = { Mechanic.Mystery, Mechanic.Linked, Mechanic.Ice,
        Mechanic.Keys, Mechanic.Sections, Mechanic.Crates, Mechanic.Large, Mechanic.Swap, Mechanic.Loop };
    private static readonly int[] Unlocks = { MysteryLevel, LinkedLevel, IceLevel, KeysLevel,
        SectionsLevel, CratesLevel, LargeLevel, SwapLevel, LoopLevel };
    private static readonly List<Mechanic> Plans = new List<Mechanic> { Mechanic.None };
    private static int lastCrates, lastLoop;

    // Keep lessons small, then practice frequently while rotating familiar combinations.
    private static Mechanic Plan(int number)
    {
        number = Mathf.Max(1, number);
        while (Plans.Count <= number)
        {
            int n = Plans.Count, unlocked = 0;
            while (unlocked < Unlocks.Length && Unlocks[unlocked] <= n) unlocked++;
            Mechanic next = Mechanic.None;
            int lesson = Array.IndexOf(Unlocks, n);
            if (lesson >= 0) next = Mechanics[lesson];
            else if (n == HiddenSectionsLevel) next = Mechanic.Mystery | Mechanic.Sections;
            else if (n == HiddenLinkedLevel) next = Mechanic.Mystery | Mechanic.Linked;
            else if (n == MultiSwapLevel) next = Mechanic.Swap;
            else if (unlocked > 0 && (n < 32 || n >= 70 || n % 12 != 11))
            {
                int newest = unlocked - 1;
                int preferred = n - Unlocks[newest] <= 7 && (n % 3 != 2 || unlocked == 1)
                    ? newest : n % unlocked;
                for (int offset = 0; offset < unlocked; offset++)
                {
                    var candidate = Mechanics[(preferred + offset) % unlocked];
                    if (candidate == Mechanic.Crates && n - lastCrates < 3
                        || candidate == Mechanic.Loop && n - lastLoop < 3) continue;
                    next = candidate;
                    break;
                }
                int lessonNumber = RecentLesson(n);
                int age = n - lessonNumber;
                var introduced = LessonMechanics(lessonNumber);
                if (age <= 7 && age > 0 && age % 2 == 0
                    && (lessonNumber == HiddenSectionsLevel || lessonNumber == HiddenLinkedLevel || lessonNumber == MultiSwapLevel))
                    next = introduced;
                int target = n >= 220 ? (n % 4 == 0 ? 4 : 3)
                    : n >= 200 && n % 3 == 0 ? 3 : n >= 70 ? (n % 5 == 1 ? 1 : 2) : n % 3 != 1 ? 2 : 1;
                if (age <= 7)
                    target = age >= 2 && (next & introduced) == introduced
                        && (age % 2 == 0 || age == 3 && (next == Mechanic.Crates || next == Mechanic.Loop)) ? 2 : 1;
                for (int offset = 0; offset < unlocked && CountMechanics(next) < target; offset++)
                {
                    int choice = (n / 3 + offset) % unlocked;
                    var candidate = Mechanics[choice];
                    if (n - Unlocks[choice] < 8 || (candidate & next) != 0 || !CanCombine(next, candidate)) continue;
                    if (age <= 7 && candidate == Mechanic.Loop) continue;
                    // Dense levels use one family of two-slot dispatches at a time.
                    if (target >= 3 && ((next | candidate) & (Mechanic.Linked | Mechanic.Large))
                        == (Mechanic.Linked | Mechanic.Large)) continue;
                    if (candidate == Mechanic.Crates && n - lastCrates < 3
                        || candidate == Mechanic.Loop && n - lastLoop < 3) continue;
                    next |= candidate;
                }
            }
            // Accent levels arrive after lesson practice, with familiar supporting types.
            if (IsFullMysteryLevel(n))
                next = Mechanic.Mystery | (n >= HiddenSectionsLevel ? Mechanic.Sections : Mechanic.None)
                    | (n >= 220 ? Mechanic.Ice | Mechanic.Swap : Mechanic.None);
            else if (IsLinkedFocusLevel(n))
                next = Mechanic.Linked | (n >= SwapLevel ? Mechanic.Swap : Mechanic.None)
                    | (n >= 220 ? Mechanic.Ice : Mechanic.None);
            if ((next & Mechanic.Crates) != 0) lastCrates = n;
            if ((next & Mechanic.Loop) != 0) lastLoop = n;
            Plans.Add(next);
        }
        return Plans[number];
    }

    public static bool IsFullMysteryLevel(int number)
        => number >= 78 && number % 24 == 8 && number - RecentLesson(number) >= 8;
    public static bool IsLinkedFocusLevel(int number)
        => number >= 78 && number % 24 == 6 && number - RecentLesson(number) >= 8;

    private static int RecentLesson(int number)
    {
        for (int i = LessonStarts.Length - 1; i >= 0; i--)
            if (LessonStarts[i] <= number) return LessonStarts[i];
        return 1;
    }
    private static int CountMechanics(Mechanic plan)
    {
        int count = 0;
        for (int bits = (int)plan; bits != 0; bits &= bits - 1) count++;
        return count;
    }
    private static bool CanCombine(Mechanic plan, Mechanic candidate)
    {
        var combined = plan | candidate;
        if ((combined & (Mechanic.Linked | Mechanic.Keys)) == (Mechanic.Linked | Mechanic.Keys)) return false;
        if ((combined & (Mechanic.Crates | Mechanic.Loop)) == (Mechanic.Crates | Mechanic.Loop)) return false;
        return (combined & (Mechanic.Crates | Mechanic.Loop)) == 0
            || (combined & (Mechanic.Keys | Mechanic.Linked | Mechanic.Large)) == 0;
    }
    private static Mechanic LessonMechanics(int number)
    {
        if (number == HiddenSectionsLevel) return Mechanic.Mystery | Mechanic.Sections;
        if (number == HiddenLinkedLevel) return Mechanic.Mystery | Mechanic.Linked;
        if (number == MultiSwapLevel) return Mechanic.Swap;
        int index = Array.IndexOf(Unlocks, number);
        return index >= 0 ? Mechanics[index] : Mechanic.None;
    }
    private static int Quantity(int number, Mechanic mechanic, int wanted, int small)
    {
        if (wanted == 0) return 0;
        var plan = Plan(number);
        if (IsLinkedFocusLevel(number))
        {
            if (mechanic == Mechanic.Linked) return number < 140 ? 4 : number < 220 ? 5 : 6;
            if (mechanic == Mechanic.Ice) wanted = Mathf.Min(wanted, 2);
            if (mechanic == Mechanic.Swap) wanted = Mathf.Min(wanted, 4);
        }
        if (mechanic == Mechanic.Sections && (plan & Mechanic.Loop) != 0) wanted = Mathf.Min(wanted, 2);
        if (mechanic == Mechanic.Linked && (plan & Mechanic.Sections) != 0)
            wanted = Mathf.Min(wanted, 2); // Leave belt room for the multi-colour sections.
        if (number >= 200 && number < 220 && CountMechanics(plan) >= 3)
            wanted = Mathf.Min(wanted, small * (1 + (number - 200) / 8));
        if (number >= 220)
        {
            bool four = CountMechanics(plan) >= 4;
            int cap = mechanic == Mechanic.Mystery ? (four ? 7 : 8)
                : mechanic == Mechanic.Linked ? (four ? 2 : 3)
                : mechanic == Mechanic.Ice ? (four ? 3 : 4)
                : mechanic == Mechanic.Keys ? (four ? 2 : 3)
                : mechanic == Mechanic.Sections ? (four ? 4 : 5)
                : mechanic == Mechanic.Large ? (four ? 2 : 3) : (four ? 4 : 6);
            return Mathf.Min(wanted, cap);
        }
        int lesson = RecentLesson(number), age = number - lesson;
        var introduced = LessonMechanics(lesson);
        if (age <= 7 && (plan & introduced) == introduced && introduced != Mechanic.None)
        {
            // Advanced lessons introduce a combination of already familiar cup types.
            bool combinationLesson = lesson == HiddenSectionsLevel || lesson == HiddenLinkedLevel;
            if (combinationLesson || (introduced & mechanic) == 0)
                return Mathf.Min(wanted, small * (age <= 4 ? 1 : 2));
        }
        return wanted;
    }

    public static int MechanicCountFor(int number) => CountMechanics(Plan(number));

    public static bool UsesGuidedBucketOrder(int number)
    {
        var plan = Plan(number);
        if (IsFullMysteryLevel(number) || IsLinkedFocusLevel(number) || number >= 70 || plan == Mechanic.None || number - RecentLesson(number) < 12) return true;
        for (int i = 0; i < Mechanics.Length; i++)
            if ((plan & Mechanics[i]) != 0 && number - Unlocks[i] < 12) return true;
        return false;
    }

    public static int MysteryBucketCount(int number) => IsFullMysteryLevel(number) ? LevelData.BucketLayoutCellCount
        : Quantity(number, Mechanic.Mystery, number < MysteryLevel ? 0 : Mathf.Min(2 + (number - MysteryLevel) / 3, 9), 2);
    public static int FrozenBucketCount(int number) => Quantity(number, Mechanic.Ice,
        number < IceLevel ? 0 : Mathf.Min(1 + (number - IceLevel) / 4, 5), 1);
    public static int LockBucketCount(int number) => Quantity(number, Mechanic.Keys,
        number < KeysLevel ? 0 : Mathf.Min(1 + (number - KeysLevel) / 5, 4), 1);
    // Crates retain their original full-board alternating-column behavior.
    public static int CrateCellCount(int number) => number < CratesLevel ? 0 : LevelData.BucketLayoutCellCount;
    public static int SectionBucketCount(int number) => Quantity(number, Mechanic.Sections,
        number < SectionsLevel ? 0 : Mathf.Min(1 + (number - SectionsLevel) / 4, 5), 1);
    public static int LinkedPairCount(int number) => Quantity(number, Mechanic.Linked,
        number < LinkedLevel ? 0 : Mathf.Min(1 + (number - LinkedLevel) / 6, 3), 1);
    public static int LargeBucketCount(int number) => Quantity(number, Mechanic.Large,
        number < LargeLevel ? 0 : Mathf.Min(1 + (number - LargeLevel) / 6, 3), 1);
    public static bool AllBucketsSwapColors(int number) => false;
    public static int SwapBucketCount(int number, int availableBuckets)
    {
        if (number < SwapLevel) return 0;
        int wanted = number < MultiSwapLevel ? 2
            : Mathf.Min(4 + 2 * ((number - MultiSwapLevel) / 6), 8);
        return Mathf.Min(Quantity(number, Mechanic.Swap, wanted, 2), availableBuckets / 2);
    }
    public static int FrostMovesFor(int number)
        => number < IceLevel ? 0 : Mathf.Min(IntroFrostMoves + (number - IceLevel) / 60, 6);

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
        var plan = Plan(number);
        level.beltCapacity = ConveyorBelt.MaxOnBelt;
        level.beltSpeedMultiplier = 1f;
        level.mysteryBuckets = (plan & Mechanic.Mystery) != 0;
        level.linkedBuckets = (plan & Mechanic.Linked) != 0;
        level.frostMoves = (plan & Mechanic.Ice) != 0 ? FrostMovesFor(number) : 0;
        level.keyGates = (plan & Mechanic.Keys) != 0;
        level.sectionBuckets = (plan & Mechanic.Sections) != 0;
        level.largeBuckets = (plan & Mechanic.Large) != 0;
        level.swappingColors = (plan & Mechanic.Swap) != 0;
        level.loopInventory = (plan & Mechanic.Loop) != 0;
        level.alternatingCrates = (plan & Mechanic.Crates) != 0;
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
        if (UsesGuidedBucketOrder(index + 1)) rules.matchBucketOrderToSand = true;
        // Two-slot cups need smaller portions when several mechanics share the five-slot belt.
        var plan = Plan(index + 1);
        if (index + 1 >= 70 && CountMechanics(plan) >= 2 && (plan & (Mechanic.Linked | Mechanic.Large)) != 0
            && (rules.maxGrainsPerBucket <= 0 || rules.maxGrainsPerBucket > 100))
            rules.maxGrainsPerBucket = 100;
        rules.startLevelIndex = index; rules.levelCount = 1;
        rules.segmentId = "campaign_" + (index + 1).ToString("000");
        rules.fillMode = BucketLayoutFillMode.UseTemplateAsIs;
        rules.randomComponents = Array.Empty<BucketLayoutComponent>();
        rules.layoutTemplate = (byte[])level.bucketLayout.Clone();
        rules.conveyorDirectionTemplate = (byte[])level.conveyorDirections.Clone();
        rules.conveyorBucketCountTemplate = (byte[])level.conveyorBucketCounts.Clone();
    }
}
