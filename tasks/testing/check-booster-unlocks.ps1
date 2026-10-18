#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

# Compile the real progression and booster code with in-memory persistence and
# minimal engine/analytics substitutes. This never touches the player's save.
$sources = @(
    'Assets/Scripts/Data/PlayerProgress.cs'
    'Assets/Scripts/Gameplay/ProgressSaveService.cs'
    'Assets/Scripts/Boosters/BoosterCatalog.cs'
    'Assets/Scripts/Boosters/BoosterService.cs'
) | ForEach-Object {
    [regex]::Replace((Get-Content (Join-Path $root $_) -Raw), '(?m)^using [^\r\n]+;\r?\n', '')
}
$stubs = @'
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public object gameObject;
        protected static void Destroy(object value) { }
    }
    public static class Mathf
    {
        public static int Max(int a, int b) => System.Math.Max(a, b);
        public static int Clamp(int value, int min, int max) => System.Math.Min(max, System.Math.Max(min, value));
    }
    public static class PlayerPrefs
    {
        public static bool HasKey(string key) => throw new System.InvalidOperationException("Unexpected player save access");
        public static int GetInt(string key, int fallback = 0) => throw new System.InvalidOperationException("Unexpected player save access");
        public static void SetInt(string key, int value) => throw new System.InvalidOperationException("Unexpected player save access");
        public static void Save() => throw new System.InvalidOperationException("Unexpected player save access");
    }
}
public static class GameProgressStore
{
    private static readonly System.Collections.Generic.Dictionary<string, int> values = new System.Collections.Generic.Dictionary<string, int>();
    public static bool IsPreview => true;
    public static bool HasKey(string key) => values.ContainsKey(key);
    public static int GetInt(string key, int fallback = 0) => values.TryGetValue(key, out int value) ? value : fallback;
    public static void SetInt(string key, int value) => values[key] = value;
    public static bool GetBool(string key, bool fallback = false) => GetInt(key, fallback ? 1 : 0) != 0;
    public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);
    public static void Save() { }
    public static void Clear() => values.Clear();
}
public static class Analytic
{
    public static void BonusUsed(string level, string booster) { }
}
'@
Add-Type ("using System;`nusing UnityEngine;`n" + ($sources -join "`n") + "`n" + $stubs)

$failures = [System.Collections.Generic.List[string]]::new()
function Require([bool] $condition, [string] $message) {
    if (-not $condition) { $failures.Add($message) }
}

foreach ($type in [Enum]::GetValues([BoosterType])) {
    [GameProgressStore]::Clear()
    [ProgressSaveService]::Initialize(300)
    $service = [BoosterService]::new()
    $unlock = [BoosterCatalog]::GetUnlockLevelIndex($type)

    [ProgressSaveService]::MarkLevelCompleted($unlock - 2)
    [ProgressSaveService]::SetCurrentLevel(0)
    $service.SyncUnlocks(0)
    Require (-not $service.IsUnlocked($type, 0)) "$type must stay locked before its required level opens"
    Require ($service.GetCharges($type) -eq 0) "$type must not receive charges before unlocking"

    # Open the required level, but replay level 1 before ever playing that level.
    [ProgressSaveService]::MarkLevelCompleted($unlock - 1)
    [ProgressSaveService]::SetCurrentLevel(0)
    [ProgressSaveService]::Load()
    Require ([ProgressSaveService]::HighestUnlockedIndex -eq $unlock) "$type fixture must open exactly the required level"
    Require (-not [ProgressSaveService]::IsLevelCompleted($unlock)) "$type fixture must not complete the required level"
    Require ($service.IsUnlocked($type, 0)) "$type must be available on level 1 once its required level opens"
    $service.SyncUnlocks(0)
    Require ($service.GetCharges($type) -eq [BoosterCatalog]::StartCharges) "$type must receive initial charges while replaying level 1"
    Require ([GameProgressStore]::GetBool([BoosterCatalog]::GetGrantedKey($type))) "$type must record its initial grant"

    # Spend a charge, switch levels, then reload progression and the service.
    Require ($service.TryConsume($type)) "$type must be usable on an earlier level"
    [ProgressSaveService]::SetCurrentLevel($unlock)
    $service.SyncUnlocks($unlock)
    [ProgressSaveService]::SetCurrentLevel(0)
    [ProgressSaveService]::Load()
    $service = [BoosterService]::new()
    $service.SyncUnlocks(0)
    Require ($service.IsUnlocked($type, 0)) "$type must remain unlocked after switching levels and reloading"
    Require ($service.GetCharges($type) -eq ([BoosterCatalog]::StartCharges - 1)) "$type must not receive duplicate initial charges"
    Require ($service.TryConsume($type)) "$type must allow spending its remaining charge"
    $service.SyncUnlocks(0)
    Require ($service.GetCharges($type) -eq 0) "$type must not refill spent charges on replay"
    Require ($service.IsUnlocked($type, 0)) "$type must stay unlocked with zero charges"
    Require (-not $service.TryConsume($type)) "$type must not be usable without charges"
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Output "FAIL: $_" }
    exit 1
}
Write-Output 'PASS: all three boosters unlock at the required progress on earlier levels; grants happen once, survive reloads, and spent charges stay spent.'
