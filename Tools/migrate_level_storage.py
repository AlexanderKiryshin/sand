"""Migrate every LevelData asset without touching authored fields or source artwork.

Dry run by default. --apply keeps originals under Temp/level-storage-backup,
then removes unreferenced generated *_preview.png files inside Assets/Levels.
--verify checks every migrated level against that backup.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
from level_storage import encode_cells, decode_cells, read_cells, other_fields

ROOT = Path(__file__).resolve().parents[1]
BACKUP = ROOT / 'Temp/level-storage-backup'
LEVEL_SCRIPT_GUID = 'a1b2c3d4e5f6789012345678abcdef01'


def read(path):
    return path.read_text(encoding='utf-8-sig')


def verify():
    manifest = json.loads(read(BACKUP / 'manifest.json'))
    for entry in manifest['levels']:
        path = ROOT / entry['path']
        before, after = read(BACKUP / entry['path']), read(path)
        assert read_cells(after) == read_cells(before), entry['path']
        assert other_fields(after) == other_fields(before), entry['path']
        assert not re.search(r'^  (?:cells|previewTexture):', after, re.M), entry['path']
        assert '  encodedCells: S1:' in after, entry['path']
    for relative in manifest['removed_previews']:
        assert not (ROOT / relative).exists(), relative
        assert (BACKUP / relative).exists(), relative
    print(f"PASS: {len(manifest['levels'])} levels preserve every cell, palette and authored field.")


def migrate(apply):
    plans = {}
    stats = {'levels': [], 'removed_previews': [], 'raw_cell_bytes': 0, 'encoded_text_bytes': 0,
             'asset_bytes_before': 0, 'asset_bytes_after': 0, 'preview_png_bytes_removed': 0}
    for path in sorted((ROOT / 'Assets').rglob('*.asset')):
        text = read(path)
        if f'guid: {LEVEL_SCRIPT_GUID},' not in text:
            continue
        cells = read_cells(text)
        width = int(re.search(r'^  gridWidth: (\d+)', text, re.M)[1])
        height = int(re.search(r'^  gridHeight: (\d+)', text, re.M)[1])
        assert len(cells) == width * height, str(path)
        encoded = encode_cells(cells)
        assert decode_cells(encoded) == cells
        converted = re.sub(r'^  (?:cells|legacyCells|encodedCells):[^\n]*\n?', '', text, flags=re.M)
        converted = re.sub(r'^  previewTexture:[^\n]*\n?', '', converted, flags=re.M)
        converted = converted.replace('  subGrainsPerAxis:', f'  encodedCells: {encoded}\n  subGrainsPerAxis:', 1)
        assert other_fields(converted) == other_fields(text)
        relative = path.relative_to(ROOT).as_posix()
        stats['levels'].append({'path': relative, 'cells_sha256': hashlib.sha256(cells).hexdigest()})
        stats['raw_cell_bytes'] += len(cells)
        stats['encoded_text_bytes'] += len(encoded)
        stats['asset_bytes_before'] += path.stat().st_size
        newline = '\r\n' if b'\r\n' in path.read_bytes() else '\n'
        payload = converted.replace('\n', newline).encode('utf-8')
        stats['asset_bytes_after'] += len(payload)
        plans[path] = payload

    # Inspect remaining serialized references before removing any generated texture.
    references = set()
    for path in (ROOT / 'Assets').rglob('*'):
        if not path.is_file() or path.suffix not in {'.asset', '.unity', '.prefab', '.mat', '.controller', '.overrideController', '.playable', '.meta'}:
            continue
        text = plans[path].decode('utf-8') if path in plans else path.read_text(encoding='utf-8-sig', errors='replace')
        references.update(re.findall(r'guid: ([0-9a-f]{32})', text) if path.suffix != '.meta'
                          else re.findall(r'guid: ([0-9a-f]{32})', re.sub(r'^guid:.*$', '', text, flags=re.M)))
    removals = []
    for path in (ROOT / 'Assets/Levels').rglob('*_preview.png'):
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            continue
        guid = re.search(r'^guid: (\w+)', read(meta), re.M)[1]
        if guid in references:
            continue
        assert path.resolve().is_relative_to((ROOT / 'Assets/Levels').resolve())
        removals.extend([path, meta])
        stats['removed_previews'].append(path.relative_to(ROOT).as_posix())
        stats['preview_png_bytes_removed'] += path.stat().st_size

    if apply:
        if (BACKUP / 'manifest.json').exists():
            raise SystemExit('Migration backup already exists. Use --verify; do not overwrite the original snapshot.')
        # Finish every backup before replacing or removing any asset.
        for path in list(plans) + removals:
            destination = BACKUP / path.relative_to(ROOT)
            assert destination.resolve().is_relative_to(BACKUP.resolve())
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, destination)
        (BACKUP / 'manifest.json').write_text(json.dumps(stats, indent=2) + '\n', encoding='utf-8')
        for path, payload in plans.items():
            path.write_bytes(payload)
        for path in removals:
            assert path.resolve().is_relative_to((ROOT / 'Assets/Levels').resolve())
            path.unlink()
        verify()
        report = ROOT / 'tasks/level-storage-migration.json'
        report.parent.mkdir(exist_ok=True)
        report.write_text(json.dumps(stats, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({**stats, 'levels': len(stats['levels']), 'removed_previews': len(stats['removed_previews']), 'applied': apply}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--apply', action='store_true')
    group.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    verify() if args.verify else migrate(args.apply)
