"""Prepare isolated full-simulation checks from current source assets.

Example: python Tools/prepare_all_level_playability.py --project Temp/PlayabilityShard0 --first 0 --last 299
Run AllLevelPlayabilityChecks.Run with SAND_PLAYABILITY_OUTPUT/FIRST/LAST.
"""
from pathlib import Path
import argparse, hashlib, json, re, shutil
ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', default='Temp/AllLevelPlayabilityValidation')
parser.add_argument('--first', type=int, default=0)
parser.add_argument('--last', type=int, default=1199)
parser.add_argument('--case-indices', help='JSON file with a cases array containing selected indices')
parser.add_argument('--manifest-output', default='tasks/all-level-playability')
args = parser.parse_args()
OUTPUT = ROOT / args.manifest_output
selected_indices = None
if args.case_indices:
    selected_indices = {c['index'] for c in json.loads((ROOT / args.case_indices).read_text(encoding='utf-8'))['cases']}
PROJECT = (ROOT / args.project).resolve()
assert PROJECT.is_relative_to(ROOT / 'Temp'), 'Validation project must stay under workspace Temp'
for directory in ['Assets/Core', 'Assets/Editor', 'Packages', 'ProjectSettings']:
    (PROJECT / directory).mkdir(parents=True, exist_ok=True)
sources = {p.name: p for p in (ROOT / 'Assets/Scripts').rglob('*.cs')}
names = """Bucket BucketGridAccess BucketInventory BucketLayoutAccess BucketRouteAccess
CampaignProgression CampaignRunFactory ColorQuantizer ConveyorBelt ConveyorDirection
ConveyorSettings ConveyorSlotState InventoryLoopRoute LevelBucketLayoutGenerator
LevelCellCodec LevelData LevelSegmentDatabase LevelSegmentRules SandColorPalette
FallingSandSimulator SandGrid SandGrainColorBaker SandCollector""".split()
hashes = {}
for name in names:
    source = sources[name + '.cs']
    shutil.copy2(source, PROJECT / 'Assets/Core' / source.name)
    shutil.copy2(str(source) + '.meta', str(PROJECT / 'Assets/Core' / source.name) + '.meta')
    hashes[str(source.relative_to(ROOT)).replace('\\', '/')] = hashlib.sha256(source.read_bytes()).hexdigest()
(PROJECT / 'Assets/Core/ValidationStubs.cs').write_text("""using UnityEngine;
// Presentation, saves and analytics are omitted; all sand, bucket and collection behavior is production code.
public class SandPourAnimator : MonoBehaviour { public bool HasActivePourTo(Bucket bucket)=>false; }
public class LevelLoader : MonoBehaviour { public LevelData CurrentLevel; public void SetCurrentLevel(LevelData level){CurrentLevel=level;} }
public static class Analytic { public static void ConveyorExpanded(string level){} }
public static class ProgressSaveService { public static int CurrentLevelIndex=>0; }
public static class CampaignText { public static string Get(string key)=>key; }
""")
shutil.copy2(ROOT / 'ProjectSettings/ProjectVersion.txt', PROJECT / 'ProjectSettings/ProjectVersion.txt')
(PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {'com.unity.modules.ui': '1.0.0'}}, indent=2))
scene = (ROOT / 'Assets/Scenes/GameScene.unity').read_text()
settings = {}
for field in ['ticksPerSecond', 'maxTicksPerFrame', 'maxCollectPerTick', 'maxCollectPerFrame', 'collectRowCount', 'straightTicksBeforeDiagonal']:
    matches = re.findall(r'^  ' + field + r': ([\d.]+)$', scene, re.M)
    assert len(matches) == 1
    settings[field] = float(matches[0]) if field == 'ticksPerSecond' else int(matches[0])
# Production MobilePortraitLayout at 1080x1920, full safe area; 152px booster bar from SandEconomyHud.
sand_size = min(720, 1920-100-88-12-(52+152+20)-20-(220+20+(5*96+4*10)+42))
settings.update(sandImageWidth=sand_size-24, conveyorWidth=sand_size+28, bucketWidth=96)
(PROJECT / 'scene-settings.json').write_text(json.dumps(settings, indent=2))
reference = sources['SandConveyorMapper.cs'].read_text(encoding='utf-8').replace('class SandConveyorMapper', 'class ReferenceSandConveyorMapper')
(PROJECT / 'Assets/Core/ReferenceSandConveyorMapper.cs').write_text(reference, encoding='utf-8')
half = settings['sandImageWidth'] / 2 - 12
(PROJECT / 'Assets/Core/SandConveyorMapper.cs').write_text(f"""using UnityEngine;
// Fixed-geometry test fixture only; verified against the production mapper before simulation.
public class SandConveyorMapper : MonoBehaviour {{
 public void EnsureReferences() {{}}
 static float Center(int column,int width)=>-{half}f+((column+.5f)/width)*{half*2}f;
 public float GetColumnAlignmentError(float position,int column,int width)=>Mathf.Abs(position-Center(column,width));
 public bool IsBucketOverSand(float position,int width)=>width>0 && position>=-{half}f-{half}f/width && position<={half}f+{half}f/width;
 public bool IsColumnUnderBucket(float position,int column,int width) {{
  if(!IsBucketOverSand(position,width))return false;
  float center=Center(column,width),half={half}f/width;
  return center-half<position+ConveyorBelt.BucketWidth*.5f && center+half>position-ConveyorBelt.BucketWidth*.5f;
 }}
}}
""", encoding='utf-8')
(PROJECT / 'Assets/Editor/PlayabilityMappingFixture.cs').write_text(f"""using System;
using UnityEngine;
public static class PlayabilityMappingFixture {{
 public static void Validate() {{
  var root=new GameObject("Mapping contract");
  try {{
   var panel=new GameObject("SandPanel",typeof(RectTransform));panel.transform.SetParent(root.transform,false);
   var image=new GameObject("SandImage",typeof(RectTransform));image.transform.SetParent(panel.transform,false);
   ((RectTransform)image.transform).sizeDelta=new Vector2({settings['sandImageWidth']},{settings['sandImageWidth']});
   var belt=new GameObject("ConveyorPanel",typeof(RectTransform));belt.transform.SetParent(root.transform,false);
   ((RectTransform)belt.transform).sizeDelta=new Vector2({settings['conveyorWidth']},{settings['conveyorWidth']});
   ConveyorBelt.ConfigureGeometry({settings['conveyorWidth']},{settings['bucketWidth']});
   var reference=root.AddComponent<ReferenceSandConveyorMapper>();reference.EnsureReferences();
   var fixedMapper=root.AddComponent<SandConveyorMapper>();
   int checks=0;
   for(int width=8;width<=64;width*=2)
   for(float x=-450;x<=450;x+=.5f) {{
    if(reference.IsBucketOverSand(x,width)!=fixedMapper.IsBucketOverSand(x,width))throw new Exception("Mapping bounds differ at "+x);
    for(int column=0;column<width;column++) {{
     if(reference.IsColumnUnderBucket(x,column,width)!=fixedMapper.IsColumnUnderBucket(x,column,width)
      ||Mathf.Abs(reference.GetColumnAlignmentError(x,column,width)-fixedMapper.GetColumnAlignmentError(x,column,width))>.0001f)
       throw new Exception("Mapping contract differs at "+x+"/"+column+"/"+width);
     checks++;
    }}
   }}
   Debug.Log("FIXED_GEOMETRY_EQUIVALENCE_PASS: "+checks+" mappings compared to production transforms");
  }} finally {{UnityEngine.Object.DestroyImmediate(root);}}
 }}
}}
""")
shutil.copy2(ROOT / 'Tools/AllLevelPlayabilityChecks.cs', PROJECT / 'Assets/Editor/AllLevelPlayabilityChecks.cs')
meta_paths = {re.search(r'^guid: (\w+)', p.read_text(), re.M)[1]: p.with_suffix('') for p in (ROOT / 'Assets/Levels').rglob('*.asset.meta')}
database = (ROOT / 'Assets/Levels/LevelDatabase.asset').read_text()
level_guids = re.findall(r'^  - \{fileID: 11400000, guid: (\w+), type: 2\}', database, re.M)
cases = []
for index, guid in enumerate(level_guids):
    source = meta_paths[guid]
    text = source.read_text(encoding='utf-8-sig')
    rule = meta_paths[re.search(r'bucketSettingsOverride:.*?guid: (\w+)', text, re.S)[1]]
    for asset in [source, rule]:
        hashes[str(asset.relative_to(ROOT)).replace('\\', '/')] = hashlib.sha256(asset.read_bytes()).hexdigest()
        if args.first <= index <= args.last and (selected_indices is None or index in selected_indices):
            destination = PROJECT / asset.relative_to(ROOT)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(asset, destination)
            shutil.copy2(str(asset) + '.meta', str(destination) + '.meta')
    cases.append(dict(index=index, path=str(source.relative_to(ROOT)).replace('\\', '/')))
(PROJECT / 'column-cases.json').write_text(json.dumps({'cases': cases}))
for source in [sources['SandConveyorMapper.cs'], ROOT/'Assets/Levels/LevelDatabase.asset', ROOT/'Assets/Scenes/GameScene.unity', ROOT/'Assets/Scripts/UI/MobilePortraitLayout.cs', ROOT/'Assets/Scripts/UI/SandEconomyHud.cs', ROOT/'Assets/Settings/ConveyorSettings.asset']:
    hashes[str(source.relative_to(ROOT)).replace('\\', '/')] = hashlib.sha256(source.read_bytes()).hexdigest()
OUTPUT.mkdir(exist_ok=True)
(OUTPUT / 'source-hashes.json').write_text(json.dumps(hashes, indent=2))
(OUTPUT / 'scene-settings.json').write_text(json.dumps(settings, indent=2))
print('Prepared', PROJECT.name, 'levels', args.first, '..', args.last, 'of', len(cases), 'with scene settings:', settings)
