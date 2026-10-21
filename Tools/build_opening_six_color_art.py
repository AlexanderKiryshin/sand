"""Convert the ten authored illustrations to six-color 64x64 level data.

Default: prepare candidates and PNGs. --apply: integrate them. --verify: audit
the integrated levels against the saved candidates and untouched authored fields.
"""
from pathlib import Path
from collections import Counter
import argparse
import hashlib
import json
import re
import shutil
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from level_storage import encode_cells, read_cells, preview_image

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'tasks/opening-six-colors'
ART = ROOT / 'Assets/Images/OpeningSixColors'
CAMPAIGN = ROOT / 'Assets/Levels/Campaign100'
MANIFEST = json.loads((WORK / 'generation.json').read_text(encoding='utf-8'))


def lab(rgb):
    rgb = np.asarray(rgb, dtype=np.float32) / 255
    linear = np.where(rgb > .04045, ((rgb + .055) / 1.055) ** 2.4, rgb / 12.92)
    xyz = linear @ np.array([[.4124564, .3575761, .1804375],
                             [.2126729, .7151522, .0721750],
                             [.0193339, .1191920, .9503041]], dtype=np.float32).T
    xyz /= np.array([.95047, 1, 1.08883], dtype=np.float32)
    f = np.where(xyz > .008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]),
                     200 * (f[..., 1] - f[..., 2])], axis=-1)


def convert(path, palette):
    # Same CIELAB nearest-color vote as ColorQuantizer's region sampler.
    rgb = np.asarray(Image.open(path).convert('RGB'))
    height, width = rgb.shape[:2]
    snapped = np.empty((height, width), dtype=np.uint8)
    palette_lab = lab(palette)
    for y in range(0, height, 64):
        delta = lab(rgb[y:y + 64])[:, :, None, :] - palette_lab
        snapped[y:y + 64] = np.argmin(np.sum(delta * delta, axis=-1), axis=-1)
    cells = bytearray()
    for y in range(64):
        for x in range(64):
            region = snapped[round(y * height / 64):round((y + 1) * height / 64),
                             round(x * width / 64):round((x + 1) * width / 64)]
            cells.append(int(np.argmax(np.bincount(region.ravel(), minlength=6))) + 1)
    return bytes(cells)


def replace_art(text, palette, cells):
    block = '  palette:\n' + ''.join('  - serializedVersion: 2\n    rgba: ' +
             str(int.from_bytes(bytes(color) + b'\xff', 'little')) + '\n' for color in palette)
    text = re.sub(r'^  palette:\n.*?(?=^  \w+:)', lambda _: block, text, flags=re.M | re.S)
    text = re.sub(r'^  encodedCells:.*$', '  encodedCells: ' + encode_cells(cells), text, flags=re.M)
    block = '  buckets:\n' + ''.join(f'  - colorIndex: {i}\n    count: 1\n' for i in sorted(set(cells)))
    return re.sub(r'^  buckets:\n.*?(?=^  \w+:)', lambda _: block, text, flags=re.M | re.S)


def protected_fields(text):
    for field in ['palette', 'buckets']:
        text = re.sub(r'^  ' + field + r':\n.*?(?=^  \w+:)', '', text, flags=re.M | re.S)
    return re.sub(r'^  encodedCells:.*\n', '', text, flags=re.M)


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'sand/opening-six-colors/' + str(path.relative_to(ROOT))).hex


def save_meta(path, folder=False):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return
    if folder:
        text = f'fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
    else:
        template = next((ROOT / 'Assets/Images').glob('*.png.meta')).read_text(encoding='utf-8')
        text = re.sub(r'^guid: .*$', 'guid: ' + guid(path), template, flags=re.M)
        for field, value in [('enableMipMap', 0), ('filterMode', 0), ('textureCompression', 0),
                             ('textureType', 0), ('spriteMode', 0), ('maxTextureSize', 64)]:
            text = re.sub(r'^(\s*' + field + r':) .*$', lambda m: m[1] + ' ' + str(value), text, flags=re.M)
    meta.write_text(text, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    for name in ['originals', 'candidates', 'backup']:
        (WORK / name).mkdir(exist_ok=True)
    ART.mkdir(parents=True, exist_ok=True)
    save_meta(ART, True)
    global_text = (ROOT / 'Assets/Settings/SandColorPalette.asset').read_text(encoding='utf-8')
    global_colors = {tuple(int(n).to_bytes(4, 'little')[:3]) for n in re.findall(r'rgba: (\d+)', global_text)}
    db_path = ROOT / 'Assets/Levels/LevelDatabase.asset'
    db_backup = WORK / 'backup/LevelDatabase.asset'
    if not db_backup.exists():
        shutil.copy2(db_path, db_backup)
    assert db_path.read_bytes() == db_backup.read_bytes(), 'Campaign order changed'
    sheet = Image.new('RGB', (1360, 630), '#F8FAFC')
    draw = ImageDraw.Draw(sheet)
    font_path = Path('C:/Windows/Fonts/arial.ttf')
    font = ImageFont.truetype(str(font_path), 17) if font_path.exists() else ImageFont.load_default()
    titles = ['Лиса', 'Пагода', 'Сокол', 'Жираф', 'Морская волна', 'Белый волк',
              'Тропики', 'Роза', 'Цветущий дворик', 'Лимонад']
    records = []
    for item in MANIFEST['levels']:
        n = item['number']
        name = f'Level_{n:03}.asset'
        path = CAMPAIGN / name
        rules_path = CAMPAIGN / f'Rules_{n:03}.asset'
        for source in [path, rules_path]:
            backup = WORK / 'backup' / source.name
            if not backup.exists():
                shutil.copy2(source, backup)
        original = WORK / 'originals' / f'Level_{n:03}_{item["subject"]}.png'
        if not original.exists():
            shutil.copy2(item['generatedPath'], original)
        palette = [tuple(bytes.fromhex(c[1:])) for c in item['palette']]
        assert len(palette) == len(set(palette)) == 6 and set(palette) <= global_colors
        cells = convert(original, palette)
        assert len(cells) == 4096 and set(cells) == set(range(1, 7))
        assert read_cells('  encodedCells: ' + encode_cells(cells)) == cells
        old = (WORK / 'backup' / name).read_text(encoding='utf-8-sig')
        candidate = replace_art(old, palette, cells)
        assert protected_fields(candidate) == protected_fields(old), 'Non-art level settings changed'
        (WORK / 'candidates' / name).write_text(candidate, encoding='utf-8')
        old_rules = (WORK / 'backup' / rules_path.name).read_text(encoding='utf-8-sig')
        rules = re.sub(r'^  defaultColorCount:.*$', '  defaultColorCount: 6', old_rules, flags=re.M)
        rules = re.sub(r'^  defaultAutoColorCount:.*$', '  defaultAutoColorCount: 0', rules, flags=re.M)
        if args.apply:
            assert path.read_text(encoding='utf-8-sig') in [old, candidate], 'Concurrent level edit'
            assert rules_path.read_text(encoding='utf-8-sig') in [old_rules, rules], 'Concurrent rules edit'
            path.write_text(candidate, encoding='utf-8')
            rules_path.write_text(rules, encoding='utf-8')
        if args.apply or args.verify:
            assert path.read_text(encoding='utf-8-sig') == candidate, f'Level {n} differs from candidate'
            assert rules_path.read_text(encoding='utf-8-sig') == rules
            assert re.search(r'guid: (\w+)', Path(str(path) + '.meta').read_text())[1] in db_path.read_text()
            assert re.search(r'guid: (\w+)', Path(str(rules_path) + '.meta').read_text())[1] in candidate
        # Export the exact level cells as the canonical six-color image.
        image = Image.new('RGB', (64, 64))
        image.putdata([palette[c - 1] for c in cells])
        image_path = ART / f'Level_{n:03}_{item["subject"]}.png'
        image.save(image_path, optimize=True)
        save_meta(image_path)
        assert len(Image.open(image_path).getcolors()) == 6
        x = ((n - 1) % 5) * 272 + 8
        y = ((n - 1) // 5) * 315 + 6
        sheet.paste(image.resize((256, 256), Image.Resampling.NEAREST), (x, y))
        draw.text((x, y + 261), f'{n:02}  {titles[n - 1]}', fill='#173E48', font=font)
        for i, color in enumerate(palette):
            draw.rectangle((x + i * 42, y + 287, x + i * 42 + 37, y + 296), fill=color)
        rendered = preview_image(candidate)
        rendered.resize((512, 512), Image.Resampling.NEAREST).save(WORK / f'runtime-preview-{n:03}.png')
        values = np.array([palette[c - 1] for c in cells]) / 255
        brightness = values @ np.array([.2126, .7152, .0722])
        record = dict(number=n, subject=item['subject'], colors=item['palette'],
                      usedColors=len(set(cells)), counts=dict(Counter(cells)),
                      meanBrightness=round(float(brightness.mean()), 3),
                      darkAreaPercent=round(float(np.mean(brightness < .25) * 100), 2),
                      image=str(image_path.relative_to(ROOT)).replace('\\', '/'),
                      cellsSha256=hashlib.sha256(cells).hexdigest())
        records.append(record)
        print(f'{n:02}: 64x64, 6 colors, dark area {record["darkAreaPercent"]}%, brightness {record["meanBrightness"]}')
    sheet.save(WORK / 'contact-sheet.png')
    (WORK / 'validation.json').write_text(json.dumps(records, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('PASS: ten six-color illustrations; all cell indices valid; codec round trip; authored mechanics preserved.' +
          (' Integrated levels and rules verified.' if args.apply or args.verify else ' Candidates ready.'))


if __name__ == '__main__':
    main()
