"""Apply only successful sand-supply candidates; preserve all level art and mechanics."""
from pathlib import Path
from collections import Counter
import argparse, hashlib, json, re
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'tasks/playability-repair'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--apply', action='store_true')
args = parser.parse_args()
baseline = json.loads((OUT/'baseline-summary.json').read_text(encoding='utf-8'))
targets = {r['index'] for r in baseline['levels'] if not r['passed']}
candidates = {}
for folder in sorted(OUT.glob('shard-*')):
    path = folder/'candidates.jsonl'
    if not path.exists(): continue
    for line in path.read_text(encoding='utf-8').splitlines():
        r = json.loads(line)
        if r['passed'] and r['index'] in targets: candidates.setdefault(r['index'], r)
missing = sorted(targets-set(candidates))
print(f'Confirmed candidates: {len(candidates)}/{len(targets)}; missing: {missing}')
if missing: raise SystemExit(2)
meta = {re.search(r'^guid: (\w+)', p.read_text(), re.M)[1]: p.with_suffix('') for p in (ROOT/'Assets/Levels').rglob('*.asset.meta')}
guids = re.findall(r'^  - \{fileID: 11400000, guid: (\w+), type: 2\}', (ROOT/'Assets/Levels/LevelDatabase.asset').read_text(), re.M)
paths = [meta[g] for g in guids]
rules = [meta[re.search(r'bucketSettingsOverride:.*?guid: (\w+)', p.read_text(encoding='utf-8-sig'), re.S)[1]] for p in paths]
assert len(set(rules)) == len(rules), 'Expected one independent rule asset per level'
changes=[]
witnesses=[]
for previous in baseline['levels']:
    index=previous['index']
    witness=candidates[index] if index in targets else previous
    witnesses.append(dict(index=index,path=paths[index].relative_to(ROOT).as_posix(),witness=witness))
    if index not in targets: continue
    rule=rules[index]
    original=rule.read_bytes()
    text=original.decode('utf-8-sig')
    old=int(re.search(r'^  maxGrainsPerBucket: (\d+)',text,re.M)[1])
    assert old==witness['originalBucketLimit'], (index,old,witness['originalBucketLimit'])
    new=re.sub(r'^  maxGrainsPerBucket: \d+',f"  maxGrainsPerBucket: {witness['bucketLimit']}",text,flags=re.M)
    assert 'matchBucketOrderToSand:' not in new
    new=new.rstrip()+f"\n  matchBucketOrderToSand: 1\n  verifiedLayoutSeed: {witness['seed']}\n"
    relative=rule.relative_to(ROOT)
    changes.append(dict(index=index,number=witness['number'],winter=witness['winter'],rule=relative.as_posix(),oldLimit=old,newLimit=witness['bucketLimit'],initialBuckets=witness['initialBuckets'],originalSha256=hashlib.sha256(original).hexdigest()))
    if args.apply:
        backup=OUT/'backup'/relative
        backup.parent.mkdir(parents=True,exist_ok=True)
        if backup.exists(): assert backup.read_bytes()==original
        else: backup.write_bytes(original)
        rule.write_text(new,encoding='utf-8',newline='')
print('Capacity factors:',dict(Counter(round(c['newLimit']/c['oldLimit'],2) for c in changes)))
if args.apply:
    (OUT/'applied-changes.json').write_text(json.dumps(changes,indent=2),encoding='utf-8')
    (OUT/'verified-cases.json').write_text(json.dumps(dict(cases=witnesses)),encoding='utf-8')
    for shard in range(4):
        (OUT/f'verified-cases-{shard}.json').write_text(json.dumps(dict(cases=witnesses[shard::4])),encoding='utf-8')
    print('Applied',len(changes),'rules; wrote',len(witnesses),'verification witnesses.')
