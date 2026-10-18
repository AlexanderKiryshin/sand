param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    New-Item -ItemType Directory -Force Temp/AdAnalyticsChecks | Out-Null
    $taskReferences = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" `
        -nologo -target:exe -out:Temp/AdAnalyticsChecks/check.exe -nostdlib `
        "-r:$taskReferences/mscorlib.dll" "-r:$taskReferences/System.Core.dll" `
        Tools/check_ad_analytics.cs Assets/Scripts/SDK/GameSdkReadiness.cs Assets/Scripts/SDK/AdManager.cs Assets/Scripts/SDK/Analytic.cs `
        Assets/Scripts/SDK/GameIsReady.cs Assets/Scripts/UI/OpenWindow.cs `
        Assets/Scripts/SDK/LevelAnalyticsSession.cs Assets/Scripts/SDK/RewardedAttempt.cs Assets/Scripts/UI/RewardedShop.cs `
        Assets/Scripts/Boosters/BoosterService.cs Assets/Scripts/Boosters/BoosterCatalog.cs
    if ($LASTEXITCODE -ne 0) { throw 'Analytics check compilation failed.' }
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" Temp/AdAnalyticsChecks/check.exe
    if ($LASTEXITCODE -ne 0) { throw 'Analytics callback checks failed.' }
} finally {
    Pop-Location
}
