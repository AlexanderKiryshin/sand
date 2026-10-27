param([string]$UnityData = 'G:/Unity/2022.3.62f3/Editor/Data')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskOutput = "$taskRoot/Temp/TutorialAnalyticsCompile"
New-Item -ItemType Directory -Force $taskOutput | Out-Null
$taskMono = "$UnityData/MonoBleedingEdge/lib/mono/4.5"
$taskRefs = @("$taskMono/mscorlib.dll", "$taskMono/System.dll", "$taskMono/System.Core.dll", "$taskMono/Facades/netstandard.dll")
$taskRefs += (Get-ChildItem "$UnityData/Managed/UnityEngine" -Filter '*.dll').FullName
$taskRefs += (Get-ChildItem "$UnityData/Managed" -Filter 'Unity*.dll').FullName
$taskRefs += (Get-ChildItem "$taskRoot/Library/ScriptAssemblies" -Filter '*.dll').FullName
$taskSources = @('Assets/Scripts/SDK/LevelAnalyticsSession.cs','Assets/Scripts/SDK/Analytic.cs','Assets/Scripts/SDK/WebGameAnalytics.cs','Assets/Scripts/UI/TutorialController.cs','Assets/Scripts/UI/SandEconomyHud.cs','Assets/Scripts/UI/GameUI.cs')
foreach ($taskMode in @('UNITY_EDITOR,UNITY_WEBGL','UNITY_WEBGL')) {
    $taskName = if ($taskMode.Contains('UNITY_EDITOR')) {'editor'} else {'webgl'}
    $taskArgs = @('-nologo','-target:library','-nostdlib','-nowarn:0436',"-define:$taskMode", "-out:`"$taskOutput/$taskName.dll`"")
    $taskArgs += $taskRefs | Where-Object {Test-Path -LiteralPath $_} | Sort-Object -Unique | ForEach-Object {"-r:`"$_`""}
    $taskArgs += $taskSources | ForEach-Object {"`"$taskRoot/$_`""}
    [IO.File]::WriteAllLines("$taskOutput/$taskName.rsp", $taskArgs)
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$taskOutput/$taskName.rsp"
    if ($LASTEXITCODE -ne 0) {throw "Tutorial analytics $taskName compilation failed"}
}
Write-Output 'TUTORIAL_ANALYTICS_EDITOR_AND_WEBGL_MANAGED_COMPILE_PASS'
