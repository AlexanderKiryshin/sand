"""Replay crash logs or run the existing scene smoke check with real graphics."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
FATAL = re.compile(r"Unrecoverable D3D12 device error|D3D12Fence::Wait.*Device removal|Crash!!!|d3d12: Device failed error")


def inspect(log):
    failures = []
    with log.open(encoding="utf-8", errors="replace") as stream:
        for number, line in enumerate(stream, 1):
            if FATAL.search(line):
                failures.append(f"{log.name}:{number}: {line.strip()}")
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--replay", type=Path)
    parser.add_argument("--api", choices=["configured", "d3d11", "d3d12"], default="configured")
    parser.add_argument("--label", default="configured")
    parser.add_argument("--interactive", action="store_true", help="Use the full editor instead of batch mode.")
    args = parser.parse_args()
    if args.replay:
        failures = inspect(args.replay)
        print("\n".join(failures) or "No matching native crash in captured log.")
        return 1 if failures else 0

    project = ROOT / "Library/EditorCrashValidation"
    log = HERE / f"{args.label}.log"
    report = project / "tasks/build-size-smoke.md"
    if report.exists():
        report.unlink()
    temporary = project / "Temp"
    temporary.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, TEMP=str(temporary), TMP=str(temporary))
    command = ["G:/Unity/6000.3.4f1/Editor/Unity.exe",
               "-projectPath", str(project), "-executeMethod", "BuildSizeSmokeChecks.Run",
               "-logFile", str(log), "-crash-report-folder", str(HERE / "crashes")]
    if not args.interactive:
        command.append("-batchmode")
    if args.api != "configured":
        command.append("-force-" + args.api)
    print(f"Starting real-graphics Play Mode smoke check: {args.label}", flush=True)
    started = time.monotonic()
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    process = subprocess.Popen(command, cwd=project, env=env, startupinfo=startup,
                               creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        process.wait(timeout=300)
    except subprocess.TimeoutExpired:
        process.terminate()
        process.wait(timeout=20)
        print("FAIL: Unity did not complete the smoke check within 300 seconds.")
        return 1
    failures = inspect(log)
    passed = process.returncode == 0 and report.exists() and report.read_text(encoding="utf-8").startswith("PASS\n") and not failures
    print(f"{'PASS' if passed else 'FAIL'}: exit={process.returncode}, elapsed={time.monotonic() - started:.1f}s")
    print("\n".join(failures))
    if report.exists():
        (HERE / f"{args.label}.md").write_bytes(report.read_bytes())
    print(f"Log: {log}")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
