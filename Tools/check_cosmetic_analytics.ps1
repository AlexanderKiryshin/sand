#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sources=@('Assets/Scripts/UI/ThemeCatalog.cs','Assets/Scripts/UI/CosmeticBundleCatalog.cs','Assets/Scripts/UI/NatureBackgroundBundle.cs','Assets/Scripts/SDK/Analytic.cs','Assets/Scripts/SDK/LevelAnalyticsSession.cs','Assets/Scripts/Sand/SandGrid.cs') | ForEach-Object {
 [regex]::Replace((Get-Content (Join-Path $root $_) -Raw),'(?m)^using [^\r\n]+;\r?\n','')
}
$focus=Get-Content (Join-Path $root 'Assets/Scripts/UI/ColorFocusPanel.cs') -Raw
$tutorial=Get-Content (Join-Path $root 'Assets/Scripts/UI/TutorialController.cs') -Raw
function Extract-Method([string]$source,[string]$signature) {
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Method missing: $signature"}
 $brace=$source.IndexOf('{',$start);$position=$brace;$depth=1
 while($depth -gt 0){$position++;if($source[$position] -eq '{'){$depth++};if($source[$position] -eq '}'){$depth--}}
 $source.Substring($start,$position-$start+1)
}
$redMethod=Extract-Method $focus 'public static byte FindRedColor('
$filterHandler=(Extract-Method $tutorial 'private void HandleFilterSelected(').Replace('private void','public void')
$harness=[regex]::Replace((Get-Content (Join-Path $root 'Tools/check_cosmetic_analytics.cs') -Raw),'(?m)^using [^\r\n]+;\r?\n','')
$remainingMethod=Extract-Method (Get-Content (Join-Path $root 'Assets/Scripts/Sand/SandRenderer.cs') -Raw) 'public bool HasRemainingColor('
$paletteRefresh=(Extract-Method $focus 'private void RefreshSwatchAvailability(').Replace('private void','public void')
$harness=$harness.Replace('FILTER_HANDLER',$filterHandler).Replace('RED_METHOD',$redMethod).Replace('REMAINING_COLOR_METHOD',$remainingMethod).Replace('PALETTE_REFRESH_METHOD',$paletteRefresh)
$sources=$sources|ForEach-Object{$_ -replace '(?<!\.)\bRandom\.Range','UnityEngine.Random.Range'}
Add-Type ("using System;`nusing System.Linq;`nusing System.Collections.Generic;`nusing UnityEngine;`nusing MirraGames.SDK.Common;`nusing Object = UnityEngine.Object;`n"+($sources -join "`n")+"`n"+$harness) -WarningAction SilentlyContinue
[CosmeticChecks]::Run()
$palette=@([regex]::Matches((Get-Content (Join-Path $root 'Assets/Levels/Campaign100/Level_002.asset') -Raw),'rgba: (\d+)')|ForEach-Object{$rgba=[uint32]$_.Groups[1].Value;[UnityEngine.Color32]::new(($rgba-band255),(($rgba-shr8)-band255),(($rgba-shr16)-band255),(($rgba-shr24)-band255))})
$red=[TutorialRedVerification]::FindRedColor([UnityEngine.Color32[]]$palette)
if($red -eq 0){throw 'Actual second-level palette has no visible tutorial target'}
"PASS: red tutorial target is swatch $red; wrong color cannot advance; acquisition/equip context and deduplication; restoration is not acquisition; analytics errors preserve ownership; random unowned bundles and 50/70/90 cadence; 100-600 casing and 400-1000 conveyor prices; live 25% palette availability and 1000 sand mutations."