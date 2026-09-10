"""Split the eight generated 5x5 atlases into the 200 authored source paintings."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'tasks/campaign-expansion-art.json').read_text(encoding='utf-8-sig'))
output = root / 'Assets/Images/CampaignExpansion/Paintings'
output.mkdir(parents=True, exist_ok=True)
sheet = Image.new('RGB', (1400, 20 * 156), '#192937')
draw = ImageDraw.Draw(sheet)
hashes = set()
records = []
for atlas in manifest['atlases']:
    with Image.open(root / 'Assets/Images/CampaignExpansion' / (atlas['name'] + '.png')) as opened:
        opened.load()
        picture = opened.convert('RGB')
    width, height = picture.size
    for tile, scene in enumerate(atlas['scenes']):
        x, y = tile % 5, tile // 5
        painting = picture.crop((round(x * width / 5), round(y * height / 5),
                                 round((x + 1) * width / 5), round((y + 1) * height / 5)))
        digest = hashlib.sha256(painting.tobytes()).hexdigest()
        assert digest not in hashes, scene
        hashes.add(digest)
        path = output / ('Painting_%03d.png' % scene['number'])
        painting.save(path, optimize=True)
        i = scene['number'] - 101
        sheet.paste(painting.resize((136, 136)), ((i % 10) * 140, (i // 10) * 156))
        draw.text(((i % 10) * 140 + 3, (i // 10) * 156 + 137), str(scene['number']) + ' ' + scene['theme'], fill='white')
        records.append({**scene, 'source': str(path.relative_to(root)).replace('\\', '/'), 'sha256': digest})
assert len(records) == 200
sheet.save(root / 'tasks/expansion-200-contact-sheet.jpg', quality=90)
(root / 'tasks/expansion-paintings.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print('PASS: 8 readable atlases, 200 distinct PNG paintings, contact sheet.')
