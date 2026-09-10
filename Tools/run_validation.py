"""Run Unity in a disposable copy, keeping all temporary files on E."""
from pathlib import Path
import os
import subprocess
import sys
root = Path(__file__).resolve().parents[1]
method, label = sys.argv[1:3]
temporary = root / "Temp/unity-tmp"
temporary.mkdir(parents=True, exist_ok=True)
environment = os.environ.copy()
environment.update(TEMP=str(temporary), TMP=str(temporary))
result = subprocess.run([
    "G:/Unity/6000.3.4f1/Editor/Unity.exe", "-batchmode", "-nographics", "-quit",
    "-projectPath", str(root / "Temp/ValidationProject"), "-executeMethod", method,
    "-logFile", str(root / ("Temp/" + label + ".log")),
], cwd=root, env=environment, creationflags=subprocess.CREATE_NO_WINDOW)
(root / ("Temp/" + label + "-exit.txt")).write_text(str(result.returncode))
