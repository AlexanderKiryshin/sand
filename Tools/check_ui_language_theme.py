"""Run UI regressions in the existing disposable Unity validation project."""
import json
import filecmp
import os
from pathlib import Path
import shutil
import subprocess

root = Path(__file__).resolve().parents[1]
project = root / "Library/EditorCrashValidation"
if not (project / "Library").is_dir():
    raise SystemExit("The disposable Unity validation project is missing.")

def copy(source, target):
    if target.exists() and filecmp.cmp(source, target, shallow=False):
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + ".copying")
    shutil.copy2(source, temporary)
    os.replace(temporary, target)  # Do not modify a possibly hard-linked source.

for folder in ["Assets/Scripts", "Packages/com.romanlee17.mirrasdk5"]:
    for source in (root / folder).rglob("*"):
        if source.is_file():
            copy(source, project / source.relative_to(root))
manifest = project / "Packages/manifest.json"
data = json.loads(manifest.read_text(encoding="utf-8-sig"))
data["dependencies"]["com.romanlee17.mirrasdk5"] = "file:com.romanlee17.mirrasdk5"
content = json.dumps(data, indent=2)
if manifest.read_text(encoding="utf-8-sig") != content:
    manifest.write_text(content, encoding="utf-8")
copy(root / "Tools/UiLanguageAndThemeChecks.cs", project / "Assets/Editor/UiLanguageAndThemeChecks.cs")
output = root / "Temp/UiLanguageThemeChecks"
output.mkdir(parents=True, exist_ok=True)
environment = dict(os.environ, TEMP=str(output), TMP=str(output))
report = project / "tasks/ui-language-theme-checks.txt"
report.unlink(missing_ok=True)
result = subprocess.run([
    "G:/Unity/6000.3.4f1/Editor/Unity.exe", "-batchmode", "-nographics", "-accept-apiupdate",
    "-projectPath", str(project), "-executeMethod", "UiLanguageAndThemeChecks.Run",
    "-logFile", str(output / "unity.log"),
], cwd=root, env=environment, creationflags=subprocess.CREATE_NO_WINDOW, timeout=600)
if report.exists():
    print(report.read_text(encoding="utf-8"))
else:
    log = (output / "unity.log").read_text(encoding="utf-8", errors="replace")
    print("\n".join(line for line in log.splitlines() if "error CS" in line)[-6000:])
print("Unity exit:", result.returncode)
raise SystemExit(result.returncode)
