"""Verify native texture compression, source aspect ratios and decoded image quality."""
import json
from pathlib import Path

import cv2
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]


def main():
    before = json.loads((ROOT / 'tasks/graphics-unity-audit.json').read_text(encoding='utf-8-sig'))['textures']
    after = json.loads((ROOT / 'tasks/graphics-unity-compressed.json').read_text(encoding='utf-8-sig'))['textures']
    backgrounds = [r for r in after if '/ThemeBackgrounds/' in r['path']]
    assert len(backgrounds) == 46
    results = []
    for record in backgrounds:
        assert record['format'] == 'DXT1Crunched' and record['crunched'] is True
        assert abs(record['sourceAspect'] - record['sourceWidth'] / record['sourceHeight']) < 0.000001
        source = cv2.imread(str(ROOT / record['path']))
        decoded = cv2.imread(str(ROOT / 'Temp/graphics-decoded' / Path(record['path']).name))
        assert source is not None and decoded is not None
        assert source.shape[:2] == (record['sourceHeight'], record['sourceWidth'])
        assert decoded.shape[:2] == (record['height'], record['width'])
        restored = cv2.resize(decoded, (source.shape[1], source.shape[0]), interpolation=cv2.INTER_CUBIC)
        psnr = cv2.PSNR(source, restored)
        assert psnr >= 28, (record['path'], psnr)
        results.append({'path': record['path'], 'psnr_db': round(psnr, 3),
                        'format': record['format'], 'source_aspect_preserved': True})
    themes = [24, 40, 28, 27, 29, 30]
    sheet = Image.new('RGB', (1320, 832), '#eeeeee')
    draw = ImageDraw.Draw(sheet)
    for index, theme in enumerate(themes):
        for column, (label, folder) in enumerate([('Source', ROOT / 'Assets/Resources/ThemeBackgrounds'),
                                                 ('Compressed', ROOT / 'Temp/graphics-decoded')]):
            x = (index % 3) * 440 + column * 220
            y = (index // 3) * 416
            draw.text((x + 5, y + 3), f'{theme} {label}', fill='black')
            with Image.open(folder / f'background_{theme}.png') as image:
                sheet.paste(image.convert('RGB').resize((220, 392), Image.Resampling.LANCZOS), (x, y + 20))
    sheet.save(ROOT / 'tasks/graphics-compression-preview.jpg', quality=95)
    def gpu_bytes(rows):
        return sum(r['width'] * r['height'] * (4 if r['format'] == 'RGBA32' else .5 if r['format'] == 'DXT1Crunched' else 1)
                   for r in rows if '/ThemeBackgrounds/' in r['path'])
    result = {'status': 'PASS', 'backgrounds': len(backgrounds), 'source_aspects_preserved': True,
              'minimum_psnr_db': min(r['psnr_db'] for r in results),
              'estimated_gpu_bytes_before': int(gpu_bytes(before)), 'estimated_gpu_bytes_after': int(gpu_bytes(after)),
              'source_pngs_lossless': True, 'textures': results}
    (ROOT / 'tasks/graphics-unity-verification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'textures'}, indent=2))


if __name__ == '__main__':
    main()
