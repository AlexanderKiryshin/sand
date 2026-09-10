"""Make review sheets of the actual quantized pictures and authored board templates."""
from pathlib import Path
import re
from PIL import Image, ImageDraw
from level_storage import preview_image
root = Path(__file__).resolve().parents[1]
source = root / 'Assets/Levels/Campaign100'
for page in range(3):
    sheet = Image.new('RGB', (1600, 2060), '#e8eff0')
    draw = ImageDraw.Draw(sheet)
    for tile in range(100):
        level = page * 100 + tile + 1
        text = (source / f'Level_{level:03d}.asset').read_text(encoding='utf-8-sig')
        layout = bytes.fromhex(re.search(r'^  bucketLayout: (.+)$', text, re.M)[1])
        x, y = tile % 10 * 160, tile // 10 * 206
        with preview_image(text) as painting:
            sheet.paste(painting.convert('RGB').resize((154, 140), Image.Resampling.NEAREST), (x, y))
        for cell, value in enumerate(layout):
            cx, cy = x + 104 + cell % 5 * 9, y + 146 + cell // 5 * 9
            draw.rectangle((cx, cy, cx + 7, cy + 7), fill='#355763' if value == 255 else '#f18e32' if value == 254 else '#ffffff')
        theme = re.search(r'^  artTheme: (.*)$', text, re.M)
        draw.text((x + 3, y + 148), f'{level:03d}', fill='#132b37')
        draw.text((x + 3, y + 163), theme[1] if theme else 'preserved', fill='#132b37')
    sheet.save(root / f'tasks/campaign-review-{page+1}.jpg', quality=92)
print('Three review sheets: 300 quantized paintings and board geometries.')
