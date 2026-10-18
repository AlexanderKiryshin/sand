from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
for i in range(1, 6):
    source = (root / f'Assets/Levels/ReworkOriginals/Opening_{i}.asset').read_text(encoding='utf-8')
    target = root / f'Assets/Levels/Campaign100/Level_{i:03}.asset'
    current = target.read_text(encoding='utf-8')
    name = re.search(r'^  m_Name: (.*)$', current, re.M)[1]
    source = re.sub(r'^  m_Name: .*$', '  m_Name: ' + name, source, flags=re.M)
    target.write_text(source, encoding='utf-8')
    guid = re.search(r'bucketSettingsOverride:.*?guid: ([a-f0-9]+)', source, re.S)[1]
    for meta in (root / 'Assets/Levels/Campaign100').glob('Rules_*.asset.meta'):
        if f'guid: {guid}' not in meta.read_text(encoding='utf-8'): continue
        rules = meta.with_suffix('')
        text = rules.read_text(encoding='utf-8')
        for field, original in [('layoutTemplate', 'bucketLayout'), ('conveyorDirectionTemplate', 'conveyorDirections'), ('conveyorBucketCountTemplate', 'conveyorBucketCounts')]:
            value = re.search(r'^  ' + original + r': (.*)$', source, re.M)[1]
            text = re.sub(r'^  ' + field + r': .*$', '  ' + field + ': ' + value, text, flags=re.M)
        count = int(re.search(r'^  gridWidth: (\d+)', source, re.M)[1]) * int(re.search(r'^  gridHeight: (\d+)', source, re.M)[1])
        text = re.sub(r'^  maxGrainsPerBucket: .*$', f'  maxGrainsPerBucket: {(count + 9) // 10}', text, flags=re.M)
        rules.write_text(text, encoding='utf-8')
        break
print('Restored five original opening paintings and their layout templates.')
