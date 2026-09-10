"""Verify the promoted campaign, its references, artwork and preserved opening levels."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
from PIL import Image
from level_storage import read_cells, other_fields, preview_image

root = Path(__file__).resolve().parents[1]
campaign = root / 'Assets/Levels/Campaign100'
backup = root / 'Temp/campaign300-backup'

def read(path):
    return path.read_text(encoding='utf-8-sig')

def field(text, name, default=None):
    match = re.search(r'^  ' + re.escape(name) + r': (.*)$', text, re.M)
    if match:
        return match[1]
    assert default is not None, name
    return default

def guid(path):
    return re.search(r'^guid: (\w+)', read(path), re.M)[1]

def painting(text):
    value = (field(text, 'gridWidth'), field(text, 'gridHeight'),
             tuple(re.findall(r'^    rgba: (\d+)$', text, re.M)), read_cells(text).hex())
    return hashlib.sha256(repr(value).encode()).hexdigest()

levels = [read(campaign / f'Level_{i:03d}.asset') for i in range(1, 301)]
database_guids = re.findall(r'^  - \{fileID: 11400000, guid: (\w+), type: 2\}',
                            read(root / 'Assets/Levels/LevelDatabase.asset'), re.M)
assert database_guids == [guid(campaign / f'Level_{i:03d}.asset.meta') for i in range(1, 301)]
for i, level in enumerate(levels, 1):
    for asset in [f'Rules_{i:03d}.asset']:
        assert guid(campaign / (asset + '.meta')) in level, (i, asset)
    cells = read_cells(level)
    assert len(cells) == int(field(level, 'gridWidth')) * int(field(level, 'gridHeight')), i
    assert 'previewTexture:' not in level, i
    with preview_image(level) as image:
        assert image.size == (int(field(level, 'gridWidth')), int(field(level, 'gridHeight')))
protected = json.loads(read(root / 'tasks/first-ten-preservation.json'))
for path, expected in protected.items():
    current = Path(path)
    if current.exists() and hashlib.sha256(current.read_bytes()).hexdigest() == expected:
        continue
    original = root / 'Temp/level-storage-backup' / current.relative_to(root)
    assert hashlib.sha256(original.read_bytes()).hexdigest() == expected, path
    if current.suffix == '.asset':
        assert read_cells(read(current)) == read_cells(read(original)), path
        assert other_fields(read(current)) == other_fields(read(original)), path
    elif current.name.endswith('_preview.png'):
        assert not current.exists(), path
        with Image.open(original) as old, preview_image(levels[int(current.stem.split('_')[1]) - 1]) as new:
            assert new.resize(old.size, Image.Resampling.NEAREST).tobytes() == old.convert('RGBA').tobytes(), path
    else:
        assert current.name.endswith('_preview.png.meta') and not current.exists(), path
assert Counter(painting(t) for t in levels[10:100]) == Counter(
    painting(read(backup / f'Level_{i:03d}.asset')) for i in range(11, 101))
assert len(set(painting(t) for t in levels)) == 300
structures = [field(t, 'bucketLayout') for t in levels]
assert len(set(structures[10:])) == 290
assert not (set(structures[:10]) & set(structures[10:]))
themes = [field(t, 'artTheme') for t in levels[10:]]
assert all(a != b for a, b in zip(themes, themes[1:]))
assert all(field(t, 'campaignTemplateVersion') == '1' for t in levels[10:])
raw = sorted((root / 'Assets/Images/CampaignExpansion/Paintings').glob('Painting_*.png'))
assert len(raw) == 200
assert len({hashlib.sha256(p.read_bytes()).hexdigest() for p in raw}) == 200
for path in raw:
    with Image.open(path) as image:
        image.verify()
crates = sum(field(t, 'alternatingCrates', '0') == '1' for t in levels)
loops = sum(field(t, 'loopInventory', '0') == '1' for t in levels)
result = {
    'levels': 300, 'new_paintings': 200, 'unique_paintings': 300,
    'unique_templates_levels_11_100': len(set(structures[10:100])),
    'unique_templates_levels_11_300': 290,
    'verified_first_ten_original_files': len(protected),
    'preserved_first_ten_existing_template_groups': len(set(structures[:10])),
    'old_art_11_100_preserved_and_reordered': True,
    'adjacent_same_themes_after_level_10': 0,
    'crate_levels': crates, 'loop_levels': loops,
    'all_database_rules_references_and_generated_previews_valid': True,
}
(root / 'tasks/campaign300-integration.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
(root / 'tasks/campaign300-summary.md').write_text(f'''# Кампания: 300 уровней

В основной LevelDatabase добавлены уровни 101–300 и 200 новых картинок. Все 300 картинок различаются. Исходные атласы и отдельные PNG находятся в Assets/Images/CampaignExpansion.

Уровни 1–10 сохраняют прежние клетки, цвета, настройки и вид превью; проверено по исходным {len(protected)} файлам с учётом миграции на сжатое текстовое хранение. В этих десяти уровнях остаются {len(set(structures[:10]))} существующих групп геометрии. На уровнях 11–100 теперь 90 разных шаблонов; на 11–300 — 290 разных расположений стен и выдающих слотов, без совпадений с первыми десятью. Темы соседних картинок после уровня 10 не повторяются. Старые 90 картинок сохранены и переставлены. Превью генерируются из данных уровней.

Ящики используются на {crates} уровнях. Подъём и опускание занимают 0,65 секунды; повторное обновление интерфейса не сбрасывает анимацию. Исправлены недоступный закрытый ряд, продвижение очереди после возврата, приоритет выдающих слотов, свободное место в кольцевой очереди и совместный выход связанных вёдер. Кольцевая механика встречается на {loops} уровнях. В кампании ящики отделены от ключей и связанных пар, чтобы не создавать зависимые блокировки.

GameAnalytics и Your Games / Яндекс.Метрика подключены для WebGL. Записываются начало, завершение, перезапуск и уход с уровня, обучение, бустеры и существующие рекламные/экономические события. Прохождение не дублируется повторным callback. Mirra продолжает обслуживать платформу, рекламу и сохранения.

Проверки: 900 вариантов очередей (по три на каждый уровень), 39 прохождений на настоящей симуляции песка, регрессии ящиков и выдающих слотов, логика попыток аналитики, JavaScript-мосты и компиляция WebGL-веток C#. Симуляция использует одну песчинку на клетку и не измеряет FPS устройства. Отчёты: campaign300-audit.md, campaign-random-mechanics.md, campaign-simulation.md, crate-validation.md, analytics-validation.md, web-analytics-bridge-validation.md, webgl-managed-validation.md.

Остаётся указать ID счётчика Метрики и Game Key / Secret Key GameAnalytics в настройках Unity. В установленном Unity отсутствует WebGL Build Support, поэтому реальная WebGL-сборка и получение событий в кабинетах не проверены. Инструкция: [web-analytics-setup.md](web-analytics-setup.md).
''', encoding='utf-8')
print(json.dumps(result, ensure_ascii=False))
