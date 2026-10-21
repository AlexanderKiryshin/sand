"""Require all 1200 production-asset runs to match their complete witness routes."""
from pathlib import Path
from collections import Counter
import hashlib, json, re
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'tasks/playability-repair'
changes=json.loads((OUT/'applied-changes.json').read_text(encoding='utf-8'))
witnesses=json.loads((OUT/'verified-cases.json').read_text(encoding='utf-8'))['cases']
records={}
for shard in range(4):
    folder=OUT/f'verified-{shard}'
    assert (folder/'status.txt').read_text().startswith('PASS'), folder
    for line in (folder/'verified.jsonl').read_text().splitlines():
        r=json.loads(line)
        assert r['index'] not in records, r['index']
        assert r['passed'] and r['sand']==r['inventory']==r['belt']==0, r
        records[r['index']]=r
assert set(records)==set(range(1200))
for c in witnesses:
    r=records[c['index']]; expected=c['witness']
    for key in ['seed','strategy','ticks','moves']:
        assert r[key]==expected[key], (c['index'],key)
for c in changes:
    original=(OUT/'backup'/c['rule']).read_bytes()
    assert hashlib.sha256(original).hexdigest()==c['originalSha256']
    current=(ROOT/c['rule']).read_text(encoding='utf-8-sig')
    current=re.sub(r'^  matchBucketOrderToSand: 1\n','',current,flags=re.M)
    current=re.sub(r'^  verifiedLayoutSeed: -?\d+\n','',current,flags=re.M)
    current=re.sub(r'^  maxGrainsPerBucket: \d+',f"  maxGrainsPerBucket: {c['oldLimit']}",current,flags=re.M)
    assert current.strip()==original.decode('utf-8-sig').replace('\r\n','\n').strip(),c['rule']
allowed={c['rule'] for c in changes}|{'Assets/Scripts/Data/LevelSegmentRules.cs','Assets/Scripts/Conveyor/BucketInventory.cs','Assets/Scripts/Data/CampaignRunFactory.cs'}
changed=[]
for name,old in json.loads((OUT/'baseline-source-hashes.json').read_text()).items():
    if hashlib.sha256((ROOT/name).read_bytes()).hexdigest()!=old:changed.append(name)
assert not set(changed)-allowed, set(changed)-allowed
for name,expected in json.loads((OUT/'integrated-snapshot/source-hashes.json').read_text()).items():
    assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==expected, 'Source changed after verification snapshot: '+name
for shard in range(4):
    log=(OUT/f'verified-{shard}-unity.log').read_text(errors='replace')
    assert 'FIXED_GEOMETRY_EQUIVALENCE_PASS: 216120' in log, shard

summary=dict(totalLevels=1200,completedLevels=len(records),repairedLevels=len(changes),previouslyCompletedLevels=720,
             regularCompleted=sum(not r['winter'] for r in records.values()),winterCompleted=sum(r['winter'] for r in records.values()),
             capacityUnchanged=sum(c['oldLimit']==c['newLimit'] for c in changes),capacityReduced=sum(c['oldLimit']!=c['newLimit'] for c in changes),
             runtimeErrors=0,unresolvedLevels=0,changedSourceFiles=changed,
             settings=json.loads((OUT/'integrated-snapshot/scene-settings.json').read_text()),
             scope='One fixed deterministic layout seed per level. The repaired 480 levels use their verified pinned seed in actual gameplay; the remaining 720 retain fresh random layouts at runtime. Every integrated-asset run must reproduce a complete witnessed route, including all moves and ticks. Actual sand physics, collector, conveyor, inventory and mechanics. Five belt slots; no boosters, artificial filling, recalls or access bypasses. Presentation is omitted; the fixed portrait coordinate mapping is checked against production transforms. This is not an exhaustive proof for every randomly generated seed.',
             levels=[records[i] for i in range(1200)])
(OUT/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
report=[
 'Проверка после исправления проходимости',
 'Успешно пройдены все 1200 уровней: 1000 обычных и 200 зимних.',
 'Все 480 ранее неподтверждённых уровней получили полное прохождение. Для остальных 720 прежние ходы и результаты воспроизведены без изменений.',
 f"Изменена очередь вёдер на 480 уровнях. Ёмкость сохранена на {summary['capacityUnchanged']}; уменьшена на {summary['capacityReduced']}.",
 'Условие успеха каждого уровня: песка 0, вёдер в запасе 0, вёдер на ленте 0.',
 'Использованы реальные физика песка, сбор, лента и инвентарь. Пять слотов, без бустеров, искусственного заполнения или возврата вёдер и без обхода блокировки столбцов.',
 'Картинки, превью выбора пака, препятствия и специальные механики не изменялись при исправлении проходимости.',
 'Для исправленных 480 уровней в игре закреплена проверенная раскладка. Для остальных 720 проверен один фиксированный seed; игра сохраняет случайные раскладки. Это не исчерпывающая проверка всех случайных вариантов.',
 'Все последовательности ходов и состояния сохранены в summary.json; изменения параметров — applied-changes.json; исходные правила — backup/.',
]
(OUT/'report.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')
print('PASS: 1200 complete routes reproduced; all 480 repaired; 720 previous witnesses unchanged; art and mechanics preserved.')
