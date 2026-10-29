from pathlib import Path
import shutil
import hashlib
import json

base = Path('D:/CodexData/.codex/skills/web-platform-porting')
stage = Path('E:/myProjects/sand/Temp/YandexRequirementsSkill/web-platform-porting')
stage.mkdir(parents=True, exist_ok=True)
hashes = {}
for relative in ['SKILL.md', 'references/platform-adaptation.md']:
    source = base / relative
    hashes[relative] = hashlib.sha256(source.read_bytes()).hexdigest()
    target = stage / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
file = stage / 'SKILL.md'
text = file.read_text(encoding='utf-8-sig')
text = text.replace('Использовать для нового порта или обновления версии', 'Использовать для проверки соответствия требованиям, нового порта или обновления версии', 1)
text = text.replace('## YouTube Playables', '''## Яндекс Игры

При проверке требований, подготовке Яндекс-версии или исправлении замечаний прочитай [проверку требований Яндекс Игр](references/yandex-requirements-audit.md). Она задаёт источники, границы локальной проверки и доказательства для SDK, покупок, рекламы, сохранений и промоматериалов. Проверяй актуальную редакцию перед каждым релизом.

Для аудита без правок достаточно чтения нужной версии и изолированного локального запуска. Создавай ветку площадки перед изменением игры; обновление этого скилла само по себе не требует переключать проект. Недоступная публичная страница неопубликованной игры не является нарушением.

## YouTube Playables''', 1)
file.write_text(text, encoding='utf-8')
file = stage / 'references/platform-adaptation.md'
text = file.read_text(encoding='utf-8-sig').replace('Подробный накопленный опыт сейчас есть для YouTube;', 'Подробный накопленный опыт портирования есть для YouTube; для Яндекс Игр добавлен отдельный процесс аудита требований;')
file.write_text(text, encoding='utf-8')
(stage.parent / 'original-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
print(stage)
