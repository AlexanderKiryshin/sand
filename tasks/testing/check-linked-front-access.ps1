#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Conveyor/BucketInventory.cs') -Raw
function Extract-Method([string] $pattern) {
    $match = [regex]::Match($source, $pattern)
    if (-not $match.Success) { throw "Missing inventory method: $pattern" }
    $start = $match.Index
    $brace = $source.IndexOf('{', $start)
    $arrow = $source.IndexOf('=>', $start)
    if ($arrow -ge 0 -and $arrow -lt $brace) { return $source.Substring($start, $source.IndexOf(';', $arrow) - $start + 1) }
    $position=$brace; $depth=1
    while ($depth -gt 0) { $position++; if ($source[$position] -eq '{') {$depth++}; if ($source[$position] -eq '}') {$depth--} }
    return $source.Substring($start, $position-$start+1)
}
$methods = @(
    (Extract-Method '(?m)^    public bool IsBucketAccessible\('),
    (Extract-Method '(?m)^    private bool IsSingleBucketAccessible\('),
    (Extract-Method '(?m)^    public bool TryTakeBucket\(Bucket bucket, ConveyorBelt conveyor, LevelData levelData, bool ignoreAccessibility\)')
)
$stubs = @"
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {}
public class Bucket {
    public int LayoutIndex; public Bucket LinkedPartner; public bool IsFrozen;
    public Bucket(int index) { LayoutIndex=index; }
    public void Reveal() {} public void ThawOneMove() {}
}
public class LevelData {
    public const int BucketLayoutColumns=5, BucketLayoutRows=5, BucketLayoutCellCount=25;
    public bool alternatingCrates; public byte[] bucketLayout = new byte[25];
    public byte GetBucketLayoutCell(int x,int y) => bucketLayout[y*5+x];
    public static bool IsBucketLayoutWall(byte c) => c==255;
    public static bool IsBucketLayoutConveyorSlot(byte c) => c==254;
}
public class ConveyorSlotState { public List<Bucket> ReservedBuckets=new List<Bucket>(); }
public class ConveyorBelt {
    public int Used; public bool CanAcceptBucket() => true;
    public bool TryPlaceBucket(Bucket bucket) { Used += bucket.LinkedPartner==null ? 1 : 2; return true; }
}
public class BucketInventory {
    public List<Bucket> buckets=new List<Bucket>();
    public List<Bucket> reserve=new List<Bucket>();
    public List<ConveyorSlotState> conveyorSlots=new List<ConveyorSlotState>();
    public bool CratesOpen; public Bucket GetBucketAtLayoutIndex(int index) => buckets.FirstOrDefault(b=>b.LayoutIndex==index);
    public bool IsGateClosed(Bucket b) => false; public bool IsCrateCovered(Bucket b) => false;
    void ApplySuccessfulMoveRules() {} bool TryDispenseAll(LevelData l) => false;
    void EnsurePlayableOpening(LevelData l) {} void NotifyChanged() {}
"@
$route = (Get-Content (Join-Path $projectRoot 'Assets/Scripts/Conveyor/BucketRouteAccess.cs') -Raw) -replace '(?m)^using [^;]+;\r?\n',''
$grid = (Get-Content (Join-Path $projectRoot 'Assets/Scripts/Conveyor/BucketGridAccess.cs') -Raw) -replace '(?m)^using [^;]+;\r?\n',''
Add-Type ($stubs + ($methods -join "`n") + "`n}`n" + $route + "`n" + $grid)
function Require([bool]$condition,[string]$message) { if (-not $condition) {throw $message} }
$level=[LevelData]::new()
$inv=[BucketInventory]::new();$a=[Bucket]::new(0);$b=[Bucket]::new(5);$a.LinkedPartner=$b;$b.LinkedPartner=$a;$inv.buckets.AddRange([Bucket[]]@($a,$b))
Require (-not $inv.IsBucketAccessible($a,$level) -and -not $inv.IsBucketAccessible($b,$level)) 'FAIL: a linked bucket hidden below its own partner must block both cups'
$inv=[BucketInventory]::new();$a=[Bucket]::new(0);$b=[Bucket]::new(6);$blocker=[Bucket]::new(1);$a.LinkedPartner=$b;$b.LinkedPartner=$a;$inv.buckets.AddRange([Bucket[]]@($a,$b,$blocker));$belt=[ConveyorBelt]::new()
Require (-not $inv.IsBucketAccessible($a,$level) -and -not $inv.IsBucketAccessible($b,$level)) 'FAIL: an empty side route must not bypass the bucket above the linked partner'
Require (-not $inv.TryTakeBucket($a,$belt,$level,$false) -and $belt.Used -eq 0 -and $inv.buckets.Count -eq 3) 'FAIL: blocked pair must not move or mutate inventory'
$inv.buckets.Remove($blocker) | Out-Null
Require ($inv.IsBucketAccessible($a,$level) -and $inv.IsBucketAccessible($b,$level)) 'FAIL: both column-front cups must become available together'
Require ($inv.TryTakeBucket($b,$belt,$level,$false) -and $belt.Used -eq 2 -and $inv.buckets.Count -eq 0) 'FAIL: either cup must dispatch the whole available pair atomically'
$inv=[BucketInventory]::new();$ordinary=[Bucket]::new(6);$blocker=[Bucket]::new(1);$inv.buckets.AddRange([Bucket[]]@($ordinary,$blocker))
Require $inv.IsBucketAccessible($ordinary,$level) 'FAIL: ordinary bucket side-exit access must remain available'
Write-Output 'PASS: both linked cups require the front of their own column; blocked dispatch is atomic; removing the blocker releases the pair; ordinary side routes remain available.'
