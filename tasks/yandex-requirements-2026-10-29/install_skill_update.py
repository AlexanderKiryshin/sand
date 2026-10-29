from pathlib import Path
import hashlib
import json
import shutil

stage = Path('E:/myProjects/sand/Temp/YandexRequirementsSkill/web-platform-porting')
target = Path('D:/CodexData/.codex/skills/web-platform-porting')
hashes = json.loads((stage.parent / 'original-hashes.json').read_text(encoding='utf-8'))
for relative, expected in hashes.items():
    current = hashlib.sha256((target / relative).read_bytes()).hexdigest()
    assert current == expected, f'Skill changed concurrently: {relative}'
reference = 'references/yandex-requirements-audit.md'
assert not (target / reference).exists(), 'Reference already exists; inspect before overwriting'
for relative in list(hashes) + [reference]:
    shutil.copy2(stage / relative, target / relative)
    assert (target / relative).read_bytes() == (stage / relative).read_bytes()
print('Installed and verified three skill files.')
