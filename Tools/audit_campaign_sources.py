"""Find visually similar source paintings before importing native Unity levels."""
from pathlib import Path
import json
import csv
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
matrix = np.cos(np.pi / 32 * (np.arange(32)[None, :] + .5) * np.arange(8)[:, None])
entries, hashes, previews = [], [], []
for line in (root/'tasks/campaign1500-art.jsonl').read_text(encoding='utf-8').splitlines():
    batch = json.loads(line)
    with Image.open(root/'Assets/Images/Campaign1500'/f'{batch["name"]}.png') as atlas:
        for tile, title in enumerate(batch['scenes']):
            x, y = tile % 5, tile // 5
            picture = atlas.crop((round(x*atlas.width/5), round(y*atlas.height/5), round((x+1)*atlas.width/5), round((y+1)*atlas.height/5))).convert('RGB')
            coeff = matrix @ np.asarray(picture.resize((32,32)).convert('L'), dtype=float) @ matrix.T
            bits = coeff.flatten()[1:] > np.median(coeff.flatten()[1:])
            hashes.append(sum(int(bit) << j for j, bit in enumerate(bits)))
            entries.append((batch['name'], tile, title))
            previews.append(picture.resize((160,160)))
pairs = []
for i in range(len(hashes)):
    for j in range(i):
        distance = (hashes[i] ^ hashes[j]).bit_count()
        if distance <= 10: pairs.append((distance,j,i))
pairs.sort()
with (root/'tasks/campaign-source-similarity.csv').open('w',encoding='utf-8-sig',newline='') as out:
    writer = csv.writer(out); writer.writerow(['distance','sheet_a','tile_a','subject_a','sheet_b','tile_b','subject_b'])
    for d,j,i in pairs: writer.writerow([d,*entries[j],*entries[i]])
if pairs:
    canvas = Image.new('RGB',(660,len(pairs)*190),'#eef1e6'); draw=ImageDraw.Draw(canvas)
    for row,(d,j,i) in enumerate(pairs):
        for col,idx in enumerate((j,i)):
            x=col*330; y=row*190
            canvas.paste(previews[idx],(x,y))
            draw.text((x,y+164),f'{entries[idx][0]} tile {entries[idx][1]+1}, d={d}',fill='#23424a')
    canvas.save(root/'tasks/previews/campaign1500/source-similarity.png')
print(f'{len(entries)} new paintings; {len(pairs)} close source pairs.')
