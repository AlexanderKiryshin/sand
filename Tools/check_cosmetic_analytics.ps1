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
$purchaseChecks=[regex]::Replace((Get-Content (Join-Path $root 'Tools/check_cosmetic_purchases.cs') -Raw),'(?m)^using [^\r\n]+;\r?\n','')
$menu=Get-Content (Join-Path $root 'Assets/Scripts/UI/ThemeCustomizationMenu.cs') -Raw
$badge=Get-Content (Join-Path $root 'Assets/Scripts/UI/ShopGoldBadge.cs') -Raw
$purchaseChecks=$purchaseChecks.Replace('PURCHASE_HANDLER',(Extract-Method $menu 'public void ApplyPreview(')).Replace('PRICE_LOOKUP',(Extract-Method $menu 'private static bool TryPrice(')).Replace('AFFORDABLE_COUNT',(Extract-Method $badge 'public static int CountAffordable('))
$sources=$sources|ForEach-Object{$_ -replace '(?<!\.)\bRandom\.Range','UnityEngine.Random.Range'}
Add-Type ("using System;`nusing System.Linq;`nusing System.Collections.Generic;`nusing UnityEngine;`nusing MirraGames.SDK;`nusing MirraGames.SDK.Common;`nusing Object = UnityEngine.Object;`n"+($sources -join "`n")+"`n"+$harness+"`n"+$purchaseChecks) -WarningAction SilentlyContinue
[CosmeticChecks]::Run()
[CosmeticPurchaseChecks]::Run()
"PASS: all 12 bundles blocked by any owned background, including shared backgrounds and random offers; owned materials alone preserve availability."
$catalog=Get-Content (Join-Path $root 'tasks/theme-iap-products.json') -Raw | ConvertFrom-Json
$expectedTags=@(foreach($part in [Enum]::GetValues([ThemeCatalog+Part])) {
 for($id=0;$id -lt [ThemeCatalog]::CountFor($part);$id++) {
  if([ThemeCatalog]::IsAvailable($id,$part) -and [ThemeCatalog]::AccessFor($id,$part) -eq [ThemeCatalog+Access]::Money) {[ThemeCatalog]::ProductTag($id,$part)}
 }
})
$registeredTags=@($catalog.products | Where-Object category -ne 'background_bundle' | ForEach-Object tag)
if($registeredTags.Count -ne $expectedTags.Count -or (Compare-Object ($expectedTags | Sort-Object) ($registeredTags | Sort-Object))) {throw 'Platform catalog does not match the individual purchase IDs used by the game'}
$legacyTags=@(8..11 | ForEach-Object {[ThemeCatalog]::ProductTag($_)})
if(Compare-Object ($legacyTags | Sort-Object) ($catalog.legacy_restore_only | Sort-Object)) {throw 'Legacy restoration IDs missing from the catalog'}
$palette=@([regex]::Matches((Get-Content (Join-Path $root 'Assets/Levels/Campaign100/Level_002.asset') -Raw),'rgba: (\d+)')|ForEach-Object{$rgba=[uint32]$_.Groups[1].Value;[UnityEngine.Color32]::new(($rgba-band255),(($rgba-shr8)-band255),(($rgba-shr16)-band255),(($rgba-shr24)-band255))})
$red=[TutorialRedVerification]::FindRedColor([UnityEngine.Color32[]]$palette)
if($red -eq 0){throw 'Actual second-level palette has no visible tutorial target'}
"PASS: red tutorial target is swatch $red; wrong color cannot advance; acquisition/equip context and deduplication; restoration is not acquisition; analytics errors preserve ownership; random unowned bundles and 50/70/90 cadence; 100-600 casing and 400-1000 conveyor prices; live 25% palette availability and 1000 sand mutations."
"PASS: independent coin, rewarded and IAP purchases; failed/pending/repeated callbacks; all 27 individual and 4 legacy receipts; old saves; exact bundle contents; category-specific affordability counters; platform catalog matches runtime IDs."
