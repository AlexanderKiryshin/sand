import hashlib
import json
import re
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
report = Path(__file__).resolve().parent
bundle = root / 'Builds/sand[18]-mirraSDK[5.1.31].zip'
target = report / 'build18'
with zipfile.ZipFile(bundle) as z:
    entries = [i for i in z.infolist() if not i.is_dir()]
    invalid = [i.filename for i in entries if re.search(r'\s|[А-Яа-яЁё]', i.filename)]
    for item in entries:
        out = (target / item.filename).resolve()
        if not out.is_relative_to(target.resolve()):
            raise ValueError(item.filename)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(z.read(item))
    result = {
        'artifact': str(bundle),
        'sha256': hashlib.sha256(bundle.read_bytes()).hexdigest(),
        'zip_bytes': bundle.stat().st_size,
        'unpacked_zip_bytes': sum(i.file_size for i in entries),
        'file_count': len(entries),
        'root_index_html': any(i.filename == 'index.html' for i in entries),
        'invalid_names': invalid,
        'files': [{'name': i.filename, 'bytes': i.file_size} for i in entries],
    }
    (report / 'bundle-inspection.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'files'}, ensure_ascii=False))
