#requires -Version 7.0
param([switch]$Player,[switch]$DevelopmentBuild)
$ErrorActionPreference = 'Stop'
if($DevelopmentBuild){$Player=$true}
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sources = @(
    'Assets/Scripts/Data/PlayerProgress.cs'
    'Assets/Scripts/Gameplay/ProgressSaveService.cs'
    'Assets/Scripts/Gameplay/WinterProgress.cs'
    'Assets/Scripts/Gameplay/LevelCheats.cs'
    'Assets/Scripts/Data/GameDebugSettings.cs'
) | ForEach-Object {
    [regex]::Replace((Get-Content (Join-Path $root $_) -Raw), '(?m)^using [^\r\n]+;\r?\n', '')
}
$stubs = @"
namespace UnityEngine {
    public class ScriptableObject {}
    public class CreateAssetMenuAttribute : System.Attribute { public string fileName,menuName; }
    public class InspectorNameAttribute : System.Attribute { public InspectorNameAttribute(string value){} }
    public class TooltipAttribute : System.Attribute { public TooltipAttribute(string value){} }
    public static class Resources { public static object TestAsset; public static T Load<T>(string path) where T:class => TestAsset as T; }

    public static class Mathf {
        public static int Max(int a,int b) => System.Math.Max(a,b);
        public static int Min(int a,int b) => System.Math.Min(a,b);
        public static int Clamp(int n,int min,int max) => System.Math.Min(max,System.Math.Max(min,n));
    }
    public static class PlayerPrefs {
        public static bool HasKey(string key) => throw new System.InvalidOperationException("Real save access");
        public static int GetInt(string key,int fallback=0) => throw new System.InvalidOperationException("Real save access");
        public static void SetInt(string key,int value) => throw new System.InvalidOperationException("Real save access");
        public static void Save() => throw new System.InvalidOperationException("Real save access");
    }
}
public static class GameProgressStore {
    private static readonly System.Collections.Generic.Dictionary<string,int> data=new System.Collections.Generic.Dictionary<string,int>();
    public static bool IsPreview => true;
    public static bool HasKey(string key) => data.ContainsKey(key);
    public static int GetInt(string key,int fallback=0) => data.TryGetValue(key,out int value)?value:fallback;
    public static bool GetBool(string key,bool fallback=false) => GetInt(key,fallback?1:0)!=0;
    public static void SetInt(string key,int value) => data[key]=value;
    public static void SetBool(string key,bool value) => SetInt(key,value?1:0);
    public static void Save() { }
    public static void Clear() => data.Clear();
}
"@
$symbols=if($DevelopmentBuild){'/define:DEVELOPMENT_BUILD'}elseif($Player){'/define:PLAYER_TEST'}else{'/define:UNITY_EDITOR'}
Add-Type ("using System;`nusing UnityEngine;`n" + ($sources -join "`n") + "`n" + $stubs) -CompilerOptions $symbols
function Require([bool]$condition,[string]$message) { if (-not $condition) { throw $message } }

$debugSettings=[GameDebugSettings]::new()
$debugSettings.showSkipLevelButton=$true
[UnityEngine.Resources]::TestAsset=$debugSettings
if($Player){
    [ProgressSaveService]::Initialize(1000)
    [WinterProgress]::Initialize(200)
    [GameProgressStore]::SetBool('sand_cheat_all_levels_unlocked',$true)
    Require (-not [LevelCheats]::AllLevelsUnlocked) 'Player honored an old cheat flag'
    Require (-not [LevelCheats]::CanSelectLevel($true,$false,$false,$true)) 'Player replay menu bypassed completion rules'
    Require (-not [GameDebugSettings]::ShowSkipLevelButton) 'Player enabled free debug skip from saved settings'
    foreach($type in @([LevelCheats],[ProgressSaveService],[WinterProgress])){
        Require ($null -eq $type.GetMethod('UnlockAllLevels')) "$type still exposes a player cheat API"
    }
    Require ([ProgressSaveService]::HighestUnlockedIndex -eq 0 -and [WinterProgress]::HighestUnlocked -eq 0) 'Player unexpectedly unlocked levels'
    [ProgressSaveService]::MarkLevelCompleted(0)
    [WinterProgress]::Complete(0)
    Require ([ProgressSaveService]::IsLevelUnlocked(1) -and [WinterProgress]::IsUnlocked(1)) 'Normal completion no longer unlocks levels'
    $mode=if($DevelopmentBuild){'development build'}else{'player'}
    Write-Output "PASS: $mode has no unlock cheat APIs, ignores the cheat flag, disables free debug skip, and preserves normal progression."
    exit 0
}
Require ([GameDebugSettings]::ShowSkipLevelButton) 'Editor free debug skip no longer follows its setting'
[ProgressSaveService]::Initialize(1000)
[WinterProgress]::Initialize(200)
Require (-not [LevelCheats]::CanSelectLevel($true,$false,$false,$true)) 'Normal replay menu must retain completed-only behavior'
Require ([LevelCheats]::CanSelectLevel($true,$true,$false,$true)) 'Completed level must remain selectable'
Require ([LevelCheats]::CanSelectLevel($true,$false,$true,$true)) 'First level must remain selectable'
Require ([LevelCheats]::CanSelectLevel($false,$false,$false,$true)) 'Normal gallery must allow unlocked levels'
[ProgressSaveService]::MarkLevelCompleted(2)
[ProgressSaveService]::SetCurrentLevel(1)
[WinterProgress]::Complete(0)
[WinterProgress]::SetCurrent(1)
[GameProgressStore]::SetInt('unrelated_gold',321)

[LevelCheats]::UnlockAllLevels()
Require ([LevelCheats]::AllLevelsUnlocked) 'Cheat flag was not saved'
Require ([ProgressSaveService]::HighestUnlockedIndex -eq 999) 'Main levels not all unlocked'
Require ([WinterProgress]::HighestUnlocked -eq 199) 'Winter levels not all unlocked'
Require ([ProgressSaveService]::HighestCompletedIndex -eq 2) 'Cheat changed completed main levels'
Require ([ProgressSaveService]::CurrentLevelIndex -eq 1 -and [WinterProgress]::Current -eq 1) 'Cheat changed current levels'
Require (-not [WinterProgress]::IsCompleted(199)) 'Cheat marked winter levels complete'
foreach ($index in 0..999) {
    Require ([ProgressSaveService]::IsLevelUnlocked($index)) "Main level $index stayed locked"
    Require ([LevelCheats]::CanSelectLevel($true,[ProgressSaveService]::IsLevelCompleted($index),($index -eq 0),[ProgressSaveService]::IsLevelUnlocked($index))) "Main replay card $index stayed locked"
}
foreach ($index in 0..199) {
    Require ([WinterProgress]::IsUnlocked($index)) "Winter level $index stayed locked"
    Require ([LevelCheats]::CanSelectLevel($true,[WinterProgress]::IsCompleted($index),($index -eq 0),[WinterProgress]::IsUnlocked($index))) "Winter replay card $index stayed locked"
}
[ProgressSaveService]::Initialize(1000)
[WinterProgress]::Initialize(200)
Require ([LevelCheats]::AllLevelsUnlocked -and [ProgressSaveService]::IsLevelUnlocked(999) -and [WinterProgress]::IsUnlocked(199)) 'Unlock did not survive reload'
[ProgressSaveService]::SetCurrentLevel(24)
[WinterProgress]::SetCurrent(199)
Require ([ProgressSaveService]::CurrentLevelIndex -eq 24 -and [WinterProgress]::Current -eq 199) 'Cannot select newly unlocked levels'
[LevelCheats]::UnlockAllLevels()
Require ([ProgressSaveService]::CurrentLevelIndex -eq 24 -and [ProgressSaveService]::HighestCompletedIndex -eq 2) 'Repeated cheat changed progress'
Require ([GameProgressStore]::GetInt('unrelated_gold') -eq 321) 'Cheat changed unrelated data'
Require (-not [LevelCheats]::CanSelectLevel($true,$false,$false,$false)) 'Cheat bypassed actual progression locks'

[GameProgressStore]::Clear()
[ProgressSaveService]::Initialize(3)
[WinterProgress]::Initialize(0)
[LevelCheats]::UnlockAllLevels()
Require ([ProgressSaveService]::HighestUnlockedIndex -eq 2) 'Small database was not respected'
Require (-not [WinterProgress]::IsUnlocked(0)) 'Empty winter campaign gained a level'
Write-Output 'PASS: Editor cheats and debug skip work; all 1000 main and 200 winter levels selectable; unlock persists; completions/current level/unrelated save data preserved; repeat activation and small/empty databases work.'