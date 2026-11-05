"""Instrument only the disposable Unity copy; never changes production sources."""
from pathlib import Path
import shutil
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
project = root / "Temp/YoutubeEconomyBuildProject"

def method_span(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth, end = 1, opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return start, opening, end

targets = {
    "Sand/SandRenderer.cs": ["public void RefreshDirty(", "public void RevealLevelBlock(", "private void ApplyTextureFull(", "private void ApplyTextureRegion("],
    "Sand/FallingSandSimulator.cs": ["private void Update("],
    "Polish/LevelIntroSandAnimator.cs": ["private void Update(", "private void SpawnVisibleGrain("],
    "Polish/SandPourAnimator.cs": ["private void Update(", "private void HandleGrainCollected(", "private Vector2 GetGrainSize("],
    "Conveyor/ConveyorBelt.cs": ["public BeltEntry FindBucketForColumn(", "private int GetLevelGridWidth("],
}
for relative, methods in targets.items():
    source_file = root / "Assets/Scripts" / relative
    original = root / "Temp/SandPerformance/originals" / Path(relative).name
    if "--original" in sys.argv and original.exists():
        source_file = original
    source = source_file.read_text(encoding="utf-8-sig")
    for signature in methods:
        start, opening, end = method_span(source, signature)
        label = Path(relative).stem + "." + signature.split("(")[0].split()[-1]
        source = source[:opening + 1] + '\nusing (SandPerformanceProbe.Sample("' + label + '")) {\n' + source[opening + 1:end - 1] + "\n}\n" + source[end - 1:]
    if relative == "Sand/SandRenderer.cs":
        source = source.replace("texture.Apply(false);", 'using (SandPerformanceProbe.Sample("SandRenderer.TextureUpload")) { texture.Apply(false); }')
    if relative == "Conveyor/ConveyorBelt.cs":
        baseline_file = root / "Temp/SandPerformance/originals/ConveyorBelt.cs"
        baseline = baseline_file.read_text(encoding="utf-8-sig") if baseline_file.exists() else subprocess.check_output(
            ["git", "-c", f"safe.directory={root.as_posix()}", "show", "HEAD:Assets/Scripts/Conveyor/ConveyorBelt.cs"], cwd=root, text=True)
        start, _, end = method_span(baseline, "public BeltEntry FindBucketForColumn(")
        method = baseline[start:end].replace("FindBucketForColumn(", "FindBucketForColumnBaseline(", 1)
        final_brace = source.rfind("}")
        source = source[:final_brace] + method + "\n" + source[final_brace:]
    (project / "Assets/Scripts" / relative).write_text(source, encoding="utf-8")
shutil.copy2(root / "Tools/SandPerformanceProbe.cs", project / "Assets/Scripts/SandPerformanceProbe.cs")
shutil.copy2(root / "Tools/SandPerformanceChecks.cs", project / "Assets/Editor/SandPerformanceChecks.cs")
print("Prepared disposable performance probe.")
