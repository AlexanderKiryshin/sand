"""Copy the pre-redesign opening paintings to their requested campaign slots."""
from pathlib import Path
import hashlib
import json
import re
import shutil

from PIL import Image, ImageDraw
from level_storage import read_cells, preview_image

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'tasks/relocated-opening-art'
CAMPAIGN = ROOT / 'Assets/Levels/Campaign100'
DESTINATIONS = [11, 17, 20, 23, 32, 58, 78, 83, 117, 122]
ART_FIELDS = ['palette', 'encodedCells', 'buckets']


def field(text, name):
    pattern = r'^  ' + name + r':[^\n]*\n(?:^(?!  \w+:).*(?:\n|$))*'
    result = re.search(pattern, text, re.M)
    if result is None:
        raise ValueError('Missing field: ' + name)
    return result[0]


def non_art(text):
    for name in ART_FIELDS:
        text = text.replace(field(text, name), '', 1)
    return text


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    (OUTPUT / 'backup').mkdir(parents=True, exist_ok=True)
    all_paths = list((ROOT / 'Assets/Levels').rglob('*.asset'))
    all_paths += list((ROOT / 'Assets/Levels').rglob('*.meta'))
    before = {p: sha(p) for p in all_paths}
    sheet = Image.new('RGB', (1000, 440), '#F8FAFC')
    draw = ImageDraw.Draw(sheet)
    records = []
    for source_number, target_number in enumerate(DESTINATIONS, 1):
        source_path = ROOT / f'tasks/opening-six-colors/backup/Level_{source_number:03}.asset'
        target_path = CAMPAIGN / f'Level_{target_number:03}.asset'
        source = source_path.read_text(encoding='utf-8-sig')
        current = target_path.read_text(encoding='utf-8-sig')
        backup_path = OUTPUT / 'backup' / target_path.name
        if not backup_path.exists():
            shutil.copy2(target_path, backup_path)
        original_target = backup_path.read_text(encoding='utf-8-sig')
        for dimension in ['gridWidth', 'gridHeight']:
            assert field(source, dimension) == field(current, dimension), 'Grid size mismatch'
        updated = original_target
        for name in ART_FIELDS:
            updated = updated.replace(field(updated, name), field(source, name), 1)
        assert current in [original_target, updated], 'Concurrent target edit'
        assert non_art(updated) == non_art(original_target), 'Destination mechanics changed'
        target_path.write_text(updated, encoding='utf-8')
        saved = target_path.read_text(encoding='utf-8-sig')
        assert all(field(saved, name) == field(source, name) for name in ART_FIELDS)
        assert read_cells(saved) == read_cells(source), 'Painting changed during transfer'
        assert preview_image(saved).tobytes() == preview_image(source).tobytes()
        x = (source_number - 1) % 5 * 200
        y = (source_number - 1) // 5 * 220
        sheet.paste(preview_image(saved).convert('RGB').resize((192, 192), Image.Resampling.NEAREST), (x, y))
        draw.text((x + 4, y + 196), f'{source_number} -> {target_number}', fill='#173E48')
        records.append(dict(source=source_number, destination=target_number,
                            sourcePath=str(source_path.relative_to(ROOT)),
                            destinationPath=str(target_path.relative_to(ROOT)),
                            cellsSha256=hashlib.sha256(read_cells(saved)).hexdigest()))
        print(f'PASS: original {source_number} -> {target_number}; exact palette/cells; destination mechanics preserved')
    changed = {CAMPAIGN / f'Level_{n:03}.asset' for n in DESTINATIONS}
    assert all(sha(p) == digest for p, digest in before.items() if p not in changed), 'Unrelated level asset changed'
    sheet.save(OUTPUT / 'contact-sheet.png')
    (OUTPUT / 'mapping.json').write_text(json.dumps(records, indent=2) + '\n', encoding='utf-8')
    print('PASS: only the ten requested level assets changed; opening levels, rules, metadata and campaign order preserved.')


if __name__ == '__main__':
    main()
