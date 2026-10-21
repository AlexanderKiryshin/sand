"""Summarize only scene-configured full sand simulation results."""
from pathlib import Path
from collections import Counter
import argparse, hashlib, json, re

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'tasks/all-level-playability'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--require-complete', action='store_true')
args = parser.parse_args()
database = (ROOT / 'Assets/Levels/LevelDatabase.asset').read_text()
total = len(re.findall(r'^  - \{fileID: 11400000, guid: (\w+), type: 2\}', database, re.M))
attempts = {}
folders = [OUTPUT / 'scene-full', OUTPUT / 'scene-native-pilot', OUTPUT / 'scene-deeper-search'] + [OUTPUT / f'scene-shard-{n}' for n in range(4)]
for folder in folders:
    path = folder / 'attempts.jsonl'
    if not path.exists():
        continue
    for line in path.read_text().splitlines():
        if not line.strip():
            continue
        record = json.loads(line)
        key = record['index'], record['seed'], record['strategy']
        if key in attempts and attempts[key] != record:
            raise ValueError(f'Deterministic attempts disagree: {key}, {folder}')
        attempts[key] = record
by_level = {}
for record in attempts.values():
    index = record['index']
    if index not in by_level or not by_level[index]['passed'] and (record['passed'] or record['strategy'] > by_level[index]['strategy']):
        by_level[index] = record
passed = [r for r in by_level.values() if r['passed']]
unresolved = [r for r in by_level.values() if not r['passed']]
missing = sorted(set(range(total)) - set(by_level))
changed = []
hash_path = OUTPUT / 'source-hashes.json'
for name, original in json.loads(hash_path.read_text()).items():
    path = ROOT / name
    if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != original:
        changed.append(name)
errors = [r for r in unresolved if r['reason'] not in ['Tick limit', 'No collected grains for 768 ticks']]
full_belt_stalls = sum(sum(map(int, re.findall(r':cost(\d+)', (r.get('diagnostics') or '').partition(' belt=')[2]))) == 5 for r in unresolved)
budget_mismatches = [r['index'] for r in unresolved if any(int(sand) != int(demand) for sand, demand in re.findall(r':sand(\d+)/demand(\d+)', r.get('diagnostics') or ''))]
summary = dict(totalLevels=total, checkedLevels=len(by_level), completedLevels=len(passed),
               unresolvedLevels=len(unresolved), missingIndices=missing, uniqueAttempts=len(attempts),
               runtimeErrors=len(errors), changedSourceFiles=changed, fullBeltStalls=full_belt_stalls, failedStateBudgetMismatches=budget_mismatches,
               regularCompleted=sum(not r['winter'] for r in passed), winterCompleted=sum(r['winter'] for r in passed),
               regularUnresolved=sum(not r['winter'] for r in unresolved), winterUnresolved=sum(r['winter'] for r in unresolved),
               settings=json.loads((OUTPUT / 'scene-settings.json').read_text()),
               scope='One fixed layout seed per level, three baseline legal dispatch strategies; selected unresolved levels receive additional strategies. Actual sand physics, inventory, collector and conveyor. No boosters, artificial filling, recalls, extra belt slots or access bypasses. Presentation animations are omitted. Fixed portrait geometry is verified against production coordinate transforms; native control attempts must match.',
               interpretation='A recorded complete run proves a route for that fixed seed. Unresolved means bounded search did not find a route; it does not prove that every legal order is impossible.',
               levels=[by_level[i] for i in sorted(by_level)])
(OUTPUT / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
def numbers(winter, records):
    return ', '.join(str(r['number']) for r in sorted(records, key=lambda r: r['number']) if r['winter'] == winter) or '—'
report = [
    'Проверка прохождения уровней с реальной симуляцией песка',
    f'Проверено: {len(by_level)} из {total}. Успешных прохождений: {len(passed)}. Не подтверждены: {len(unresolved)}.',
    f'Обычные: подтверждено {summary["regularCompleted"]}, не подтверждено {summary["regularUnresolved"]}.',
    f'Зимние: подтверждено {summary["winterCompleted"]}, не подтверждено {summary["winterUnresolved"]}.',
    f'Уникальных попыток: {len(attempts)}. Ошибок выполнения: {len(errors)}.',
    f'Из неподтверждённых случаев с заполненными пятью слотами конвейера: {full_belt_stalls}. Нарушений баланса цветов: {len(budget_mismatches)}.',
    'Один фиксированный seed раскладки на уровень; три базовых порядка ходов; для части сложных уровней — дополнительные стратегии. Без бустеров, искусственного заполнения, возврата вёдер, дополнительных слотов и обхода доступности.',
    'Успех: песка 0, вёдер в запасе 0, вёдер на конвейере 0.',
    'Не подтверждено: ограниченный поиск не нашёл прохождение. Это не доказательство невозможности любого порядка ходов.',
    '',
    'Не подтверждённые обычные уровни: ' + numbers(False, unresolved),
    '',
    'Не подтверждённые зимние уровни: ' + numbers(True, unresolved),
    '',
    'Подтверждённые обычные уровни: ' + numbers(False, passed),
    '',
    'Подтверждённые зимние уровни: ' + numbers(True, passed),
    '',
    'Не проверены (индексы базы): ' + (', '.join(map(str, missing)) or 'нет'),
    'Изменённые исходные файлы после подготовки: ' + (', '.join(changed) or 'нет'),
]
(OUTPUT / 'report.txt').write_text('\n'.join(report) + '\n', encoding='utf-8')
print(json.dumps({k: summary[k] for k in ['totalLevels','checkedLevels','completedLevels','unresolvedLevels','uniqueAttempts','runtimeErrors','regularCompleted','winterCompleted']}, ensure_ascii=False))
if changed:
    print('SOURCE CHANGES:', changed)
if args.require_complete and missing:
    raise SystemExit('Not all levels have a completed check yet')
