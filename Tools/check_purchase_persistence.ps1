param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $output = 'Temp/PurchasePersistenceChecks'
    New-Item -ItemType Directory -Force $output | Out-Null
    $references = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
    $payments = 'Packages/com.romanlee17.mirrasdk5/Runtime/Common/Interfaces/Payments'
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" `
        -nologo -target:exe "-out:$output/check.exe" -nostdlib `
        "-r:$references/mscorlib.dll" "-r:$references/System.Core.dll" `
        Tools/check_purchase_persistence.cs Assets/Scripts/Economy/CoinManager.cs `
        Assets/Scripts/UI/RewardedShop.cs Assets/Scripts/SDK/GameProgressStore.cs `
        Assets/Scripts/SDK/GameSdkReadiness.cs Assets/Scripts/Boosters/BoosterService.cs `
        Assets/Scripts/Boosters/BoosterCatalog.cs `
        "$payments/Components/IProductSupply.cs" "$payments/Components/IRestoreData.cs" "$payments/Types/RestoreData.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Purchase persistence checks did not compile.' }
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" "$output/check.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Purchase persistence checks failed.' }
} finally { Pop-Location }
