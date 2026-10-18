"""Check serialized TMP font references, including fonts that only fail in players."""
from pathlib import Path
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
errors = []
for path in (root / "Assets/TextMesh Pro/Resources/Fonts & Materials").glob("*.asset"):
    source = path.read_text(encoding="utf-8-sig")
    font_blocks = [block for block in re.split(r"(?=--- !u!)", source)
                   if "guid: 71c1514a6bd24e1e882cebbe1904ce04" in block]
    for font in font_blocks:
        material = re.search(r"^  material: \{fileID: (\d+)([^}]*)\}", font, re.M)
        if material is None or material.group(1) == "0":
            errors.append(f"{path.name}: missing font material (TMP fallback rendering will throw)")
            continue
        if "guid:" not in material.group(2):
            if not re.search(rf"^--- !u!21 &{material.group(1)}$", source, re.M):
                errors.append(f"{path.name}: font material points to a missing subasset")
            else:
                print(f"PASS {path.name}: material {material.group(1)}")
if errors:
    print("\n".join("FAIL " + error for error in errors))
    sys.exit(1)
