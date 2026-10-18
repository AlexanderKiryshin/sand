param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = [IO.File]::ReadAllText("$taskRoot/Assets/Scripts/UI/GameUI.cs")
function Get-CheckMethod([string]$taskName) {
    $taskMatch = [regex]::Match($taskSource, "    private void $taskName\(\)\s*\{")
    if (!$taskMatch.Success) { return '' }
    $taskStart = $taskSource.IndexOf('{', $taskMatch.Index)
    $taskDepth = 1; $taskEnd = $taskStart + 1
    while ($taskDepth -gt 0) {
        if ($taskSource[$taskEnd] -eq '{') { $taskDepth++ }
        if ($taskSource[$taskEnd] -eq '}') { $taskDepth-- }
        $taskEnd++
    }
    return $taskSource.Substring($taskMatch.Index, $taskEnd - $taskMatch.Index)
}
$taskMethods = @('RefreshLanguage', 'RefreshLevelNumber', 'LateUpdate', 'HandleResetClicked') | ForEach-Object { Get-CheckMethod $_ }
$taskHarness = @'
using System;
using System.Reflection;
class GameManager { public int CurrentLevelIndex; public int LevelCount = 300; public int Restarts; public void RestartLevel() { Restarts++; } }
class Button { public bool interactable = true; }
class AdManager {
    public static AdManager instance;
    public int Requests;
    public bool Unavailable;
    public Action Closed;
    public void ShowInterstitial(Action closed) { Requests++; Closed = closed; if (Unavailable) closed(); }
}
class Label { public string text; }
static class CampaignText { public static string Get(string key) { return "Уровень"; } }
class GameUI {
    private GameManager gameManager = new GameManager();
    private Label levelLabel = new Label();
    private int displayedLevelIndex = -1;
    private bool restartAdPending;
    private Button resetButton = new Button();
    private void RefreshConveyor() {}
    // Actual production methods are inserted below.
METHODS
    public static void Main() {
        var ui = new GameUI(); ui.RefreshLanguage();
        ui.gameManager.CurrentLevelIndex = 1;
        typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ui, null);
        if (ui.levelLabel.text != "Уровень 2") throw new Exception("Stale level after transition: " + ui.levelLabel.text);
        ui.gameManager.CurrentLevelIndex = 29;
        typeof(GameUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(ui, null);
        ui.RefreshLanguage();
        if (ui.levelLabel.text != "Уровень 30") throw new Exception("Level text includes total count or stale index: " + ui.levelLabel.text);
        Console.WriteLine("LEVEL_NUMBER_TRANSITIONS_PASS");
        AdManager.instance = new AdManager();
        ui.HandleResetClicked(); ui.HandleResetClicked();
        if (AdManager.instance.Requests != 1 || ui.gameManager.Restarts != 0 || ui.resetButton.interactable)
            throw new Exception("Restart must wait for one interstitial and ignore repeated clicks");
        AdManager.instance.Closed(); AdManager.instance.Closed();
        if (ui.gameManager.Restarts != 1 || !ui.resetButton.interactable)
            throw new Exception("Ad close must restart once and unlock the button");
        AdManager.instance = new AdManager { Unavailable = true };
        ui.HandleResetClicked();
        if (ui.gameManager.Restarts != 2 || !ui.resetButton.interactable)
            throw new Exception("Unavailable ad must restart immediately");
        AdManager.instance = null; ui.HandleResetClicked();
        if (ui.gameManager.Restarts != 3) throw new Exception("Missing ad manager must restart immediately");
        Console.WriteLine("RESTART_INTERSTITIAL_FLOW_PASS");
    }
}
'@
$taskOutput = "$taskRoot/Temp/LevelNumberChecks"
New-Item -ItemType Directory -Force $taskOutput | Out-Null
[IO.File]::WriteAllText("$taskOutput/check.cs", $taskHarness.Replace('METHODS', ($taskMethods -join "`n")))
$taskReferences = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" -nologo -target:exe -nostdlib "-out:$taskOutput/check.exe" "-r:$taskReferences/mscorlib.dll" "$taskOutput/check.cs"
if ($LASTEXITCODE -ne 0) { throw 'Level number harness compilation failed' }
& "$UnityData/MonoBleedingEdge/bin/mono.exe" "$taskOutput/check.exe"
if ($LASTEXITCODE -ne 0) { throw 'Level number transition check failed' }
