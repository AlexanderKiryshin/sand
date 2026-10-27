param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = [IO.File]::ReadAllText("$taskRoot/Assets/Scripts/UI/TutorialController.cs")
$taskMethods = foreach ($taskName in @('HandleLevelLoaded','HandleBucketPlaced','HandleBucketFilled','FinishWatchStep','BeginFirstStep','CompleteTutorial','HandleHintDismissed','ShowFilterHint','HandleFilterSelected')) {
    $taskMatch = [regex]::Match($taskSource, "    (public|private) void $taskName\([^)]*\)\s*\{")
    if (!$taskMatch.Success) { throw "Missing method $taskName" }
    $taskStart = $taskSource.IndexOf('{', $taskMatch.Index); $taskDepth = 1; $taskEnd = $taskStart + 1
    while ($taskDepth -gt 0) { if ($taskSource[$taskEnd] -eq '{') { $taskDepth++ }; if ($taskSource[$taskEnd] -eq '}') { $taskDepth-- }; $taskEnd++ }
    $taskSource.Substring($taskMatch.Index, $taskEnd - $taskMatch.Index)
}
$taskHarness = @'
using System;
using System.Collections.Generic;
using System.Linq;
class GameManager { public int CurrentLevelIndex, DisplayIndex; public object Inventory=new object(); public bool Seasonal; public int CampaignLevelIndex=>Seasonal?DisplayIndex:CurrentLevelIndex; public bool IsSeasonal(int index)=>Seasonal; }
class Bucket {}
class ConveyorBelt { public List<object> Entries=new List<object>(); }
class BucketInventoryUI { public Bucket Target = new Bucket(); public Bucket GetFirstAccessibleBucket() => Target; public void Refresh() {} public void RefreshInteractionState() {} }
class LevelIntroSandAnimator { public bool IsPlaying; }
enum BoosterType {Magnet,Transparent,Shuffle}
class FallingSandSimulator {}
static class BoosterCatalog {public static string GetTutorialHintKey(BoosterType type)=>"hint";}
static class BoosterMechanics {
 public static bool Success=true;
 public static bool TryForcePlaceBucket(GameManager manager,Bucket bucket)=>Success;
 public static bool TryFillConveyorBucket(Bucket bucket,FallingSandSimulator simulator,ConveyorBelt belt)=>Success;
 public static bool TryShuffleInventoryColors(object inventory)=>Success;
}
class BoosterService {
 public static BoosterService instance=new BoosterService(); public int Queries;public BoosterType Pending;public BoosterType? ActiveTargetMode;
 public BoosterType? GetPendingTutorial(int level){Queries++;return Pending;}
 public bool IsUnlocked(BoosterType type,int level)=>true;public int GetCharges(BoosterType type)=>2;
 public bool TryConsume(BoosterType type)=>true;public void MarkTutorialSeen(BoosterType type){}
 public void ClearTargetMode(){ActiveTargetMode=null;}public void CancelTargetMode(){ActiveTargetMode=null;}
 public void BeginTargetMode(BoosterType type){ActiveTargetMode=type;}
}
class SandEconomyHud {
 private GameManager gameManager;private BoosterType? tutorialType;private bool tutorialWaitingFirstBucket,tutorialWaitingActivation,tutorialWaitingTarget,tutorialGameplayFrozen,handVisible;
 private ConveyorBelt tutorialConveyor;
 private void UnwireTutorialConveyor(){tutorialConveyor=null;}
 private void ClearTutorialGameplayFreeze(){tutorialGameplayFrozen=false;}
 private void FreezeTutorialGameplay(){tutorialGameplayFrozen=true;}
 private void HideHand(){handVisible=false;}
 private void WireTutorialConveyor(){tutorialConveyor=new ConveyorBelt();}private void RefreshBoosters(){}
 private void UpdateTutorialHandForTarget(){handVisible=true;}private void PointHandAtBooster(BoosterType type){handVisible=true;}
 private bool HasFillTarget()=>true;private void OpenBuyPopup(BoosterType type){}
 private object PointHandAtBoosterNextFrame(BoosterType type)=>new object();private void StartCoroutine(object value){handVisible=true;}
 private T FindObjectOfType<T>() where T:class=>null;
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
 public static void CheckBoosterAnalytics(){
  var manager=new GameManager{CurrentLevelIndex=5};var hud=new SandEconomyHud{gameManager=manager};
  Analytic.Begin(manager);BoosterService.instance.Pending=BoosterType.Magnet;hud.TryStartBoosterTutorial(5);
  hud.HandleBoosterButton(BoosterType.Magnet);
  BoosterMechanics.Success=false;hud.TryHandleBucketClick(new Bucket());
  if(Analytic.Count("tutorial_booster_magnet_target_completed")!=0)throw new Exception("Failed magnet target completed tutorial");
  BoosterService.instance.CancelTargetMode();hud.HandleBoosterModeCancelled();hud.HandleBoosterButton(BoosterType.Magnet);
  BoosterMechanics.Success=true;hud.TryHandleBucketClick(new Bucket());hud.TryHandleBucketClick(new Bucket());
  if(Analytic.Count("tutorial_booster_magnet_activate_completed")!=1 || Analytic.Count("tutorial_booster_magnet_target_completed")!=1)throw new Exception("Magnet retry duplicated or missed tutorial steps");
  manager.CurrentLevelIndex=7;Analytic.Begin(manager);hud=new SandEconomyHud{gameManager=manager};BoosterService.instance.Pending=BoosterType.Transparent;
  hud.TryStartBoosterTutorial(7);hud.HandleBoosterButton(BoosterType.Transparent);
  if(Analytic.Count("tutorial_booster_fill_activate_completed")!=0)throw new Exception("Fill activated before placing a belt cup");
  hud.tutorialConveyor.Entries.Add(new object());hud.HandleTutorialConveyorChanged();hud.HandleTutorialConveyorChanged();
  hud.HandleBoosterButton(BoosterType.Transparent);hud.TryHandleConveyorBucketClick(new Bucket());hud.TryHandleConveyorBucketClick(new Bucket());
  foreach(string step in new[]{"place_bucket","activate","target"})if(Analytic.Count("tutorial_booster_fill_"+step+"_completed")!=1)throw new Exception("Fill step missing/duplicated: "+step);
  manager.CurrentLevelIndex=9;Analytic.Begin(manager);hud=new SandEconomyHud{gameManager=manager};BoosterService.instance.Pending=BoosterType.Shuffle;
  hud.TryStartBoosterTutorial(9);BoosterMechanics.Success=false;hud.HandleBoosterButton(BoosterType.Shuffle);
  if(Analytic.Count("tutorial_booster_shuffle_activate_completed")!=0)throw new Exception("Failed shuffle completed tutorial");
  BoosterMechanics.Success=true;hud.HandleBoosterButton(BoosterType.Shuffle);hud.FinishTutorial();
  if(Analytic.Count("tutorial_booster_shuffle_activate_completed")!=1)throw new Exception("Shuffle step missing/duplicated");
  Console.WriteLine("BOOSTER_TUTORIAL_REAL_ACTIONS_RETRIES_TARGET_FAILURES_PASS");
 }
}
class ColorFocusPanel {public byte RedColor=7;public event Action<byte> SelectionChanged;}

class LocalizationManager { public static LocalizationManager Instance; public Action onLanguageChange; public string GetText(string key)=>key; }
static class GameProgressStore { public static bool Completed = true; public static bool GetBool(string key) => Completed; public static void SetBool(string key, bool value) { Completed = value; } public static void Save() {} }
static class CampaignProgression { public static bool IsLessonStart(int index) => index==24; public static string GetLesson(int index) => "lesson"; public static string GetLessonId(int index) => "lesson_test"; }
static class CampaignText { public static string Get(string key) => key; }
static class Analytic {
 public static readonly List<Tuple<string,Dictionary<string,object>>> Events=new List<Tuple<string,Dictionary<string,object>>>();
 private static readonly LevelAnalyticsSession session=new LevelAnalyticsSession((n,d)=>Events.Add(Tuple.Create(n,d)),()=>0);
 public static void Begin(GameManager manager){session.Begin(manager.CurrentLevelIndex,"Test","", "",false);}
 public static void Track(string name){session.Track(name);}
 public static void TutorialStep(string id,int step,string stepId,string status,string reason=""){session.TutorialStep(id,step,stepId,status,reason);}
 public static int Count(string name){return Events.Count(e=>e.Item1==name);}
}
class GameUI { public void SetFillTargetHintVisible(bool value){} public Action OnHintDismissed; public string Text; public int Step; public bool Visible,Controls; public void SetControlsInteractable(bool value) {Controls=value;} public void SetHintVisible(bool value) { Visible = value; } public void SetTutorialHint(string text, int step = 0) { Text = text; Step = step; Visible = true; } }
class TutorialController {
    private GameManager gameManager; private GameUI gameUI; private BucketInventoryUI bucketInventoryUI;
    private SandEconomyHud economyHud; private LocalizationManager localization; private LevelIntroSandAnimator introAnimator;
    private bool tutorialOnFirstLevelOnly = true, tutorialEnabled, completed, stepActive, levelHintActive;
    private float animationTime, watchTime; private Bucket targetBucket; private int phase;
    private ColorFocusPanel filterPanel;public int FilterTutorialStep;private bool handVisible;

    private const string CompletedKey = "sand_tutorial_v2_complete";
    private T FindObjectOfType<T>() where T:class => typeof(T)==typeof(ColorFocusPanel)?new ColorFocusPanel() as T:null;
    private void RefreshLanguage() {} private void ResolveCanvas() {} private void EnsureOverlay() {} private void ResolveIntroAnimator() {} private void DisableTutorialVisuals() {handVisible=false;} private void SetTutorialVisualsVisible(bool visible) {}
METHODS
    public static void Main() {
        var tutorial = new TutorialController(); var manager = new GameManager(); var ui = new GameUI(); var stock = new BucketInventoryUI();
        Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(ui.Visible && ui.Step == 1 && ui.Text == "tutorial_pick", "Completed save must not suppress first-level tutorial on replay");
        tutorial.HandleBucketPlaced(stock.Target);
        Require(ui.Visible && ui.Step == 2 && ui.Text == "tutorial_watch", "Step 2 must request its instruction text");
        tutorial.HandleBucketFilled(stock.Target);
        Require(ui.Visible && ui.Step == 3 && ui.Text == "tutorial_repeat", "Step 3 must request its instruction text");
        tutorial.HandleBucketPlaced(stock.Target);
        Require(Analytic.Count("tutorial_basic_pick_completed")==1 && Analytic.Count("tutorial_basic_watch_fill_completed")==1 && Analytic.Count("tutorial_basic_repeat_completed")==1,"Each real basic action reaches its stable goal");
        tutorial.HandleBucketFilled(stock.Target); tutorial.HandleBucketPlaced(stock.Target);
        Require(Analytic.Count("tutorial_basic_repeat_completed")==1,"Repeated callbacks must not complete steps twice");
        Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(ui.Step == 1 && ui.Text == "tutorial_pick", "Reopening level must reset tutorial to step 1");
        manager.CurrentLevelIndex = 1; Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager, ui, stock, null);
        Require(!tutorial.tutorialEnabled, "First-level tutorial must not leak into later levels");
        Require(tutorial.FilterTutorialStep==1 && ui.Visible,"Ordinary palette tutorial missing");
        tutorial.ShowFilterHint(); tutorial.HandleFilterSelected(0);
        Require(Analytic.Count("tutorial_filter_pick_completed")==0,"Wrong filter selection cannot complete a step");
        tutorial.HandleFilterSelected(7); tutorial.HandleFilterSelected(7); tutorial.ShowFilterHint();
        tutorial.HandleFilterSelected(0); tutorial.HandleFilterSelected(0);
        Require(Analytic.Count("tutorial_filter_pick_completed")==1 && Analytic.Count("tutorial_filter_reset_completed")==1,"Both real filter actions reach goals once");
        manager.Seasonal=true;
        foreach(int display in new[]{0,1,24,199}){
            manager.CurrentLevelIndex=1000+display;manager.DisplayIndex=display;ui.Visible=true;tutorial.handVisible=true;
            Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager,ui,stock,null);
            Require(!tutorial.tutorialEnabled && !tutorial.stepActive && !tutorial.levelHintActive && tutorial.FilterTutorialStep==0 && !tutorial.handVisible && !ui.Visible && ui.Controls,"Winter must suppress all tutorials and leave controls available");
        }
        manager.Seasonal=false;manager.CurrentLevelIndex=0;Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager,ui,stock,null);
        Require(tutorial.tutorialEnabled && ui.Step==1 && ui.Text=="tutorial_pick","Ordinary tutorial did not resume after winter");
        manager.CurrentLevelIndex=24;Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager,ui,stock,null);
        Require(ui.Visible && ui.Text=="lesson","Ordinary mechanic hints were removed");
        tutorial.HandleHintDismissed(); tutorial.HandleHintDismissed();
        Require(Analytic.Count("tutorial_mechanics_lesson_test_completed")==1,"Lesson dismissal completes one mechanic step");
        manager.CurrentLevelIndex=0; Analytic.Begin(manager); tutorial.HandleLevelLoaded(manager,ui,stock,null);
        tutorial.BeginFirstStep(); tutorial.BeginFirstStep();
        int filledBefore=Analytic.Count("tutorial_basic_watch_fill_completed");
        tutorial.HandleBucketPlaced(new Bucket());
        Require(tutorial.phase==0,"Wrong bucket cannot advance the tutorial");
        tutorial.HandleBucketPlaced(stock.Target); tutorial.FinishWatchStep(false); tutorial.HandleBucketFilled(stock.Target);
        Require(Analytic.Count("tutorial_step_skipped")==1 && Analytic.Count("tutorial_basic_watch_fill_completed")==filledBefore,"Timeout is skipped and never fabricates a filled bucket");
        var started=Analytic.Events.Where(e=>e.Item1=="tutorial_step_started" && (string)e.Item2["step_id"]=="pick" && (string)e.Item2["tutorial_id"]=="basic").ToArray();
        Require(started.Length>=2 && started.Select(e=>(string)e.Item2["attempt_id"]).Distinct().Count()==started.Length,"Hint refreshes deduplicate per attempt; restart gets its own attempt");
        SandEconomyHud.CheckWinterTutorials();
        SandEconomyHud.CheckBoosterAnalytics();
        Console.WriteLine("TUTORIAL_STEP_ANALYTICS_ACTIONS_DEDUP_TIMEOUT_REPLAY_PASS");
        Console.WriteLine("TUTORIAL_REPLAY_AND_THREE_STEPS_PASS");
        Console.WriteLine("WINTER_FIRST_PALETTE_AND_MECHANIC_TUTORIALS_DISABLED_PASS");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
'@
$boosterSource=[IO.File]::ReadAllText("$taskRoot/Assets/Scripts/UI/SandEconomyHud.cs")
$boosterMethods = foreach ($boosterName in @('TryStartBoosterTutorial','TrackBoosterTutorialStep','BeginBoosterActivationTutorial','BeginTransparentTutorialAfterFirstBucket','HandleTutorialConveyorChanged','HandleBoosterButton','CompleteBoosterUse','FinishTutorial','TryHandleBucketClick','TryHandleConveyorBucketClick','HandleBoosterModeCancelled')) {
    $boosterMatch=[regex]::Match($boosterSource,"    (public|private) (void|bool) $boosterName\([^)]*\)\s*\{")
    if(-not $boosterMatch.Success){throw "Booster method missing: $boosterName"}
    $boosterStart=$boosterSource.IndexOf('{',$boosterMatch.Index);$boosterDepth=1;$boosterEnd=$boosterStart+1
    while($boosterDepth -gt 0){if($boosterSource[$boosterEnd] -eq '{'){$boosterDepth++};if($boosterSource[$boosterEnd] -eq '}'){$boosterDepth--};$boosterEnd++}
    $boosterSource.Substring($boosterMatch.Index,$boosterEnd-$boosterMatch.Index)
}
$taskHarness=$taskHarness.Replace('BOOSTER_METHOD',($boosterMethods -join "`n"))
$taskOutput = "$taskRoot/Temp/TutorialFlowChecks"
New-Item -ItemType Directory -Force $taskOutput | Out-Null
[IO.File]::WriteAllText("$taskOutput/check.cs", $taskHarness.Replace('METHODS', ($taskMethods -join "`n")))
$taskReferences = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" -nologo -target:exe -nostdlib "-out:$taskOutput/check.exe" "-r:$taskReferences/mscorlib.dll" "-r:$taskReferences/System.Core.dll" "$taskRoot/Assets/Scripts/SDK/LevelAnalyticsSession.cs" "$taskOutput/check.cs"
if ($LASTEXITCODE -ne 0) { throw 'Tutorial harness compilation failed' }
& "$UnityData/MonoBleedingEdge/bin/mono.exe" "$taskOutput/check.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tutorial flow check failed' }
