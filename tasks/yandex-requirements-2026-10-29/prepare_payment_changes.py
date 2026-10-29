from pathlib import Path
import shutil
import zipfile

root = Path('G:/codex/sand-yandex-payments/sand')
stage = Path('E:/myProjects/sand/Temp/YandexPaymentsChanges')
archive = Path(__file__).parent / 'mirra-sdk-9c5c97b.zip'
package = stage / 'Packages/com.romanlee17.mirrasdk5'
if package.exists():
    raise SystemExit('Stage already exists; keep edits intact.')
with zipfile.ZipFile(archive) as z:
    for entry in z.infolist():
        if entry.is_dir():
            continue
        relative = Path(*Path(entry.filename).parts[1:])
        target = (package / relative).resolve()
        assert target.is_relative_to(package.resolve())
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(z.read(entry))
for relative in [
    'Assets/Scripts/UI/RewardedShop.cs',
    'Assets/Scripts/UI/ThemeCustomizationMenu.cs',
    'Assets/Scripts/UI/NatureBackgroundOffer.cs',
    'Packages/manifest.json', 'Packages/packages-lock.json',
    'Tools/check_ad_analytics.cs', 'Tools/check_cosmetic_analytics.ps1',
]:
    target = stage / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / relative, target)
print(f'Prepared clean SDK and editable files in {stage}')
