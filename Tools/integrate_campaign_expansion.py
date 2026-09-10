"""Copy verified campaign output from the disposable Unity project into the workspace."""
from pathlib import Path
import hashlib
import json
import re
import shutil

root = Path(__file__).resolve().parents[1]
checked = root / 'Temp/ValidationProject'
backup = root / 'Temp/campaign300-backup'
earlier_log = (root / 'Temp/expansion-release.log').read_text(encoding='utf-8-sig')
physics_log = (root / 'Temp/expansion-access2.log').read_text(encoding='utf-8-sig')
assert 'CAMPAIGN_ACCESS_FIX_PASS' in physics_log, 'Validation of the final runtime must pass before integration.'
assert (root / 'Temp/expansion-access2-exit.txt').read_text().strip() == '0'
passed = dict((int(level), int(ticks)) for level, ticks in re.findall(r'SIMULATION_LEVEL_PASS: (\d+), (\d+) ticks', earlier_log + physics_log))
rechecked = {int(level) for level in re.findall(r'SIMULATION_LEVEL_PASS: (\d+),', physics_log)}
expected = {1,3,12,14,16,18,20,22,24,26,28,38,48,50,58,68,80,90,100,
            101,102,103,104,105,106,107,108,109,110,111,112,125,150,175,200,225,250,275,300}
assert set(passed) == expected, 'All 39 simulation samples must pass.'
for index in expected:
    serialized = (checked / f'Assets/Levels/Campaign100/Level_{index:03d}.asset').read_text(encoding='utf-8-sig')
    if index >= 112 or ('  campaignTemplateVersion: 1' in serialized and '  keyGates: 1' in serialized):
        assert index in rechecked, 'A level affected by the final access fix must be rechecked: ' + str(index)
campaign = 'Assets/Levels/Campaign100'
assert (root / 'Assets/Levels/LevelDatabase.asset').read_bytes() == (backup / 'LevelDatabase.asset').read_bytes(), 'Concurrent database edit.'
protected = []
for index in range(1, 11):
    for prefix, suffix in [('Level_', '.asset'), ('Rules_', '.asset'), ('Level_', '_preview.png')]:
        for meta in ['', '.meta']:
            name = f'{prefix}{index:03d}{suffix}{meta}'
            original = backup / name
            if not original.exists():
                continue
            current = root / campaign / name
            assert current.read_bytes() == original.read_bytes(), 'First ten changed: ' + name
            protected.append(current)
before = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
for index in range(11, 101):
    for prefix, suffix in [('Level_', '.asset'), ('Rules_', '.asset'), ('Level_', '_preview.png')]:
        for meta in ['', '.meta']:
            name = f'{prefix}{index:03d}{suffix}{meta}'
            assert (root / campaign / name).read_bytes() == (backup / name).read_bytes(), 'Concurrent level edit: ' + name

def copy(source):
    target = root / source.relative_to(checked)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

for index in range(11, 301):
    for prefix, suffix in [('Level_', '.asset'), ('Rules_', '.asset'), ('Level_', '_preview.png')]:
        for meta in ['', '.meta']:
            copy(checked / campaign / f'{prefix}{index:03d}{suffix}{meta}')
for source in (checked / 'Assets/Levels/CampaignSource100').rglob('*'):
    if source.is_file(): copy(source)
copy(checked / 'Assets/Levels/CampaignSource100.meta')
copy(checked / 'Assets/Levels/LevelDatabase.asset')
for path in ['Assets/PluginYourGames/Resources/SettingsYG2.asset', 'Assets/Resources/GameAnalytics/Settings.asset']:
    target = root / path
    original = target.read_text(encoding='utf-8-sig') if target.exists() else ''
    incoming = (checked / path).read_text(encoding='utf-8-sig')
    if 'SettingsYG2' in path and original:
        counter = re.search(r'^    metricaCounterID: (\d+)', original, re.M)
        for section in ['Basic', 'Metrica']:
            pattern = r'^  ' + section + r':\n(?:^    .*\n)*'
            block = re.search(pattern, incoming, re.M)[0]
            if re.search(pattern, original, re.M): original = re.sub(pattern, lambda _: block, original, flags=re.M)
            else: original += '\n' + block
        if counter: original = re.sub(r'(^    metricaCounterID: )\d+', lambda m: m[1] + counter[1], original, count=1, flags=re.M)
        target.write_text(original, encoding='utf-8')
    elif original and re.search(r'^  - [\w-]{32,40}$', original, re.M):
        # Preserve credentials entered while the isolated checks were running.
        pass
    else: copy(checked / path)
    if not (root / (path + '.meta')).exists(): copy(checked / (path + '.meta'))
for directory in ['Assets/Scripts', 'Assets/Editor', 'Assets/Resources', 'Assets/Images/CampaignExpansion']:
    for source in (checked / directory).rglob('*.meta'):
        if not (root / source.relative_to(checked)).exists(): copy(source)
for name in ['campaign300-audit.md', 'campaign-random-mechanics.md', 'campaign-simulation.md',
             'crate-validation.md', 'analytics-validation.md', 'web-analytics-settings-status.txt']:
    copy(checked / 'tasks' / name)
report = ['# Sand simulation checks', '', 'All 39 samples completed with the actual sand simulator, collector, conveyor and inventory; one grain per cell, no paid boosters. Results combine expansion-release and expansion-access2. Every authored key board affected by the final access fix was rerun in expansion-access2, together with all late samples. Recall overflow from a temporarily occupied access aisle has a separate regression check. The other runtime paths are unchanged. Bounded deterministic strategies choose different legal orders for the mechanic combinations; each PASS requires zero sand and zero remaining cups. This is a functional check, not a device performance measurement.', '', 'Level | Ticks | Sand left | Buckets left', '--- | --- | --- | ---']
report.extend(f'{level} | {ticks} | 0 | 0' for level, ticks in sorted(passed.items()))
(root / 'tasks/campaign-simulation.md').write_text('\n'.join(report) + '\n', encoding='utf-8')

# YG supplies Metrica; the existing Mirra SDK owns the platform bootstrap.
settings = root / 'ProjectSettings/ProjectSettings.asset'
text = settings.read_text(encoding='utf-8-sig')
text = text.replace(';YandexGamesPlatform_yg', '').replace('YandexGamesPlatform_yg;', '')
defines = re.search(r'^  scriptingDefineSymbols:\n(?:^    .*\n)*', text, re.M)[0]
for target in ['Standalone', 'WebGL']:
    pattern = r'^    ' + target + r': (.*)$'
    existing = re.search(pattern, defines, re.M)
    values = existing[1].split(';') if existing else []
    values = [v for v in values if v and v != 'YandexGamesPlatform_yg']
    for symbol in ['PLUGIN_YG_2', 'Metrica_yg', 'TMP_YG2']:
        if symbol not in values: values.append(symbol)
    line = '    ' + target + ': ' + ';'.join(values)
    if existing: defines = re.sub(pattern, lambda _: line, defines, flags=re.M)
    else: defines += line + '\n'
text = re.sub(r'^  scriptingDefineSymbols:\n(?:^    .*\n)*', lambda _: defines, text, flags=re.M)
settings.write_text(text, encoding='utf-8')
lock = root / 'Packages/packages-lock.json'
resolved = json.loads(lock.read_text(encoding='utf-8-sig'))
validated = json.loads((checked / 'Packages/packages-lock.json').read_text(encoding='utf-8-sig'))
resolved['dependencies']['com.gameanalytics.sdk'] = validated['dependencies']['com.gameanalytics.sdk']
lock.write_text(json.dumps(resolved, indent=2) + '\n', encoding='utf-8')
assert before == {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
(root / 'tasks/first-ten-preservation.json').write_text(json.dumps(before, indent=2), encoding='utf-8')
print(f'INTEGRATED: levels 11-300, source art backup, settings, reports. {len(protected)} first-ten files byte-identical.')
