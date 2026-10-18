"""Audit actual Unity level pixels, not source thumbnails."""
from pathlib import Path
import csv
import re
import numpy as np
from PIL import Image, ImageDraw
from level_storage import preview_image

root = Path(__file__).resolve().parents[1]
db = (root/'Assets/Levels/LevelDatabase.asset').read_text(encoding='utf-8')
guids = re.findall(r'^  - \{fileID: 11400000, guid: ([0-9a-f]+), type: 2\}', db, re.M)
paths = {}
for meta in (root/'Assets/Levels').rglob('*.asset.meta'):
    guid = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8'), re.M)
    if guid: paths[guid[1]] = meta.with_suffix('')

matrix = np.cos(np.pi / 32 * (np.arange(32)[None,:] + .5) * np.arange(8)[:,None])
images, entries, hashes = [], [], []
for i, guid in enumerate(guids):
    text = paths[guid].read_text(encoding='utf-8')
    image = preview_image(text).convert('RGB')
    gray = np.asarray(image.resize((32,32), Image.Resampling.LANCZOS).convert('L'), dtype=float)
    coeff = matrix @ gray @ matrix.T
    bits = (coeff.flatten()[1:] > np.median(coeff.flatten()[1:])).tolist()
    value = sum(int(bit) << j for j,bit in enumerate(bits))
    hashes.append(value); images.append(image)
    seasonal = re.search(r'^  seasonalNumber: (\d+)', text, re.M)
    winter_number = int(seasonal[1]) if seasonal else 0
    title = re.search(r'^  displayTitle: (.*)', text, re.M)
    entries.append((i+1, 'winter' if winter_number else 'ordinary', winter_number or i+1, title[1] if title else '', str(paths[guid])))

output = root/'tasks/previews/campaign1500'
output.mkdir(parents=True, exist_ok=True)
with (root/'tasks/campaign1500-art-index.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.writer(f);writer.writerow(['database_number','campaign','level','title','asset']);writer.writerows(entries)

pairs=[]
for i in range(len(hashes)):
    for j in range(i):
        distance=(hashes[i]^hashes[j]).bit_count()
        if distance<=7: pairs.append((distance,j,i))
pairs.sort()
with (root/'tasks/campaign1500-similar-art.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.writer(f);writer.writerow(['distance','level_a','title_a','level_b','title_b'])
    for d,j,i in pairs:writer.writerow([d,entries[j][0],entries[j][3],entries[i][0],entries[i][3]])

def sheet(indices, filename, cols=5):
    canvas=Image.new('RGB',(cols*164,((len(indices)+cols-1)//cols)*192),'#eef1e6');draw=ImageDraw.Draw(canvas)
    for k,i in enumerate(indices):
        x=k%cols*164;y=k//cols*192
        canvas.paste(images[i].resize((160,160),Image.Resampling.NEAREST),(x+2,y+2))
        draw.text((x+5,y+164),f'{entries[i][1]} {entries[i][2]}',fill='#23424a')
    canvas.save(output/filename)

sheet(list(range(min(5,len(images)))), 'opening.png')
for start in range(0,len(images),100):sheet(list(range(start,min(start+100,len(images)))),f'catalog_{start+1:04}.png',10)
if pairs:sheet([i for _,a,b in pairs for i in (a,b)],'similarity-review.png',4)
print(f'{len(images)} paintings rendered; {len(pairs)} close visual pairs need human inspection.')
