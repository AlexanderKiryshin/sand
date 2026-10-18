param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = [IO.File]::ReadAllText("$taskRoot/Assets/Scripts/UI/TutorialController.cs")
$taskMethods = foreach ($taskName in @('HandleLevelLoaded','HandleBucketPlaced','HandleBucketFilled','BeginFirstStep','CompleteTutorial')) {
    $taskMatch = [regex]::Match($taskSource, "    (public|private) void $taskName\([^)]*\)\s*\{")
    if (!$taskMatch.Success) { throw "Missing method $taskName" }
    $taskStart = $taskSource.IndexOf('{', $taskMatch.Index); $taskDepth = 1; $taskEnd = $taskStart + 1
    while ($taskDepth -gt 0) { if ($taskSource[$taskEnd] -eq '{') { $taskDepth++ }; if ($taskSource[$taskEnd] -eq '}') { $taskDepth-- }; $taskEnd++ }
    $taskSource.Substring($taskMatch.Index, $taskEnd - $taskMatch.Index)
}
$taskHarness = @'
using System;
using System.Collections.Generic;
class GameManager { public int CurrentLevelIndex, DisplayIndex; public bool Seasonal; public int CampaignLevelIndex=>Seasonal?DisplayIndex:CurrentLevelIndex; public bool IsSeasonal(int index)=>Seasonal; }
class Bucket {}
class ConveyorBelt { public List<object> Entries=new List<object>(); }
class BucketInventoryUI { public Bucket Target = new Bucket(); public Bucket GetFirstAccessibleBucket() => Target; public void Refresh() {} }
class LevelIntroSandAnimator { public bool IsPlaying; }
enum BoosterType {Magnet,Transparent,Shuffle}
class BoosterService {
 public static BoosterService instance=new BoosterService();public int Queries;public BoosterType Pending;
 public BoosterType? GetPendingTutorial(int level){Queries++;return Pending;}
}
class SandEconomyHud {
 private GameManager gameManager;private BoosterType? tutorialType;private bool tutorialWaitingFirstBucket,tutorialWaitingActivation,tutorialWaitingTarget,tutorialGameplayFrozen,handVisible;
 private ConveyorBelt tutorialConveyor;
 private void UnwireTutorialConveyor(){tutorialConveyor=null;}
 private void ClearTutorialGameplayFreeze(){tutorialGameplayFrozen=false;}
 private void HideHand(){handVisible=false;}
 private void WireTutorialConveyor(){}private void RefreshBoosters(){}private void BeginTransparentTutorialAfterFirstBucket(){}
 private void BeginBoosterActivationTutorial(BoosterType type){tutorialWaitingActivation=true;handVisible=true;}
BOOSTER_METHOD
 public static void CheckWinterTutorials(){
  var hud=new SandEconomyHud();hud.gameManager=new GameManager{Seasonal=true,CurrentLevelIndex=1000};
  foreach(var type in new[]{BoosterType.Magnet,BoosterType.Transparent,BoosterType.Shuffle}){
   BoosterService.instance.Pending=type;BoosterService.instance.Queries=0;hud.tutorialType=type;hud.tutorialGameplayFrozen=true;hud.handVisible=true;
   hud.TryStartBoosterTutorial(0);
   if(hud.tutorialType.HasValue || hud.tutorialGameplayFrozen || hud.handVisible || hud.tutorialWaitingFirstBucket || hud.tutorialWaitingActivation || hud.tutorialWaitingTarget || BoosterService.instance.Queries!=0)throw new Exception("Winter retained booster tutorial, freeze or consumed pending lesson");
  }
  hud.gameManager.Seasonal=false;BoosterService.instance.Pending=BoosterType.Magnet;hud.TryStartBoosterTutorial(0);
  if(!hud.tutorialWaitingActivation || !hud.handVisible)throw new Exception("Ordinary booster tutorial no longer starts");
  Console.WriteLine("WINTER_BOOSTER_TUTORIALS_DISABLED_PASS");
 }
}
class ColorFocusPanel {public byte RedColor=7;public event Action<byte> SelectionChanged;}

class LocalizationManager { public static LocalizationManager Instance; public Action onLanguageChange; }
static class GameProgressStore { public static bool Completed = true; public static bool GetBool(string key) => Completed; public static void SetBool(string key, bool value) { Completed = value; } public static void Save() {} }
static class CampaignProgression { public static bool IsLessonStart(int index) => index==24; public static string GetLesson(int index) => "lesson"; }
static class CampaignText { public static string Get(string key) => key; }
static class Analytic { public static void Track(string name) {} }
class GameUI { public Action OnHintDismissed; public string Text; public int Step; public bool Visible,Controls; public void SetControlsInteractable(bool value) {Controls=value;} public void SetHintVisible(bool value) { Visible = value; } public void SetTutorialHint(string text, int step = 0) { Text = text; Step = step; Visible = true; } }
class TutorialController {
    private GameManager gameManager; private GameUI gameUI; private BucketInventoryUI bucketInventoryUI;
    private SandEconomyHud economyHud; private LocalizationManager localization; private LevelIntroSandAnimator introAnimator;
    private bool tutorialOnFirstLevelOnly = true, tutorialEnabled, completed, stepActive, levelHintActive;
    private float animationTime, watchTime; private Bucket targetBucket; private int phase;
    private ColorFocusPanel filterPanel;public int FilterTutorialStep;private bool handVisible;
    private void HandleFilterSelected(byte selected){}private void ShowFilterHint(){gameUI.SetTutorialHint("tutorial_filter_pick",4);}
    private const string CompletedKey = "sand_tutorial_v2_complete";
    private T FindObjectOfType<T>() where T:class => typeof(T)==typeof(ColorFocusPanel)?new ColorFocusPanel() as T:null;
    private void HandleHintDismissed() {} private void RefreshLanguage() {} private void ResolveCanvas() {} private void EnsureOverlay() {} private void ResolveIntroAnimator() {} private void DisableTutorialVisuals() {handVisible=false;} private void SetTutorialVisualsVisible(bool visible) {}
METHODS
    public static void Main() {
        var tutorial = new TutorialController(); var manager = new GameManager(); var ui = new GameUI(); var stock = new BucketInventoryUI();
        tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(ui.Visible && ui.Step == 1 && ui.Text == "tutorial_pick", "Completed save must not suppress first-level tutorial on replay");
        tutorial.HandleBucketPlaced(stock.Target);
        Require(ui.Visible && ui.Step == 2 && ui.Text == "tutorial_watch", "Step 2 must request its instruction text");
        tutorial.HandleBucketFilled(stock.Target);
        Require(ui.Visible && ui.Step == 3 && ui.Text == "tutorial_repeat", "Step 3 must request its instruction text");
        tutorial.HandleBucketPlaced(stock.Target);
        tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(ui.Step == 1 && ui.Text == "tutorial_pick", "Reopening level must reset tutorial to step 1");
        manager.CurrentLevelIndex = 1; tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(!tutorial.tutorialEnabled, "First-level tutorial must not leak into later levels");
        Require(tutorial.FilterTutorialStep==1 && ui.Visible,"Ordinary palette tutorial missing");
        manager.Seasonal=true;
        foreach(int display in new[]{0,1,24,199}){
            manager.CurrentLevelIndex=1000+display;manager.DisplayIndex=display;ui.Visible=true;tutorial.handVisible=true;
            tutorial.HandleLevelLoaded(manager,ui,stock,null);
            Require(!tutorial.tutorialEnabled && !tutorial.stepActive && !tutorial.levelHintActive && tutorial.FilterTutorialStep==0 && !tutorial.handVisible && !ui.Visible && ui.Controls,"Winter must suppress all tutorials and leave controls available");
        }
        manager.Seasonal=false;manager.CurrentLevelIndex=0;tutorial.HandleLevelLoaded(manager,ui,stock,null);
        Require(tutorial.tutorialEnabled && ui.Step==1 && ui.Text=="tutorial_pick","Ordinary tutorial did not resume after winter");
        manager.CurrentLevelIndex=24;tutorial.HandleLevelLoaded(manager,ui,stock,null);
        Require(ui.Visible && ui.Text=="lesson","Ordinary mechanic hints were removed");
        SandEconomyHud.CheckWinterTutorials();
        Console.WriteLine("TUTORIAL_REPLAY_AND_THREE_STEPS_PASS");
        Console.WriteLine("WINTER_FIRST_PALETTE_AND_MECHANIC_TUTORIALS_DISABLED_PASS");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
'@
$boosterSource=[IO.File]::ReadAllText("$taskRoot/Assets/Scripts/UI/SandEconomyHud.cs")
$boosterMatch=[regex]::Match($boosterSource,'    private void TryStartBoosterTutorial\([^)]*\)\s*\{')
if(-not $boosterMatch.Success){throw 'Booster tutorial method missing'}
$boosterStart=$boosterSource.IndexOf('{',$boosterMatch.Index);$boosterDepth=1;$boosterEnd=$boosterStart+1
while($boosterDepth -gt 0){if($boosterSource[$boosterEnd] -eq '{'){$boosterDepth++};if($boosterSource[$boosterEnd] -eq '}'){$boosterDepth--};$boosterEnd++}
$boosterMethod=$boosterSource.Substring($boosterMatch.Index,$boosterEnd-$boosterMatch.Index)
$taskHarness=$taskHarness.Replace('BOOSTER_METHOD',$boosterMethod)
$taskOutput = "$taskRoot/Temp/TutorialFlowChecks"
New-Item -ItemType Directory -Force $taskOutput | Out-Null
[IO.File]::WriteAllText("$taskOutput/check.cs", $taskHarness.Replace('METHODS', ($taskMethods -join "`n")))
$taskReferences = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" -nologo -target:exe -nostdlib "-out:$taskOutput/check.exe" "-r:$taskReferences/mscorlib.dll" "$taskOutput/check.cs"
if ($LASTEXITCODE -ne 0) { throw 'Tutorial harness compilation failed' }
& "$UnityData/MonoBleedingEdge/bin/mono.exe" "$taskOutput/check.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tutorial flow check failed' }
