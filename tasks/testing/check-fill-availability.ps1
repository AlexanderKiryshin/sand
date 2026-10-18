#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$hudSource = Get-Content (Join-Path $projectRoot 'Assets/Scripts/UI/SandEconomyHud.cs') -Raw
function Extract-Method([string] $name) {
    $match = [regex]::Match($hudSource, '(?m)^    private (?:void|bool) ' + $name + '\([^\r\n]*\)\r?\n    \{')
    if (-not $match.Success) { return '' }
    $start = $match.Index
    $position = $hudSource.IndexOf('{', $start)
    $depth = 1
    while ($depth -gt 0) {
        $position++
        if ($hudSource[$position] -eq '{') { $depth++ }
        if ($hudSource[$position] -eq '}') { $depth-- }
    }
    return $hudSource.Substring($start, $position - $start + 1).Replace('private ', 'public ')
}
$methods = @('WireEvents', 'RefreshBoosters', 'HandleBoosterButton', 'HasFillTarget', 'HandleBoosterConveyorChanged') | ForEach-Object { Extract-Method $_ }
$stubSource = @'
using System;
using System.Collections.Generic;
public enum BoosterType { Magnet, Transparent, Shuffle }
public enum BucketState { InInventory, OnConveyor, Full, Removed }
public class Bucket { public BucketState State = BucketState.OnConveyor; public bool IsFull; }
public class ConveyorBelt {
    public event Action OnConveyorChanged;
    public void NotifyChanged() { OnConveyorChanged?.Invoke(); }
    public class BeltEntry { public Bucket Bucket; public bool PendingRemoval; }
    public List<BeltEntry> Entries = new List<BeltEntry>();
}
public class Transform { public Transform Find(string name) => null; public Image GetComponent<T>() => null; public GameObject gameObject = new GameObject(); }
public class GameObject { public void SetActive(bool value) {} }
public struct Color { public float a; public Color(float r,float g,float b,float alpha=1) { a=alpha; } public static Color white => new Color(); }
public class Sprite {}
public class Image { public enum Type { Simple } public Color color; public Sprite sprite; public bool preserveAspect; public Type type; }
public class Button { public bool interactable; public Transform transform = new Transform(); public Image GetComponent<T>() => null; }
public class TextMeshProUGUI { public string text; public GameObject gameObject = new GameObject(); }
public class LocalizationManager { public static LocalizationManager Instance; public Action onLanguageChange; public string GetText(string key, params object[] args) => key; }
public class CoinManager { public static CoinManager instance; public Action<int> OnCoinsChanged; public int Coins; }
public static class BoosterCatalog { public static string GetLocalizationKey(BoosterType type) => type.ToString(); public static int GetUnlockLevelIndex(BoosterType type) => 0; }
public static class GameUiSymbols { public static Sprite Booster(BoosterType type) => null; }
public static class CampaignText { public static string Get(string key) => key; }
public class GameManager { public int CurrentLevelIndex; public object Inventory; }
public class BoosterService {
    public Action OnChargesChanged, OnBoosterModeCancelled;
    public Action<BoosterType> OnBoosterActivated;
    public static BoosterService instance = new BoosterService();
    public BoosterType? ActiveTargetMode; public int Charges = 2; public bool Unlocked = true;
    public bool IsUnlocked(BoosterType type, int level) => Unlocked;
    public int GetCharges(BoosterType type) => Charges;
    public void CancelTargetMode() { ActiveTargetMode = null; OnBoosterModeCancelled?.Invoke(); }
    public bool TryConsume(BoosterType type) { Charges--; return true; }
    public void BeginTargetMode(BoosterType type) { ActiveTargetMode = type; }
}
public static class BoosterMechanics { public static bool TryShuffleInventoryColors(object inventory) => true; }
public class HudCheck {
    public GameManager gameManager = new GameManager();
    public ConveyorBelt boosterConveyor;
    public static ConveyorBelt Conveyor;
    public static T FindObjectOfType<T>() where T : class => Conveyor as T;
    public Button[] boosterButtons = { new Button(), new Button(), new Button() };
    public TextMeshProUGUI[] boosterTitles = new TextMeshProUGUI[3], boosterCharges = new TextMeshProUGUI[3], boosterUnlockLabels = new TextMeshProUGUI[3];
    public bool tutorialGameplayFrozen, tutorialWaitingFirstBucket, tutorialWaitingTarget, tutorialWaitingActivation;
    public BoosterType? tutorialType; public bool BuyOpened;
    public void HideHand() {} public void UpdateTutorialHandForTarget() {} public void CompleteBoosterUse(BoosterType type) {}
    public void HandleCoinsChanged(int coins) {} public void HandleBoosterActivated(BoosterType type) {}
    public void HandleBoosterModeCancelled() { RefreshBoosters(); } public void RefreshLocalized() {}
    public void OpenBuyPopup(BoosterType type) { BuyOpened = true; }
'@
Add-Type ($stubSource + ($methods -join "`n") + "`n}")
$hud = [HudCheck]::new()
$conveyor = [ConveyorBelt]::new()
[HudCheck]::Conveyor = $conveyor
$hud.WireEvents()
function Require([bool] $condition, [string] $message) { if (-not $condition) { throw $message } }
$hud.RefreshBoosters()
Require (-not $hud.boosterButtons[1].interactable) 'Fill must be disabled with an empty conveyor'
Require ($hud.boosterButtons[0].interactable -and $hud.boosterButtons[2].interactable) 'Other boosters must remain available'
$hud.HandleBoosterButton([BoosterType]::Transparent)
Require ($null -eq [BoosterService]::instance.ActiveTargetMode) 'Direct activation must reject an empty conveyor'
[BoosterService]::instance.Charges = 0
$hud.HandleBoosterButton([BoosterType]::Transparent)
Require (-not $hud.BuyOpened) 'Empty conveyor must not open the Fill purchase popup'
[BoosterService]::instance.Charges = 2
$entry = [ConveyorBelt+BeltEntry]::new()
$entry.Bucket = [Bucket]::new()
$conveyor.Entries.Add($entry)
$conveyor.NotifyChanged()
Require $hud.boosterButtons[1].interactable 'Placing a bucket must enable Fill'
$hud.HandleBoosterButton([BoosterType]::Transparent)
Require ([BoosterService]::instance.ActiveTargetMode -eq [BoosterType]::Transparent) 'Fill must activate with a target'
$conveyor.Entries.Clear()
$conveyor.NotifyChanged()
Require (-not $hud.boosterButtons[1].interactable) 'Removing all buckets must disable Fill'
Require ($null -eq [BoosterService]::instance.ActiveTargetMode) 'Removing all targets must cancel Fill selection'
Require ([BoosterService]::instance.Charges -eq 2) 'Selection and availability must not consume charges'
$conveyor.Entries.Add($entry)
$conveyor.NotifyChanged()
Require $hud.boosterButtons[1].interactable 'A new bucket must re-enable Fill'
$entry.Bucket.IsFull = $true
$conveyor.NotifyChanged()
Require (-not $hud.boosterButtons[1].interactable) 'Full buckets must not enable Fill'
$entry.Bucket.IsFull = $false
$entry.Bucket.State = [BucketState]::InInventory
$conveyor.NotifyChanged()
Require (-not $hud.boosterButtons[1].interactable) 'Inventory buckets must not enable Fill'
Write-Output 'PASS: actual HUD methods reject empty conveyor, track target changes, cancel stale selection, preserve charges, and exclude full/inventory targets.'
