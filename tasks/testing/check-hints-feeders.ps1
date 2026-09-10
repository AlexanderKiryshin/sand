$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Get-Content (Join-Path $root 'Assets/Scripts/Conveyor/ConveyorDirection.cs') -Raw
Add-Type ($source + ' public static class LevelData { public const int BucketLayoutColumns = 5; public const int BucketLayoutRows = 5; }')
$failures = @()
$arrow = Get-Content (Join-Path $root 'Assets/Scripts/UI/ConveyorSlotView.cs') -Raw
# Read the generated sprite's tip interval: the narrow point is its low-Y end.
$match = [regex]::Match($arrow, 'bool tip = y >= ([0-9]+) && y <= ([0-9]+)')
$tipY = [int]$match.Groups[1].Value
$baseY = [int]$match.Groups[2].Value
foreach ($direction in [Enum]::GetValues([ConveyorDirection])) {
    $target = [ConveyorDirectionUtility]::GetTargetLayoutIndex(12, $direction)
    $dx = ($target % 5) - 2
    $dy = -( [Math]::Floor($target / 5) - 2)
    $angle = [ConveyorDirectionUtility]::GetArrowRotationDegrees($direction) * [Math]::PI / 180
    $arrowX = -($tipY - $baseY) * [Math]::Sin($angle)
    $arrowY = ($tipY - $baseY) * [Math]::Cos($angle)
    if ($arrowX * $dx + $arrowY * $dy -le 0) { $failures += "Arrow $direction points away from its actual exit" }
}
$tutorial = Get-Content (Join-Path $root 'Assets/Scripts/UI/TutorialController.cs') -Raw
if ($tutorial -match 'tipTime *-=' ) { $failures += 'Level hint expires on a timer' }
$level = Get-Content (Join-Path $root 'Assets/Levels/Campaign100/Level_005.asset') -Raw
$layout = [regex]::Match($level, 'bucketLayout: ([0-9a-f]+)').Groups[1].Value
$exit = [ConveyorDirectionUtility]::GetTargetLayoutIndex(7, [ConveyorDirection]::Front)
if ($layout.Substring($exit * 2, 2) -eq 'ff') { $failures += 'Level 5 feeder exit is a wall' }
if ($failures.Count) { $failures | ForEach-Object { Write-Output "FAIL: $_" }; exit 1 }
Write-Output 'PASS: all four arrows match grid exits; level 5 exit is clear; hints have no expiry timer.'
