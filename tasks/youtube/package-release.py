"""Validate the exported bundle and create an upload ZIP, never including project sources."""
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

root = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2]).resolve()
assert root.is_dir() and (root / 'index.html').is_file()
assert not target.exists(), f'Refusing to replace {target}'
files = sorted(p for p in root.rglob('*') if p.is_file())
assert 0 < len(files) <= 8000
for p in files:
    relative = p.relative_to(root)
    assert all(re.fullmatch(r'[A-Za-z0-9_.-]+', name) for name in relative.parts), relative
    assert p.stat().st_size < 30 * 1024 * 1024, relative
    assert p.suffix not in ['.cs', '.meta', '.pdb', '.mdb', '.asset', '.gz', '.br', '.unityweb'], relative
    if p.suffix == '.zip':
        with zipfile.ZipFile(p) as inner:
            assert inner.testzip() is None
            assert inner.namelist() == [p.stem], (relative, inner.namelist())
total = sum(p.stat().st_size for p in files)
assert total < 250 * 1024 * 1024
html = (root / 'index.html').read_text(encoding='utf-8')
assert '{{{' not in html and '#if' not in html
assert html.index('https://www.youtube.com/game_api/v1') < html.index('ytgame-lifecycle.js')
assert 'cdnjs' not in html and 'mirragames' not in html
assert len(list((root/'Build').glob('*.data.zip'))) == 1
assert len(list((root/'Build').glob('*.wasm.zip'))) == 1
assert (root/'lib/jszip.min.js').is_file()
assert (root/'lib/LICENSE.markdown').is_file()
for ref in re.findall(r"(?:src=\"|(?:dataUrl|codeUrl|frameworkUrl|loaderUrl):\s*')([^\"']+)", html):
    if ref.startswith('https://'):
        assert ref == 'https://www.youtube.com/game_api/v1'
    else:
        assert (root / ref).is_file(), ref
target.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for p in files:
        archive.write(p, p.relative_to(root).as_posix())
report = {
    'bundle': str(root), 'zip': str(target), 'zipBytes': target.stat().st_size,
    'bundleBytes': total, 'initialGameFilesBytesUpperBound': total,
    'initialTransferNote': 'Includes all exported files, excludes the platform SDK and HTTP overhead. Recheck after YouTube ingestion.',
    'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
    'files': [{'path': p.relative_to(root).as_posix(), 'bytes': p.stat().st_size,
               'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]
}
Path('tasks/youtube/bundle-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in report.items() if k != 'files'}, indent=2))
