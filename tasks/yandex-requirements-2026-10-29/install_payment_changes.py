from pathlib import Path
import shutil
import subprocess

root = Path('G:/codex/sand-yandex-payments/sand').resolve()
stage = Path('E:/myProjects/sand/Temp/YandexPaymentsChanges').resolve()
branch = subprocess.check_output(['git', '-c', f'safe.directory={root.as_posix()}', 'branch', '--show-current'], cwd=root, text=True).strip()
assert branch == 'codex/yandex-payments', branch
count = 0
for source in stage.rglob('*'):
    if not source.is_file():
        continue
    target = (root / source.relative_to(stage)).resolve()
    assert target.is_relative_to(root)
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or target.read_bytes() != source.read_bytes():
        shutil.copy2(source, target)
        count += 1
print(f'Installed {count} changed files into {branch}: {root}')
