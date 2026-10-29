"""Capture additional marketing locales in an existing disposable Unity project.

Copy the game's Assets, Packages, ProjectSettings and Tools/MarketingCapture.cs
into the disposable project first. All captures use the editor's preview save.
"""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "tasks/marketing"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--unity", default="G:/Unity/2022.3.62f3/Editor/Unity.exe")
    parser.add_argument("--languages", nargs="+", choices=["ru", "en", "tr", "de"], default=["tr", "de"])
    parser.add_argument("--only", choices=["screenshots", "videos", "all"], default="all")
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--final-root", type=Path, default=ROOT / "Marketing_Originals")
    parser.add_argument("--video-level", type=int, default=9, help="One-based regular campaign level")
    parser.add_argument("--screenshot-indices", nargs=6, type=int, help="Six zero-based database indices")
    parser.add_argument("--min-level", type=int, default=1, help="Minimum displayed level number, including seasonal levels")
    args = parser.parse_args()
    output = args.output.resolve()
    final_root = args.final_root.resolve()
    output.mkdir(parents=True, exist_ok=True)
    final_root.mkdir(parents=True, exist_ok=True)
    if args.video_level < args.min_level or args.min_level < 1:
        raise ValueError("Invalid minimum or video level")
    project = args.project.resolve()
    if project == ROOT or not (project / "Assets/Editor/MarketingCapture.cs").is_file():
        raise ValueError("A prepared disposable capture project is required")
    # Use a dedicated preview flag so unrelated editor smoke checks cannot run.
    store = project / "Assets/Scripts/SDK/GameProgressStore.cs"
    source = store.read_text(encoding="utf-8")
    marker = 'return UnityEditor.SessionState.GetBool("Sand.CampaignPreview", false)'
    if 'GetBool("Sand.MarketingCapture", false)' not in source:
        if marker not in source:
            raise ValueError("Unrecognized preview-profile implementation")
        source = source.replace(marker, 'return UnityEditor.SessionState.GetBool("Sand.MarketingCapture", false)\n'
                                '                || UnityEditor.SessionState.GetBool("Sand.CampaignPreview", false)')
        store.write_text(source, encoding="utf-8")
    shutil.copy2(ROOT / "Tools/MarketingCapture.cs", project / "Assets/Editor/MarketingCapture.cs")
    temporary = project / "CaptureTemp"
    temporary.mkdir(exist_ok=True)
    environment = dict(os.environ, TEMP=str(temporary), TMP=str(temporary),
                       SAND_CAPTURE_FFMPEG=imageio_ffmpeg.get_ffmpeg_exe(),
                       SAND_CAPTURE_LANGUAGES=",".join(args.languages), SAND_CAPTURE_LEVEL=str(args.video_level - 1),
                       SAND_CAPTURE_MIN_LEVEL=str(args.min_level))
    if args.screenshot_indices:
        environment["SAND_CAPTURE_SCREENSHOT_INDICES"] = ",".join(map(str, args.screenshot_indices))
    else:
        environment.pop("SAND_CAPTURE_SCREENSHOT_INDICES", None)
    runs = []
    if args.only != "videos":
        runs.append(("screenshots-" + "-".join(args.languages),
                     output / ("screenshot-capture-" + "-".join(args.languages)), "1", args.languages[0]))
    if args.only != "screenshots":
        for language in args.languages:
            source_name = "lake-source" if language == "ru" else f"lake-source-{language}"
            runs.append((f"video-{language}", output / source_name, "0", language))
    for label, destination, screenshots, language in runs:
        destination.mkdir(parents=True, exist_ok=True)
        status = destination / "capture-status.txt"
        if status.exists():
            status.unlink()
        environment.update(SAND_CAPTURE_OUTPUT=str(destination), SAND_CAPTURE_SCREENSHOTS=screenshots,
                           SAND_CAPTURE_LANGUAGE=language)
        log = output / f"capture-{label}.log"
        command = [args.unity, "-batchmode", "-force-d3d11", "-projectPath", str(project),
                   "-executeMethod", "MarketingCapture.Run", "-logFile", str(log)]
        print(f"Capturing {label}: {destination}", flush=True)
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = subprocess.SW_HIDE
        result = subprocess.run(command, cwd=project, env=environment, startupinfo=startup,
                                creationflags=subprocess.CREATE_NO_WINDOW)
        if result.returncode != 0 or not status.exists() or not status.read_text().startswith("PASS\n"):
            raise RuntimeError(f"Capture failed ({result.returncode}): {log}; {status}")
        print(f"PASS: {label}", flush=True)
        if screenshots == "1":
            subprocess.run([sys.executable, str(ROOT / "Tools/verify_marketing_screenshots.py"),
                            str(destination), "--output", str(output), "--final-root", str(final_root),
                            "--min-level", str(args.min_level)], check=True, cwd=ROOT)
        else:
            subprocess.run([sys.executable, str(ROOT / "Tools/edit_marketing_gameplay.py"),
                            "--language", language, "--output", str(output)], check=True, cwd=ROOT)
            for aspect in ["9x16", "16x9"]:
                filename = f"gameplay-{language}-{aspect}-5x.mp4"
                shutil.copy2(output / filename, final_root / filename)


if __name__ == "__main__":
    main()
