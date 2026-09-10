"""Compile WebGL conditional C# paths using Unity's compiler and resolved references.

This supplements Editor checks; it is not an IL2CPP/linker/WebGL player build.
"""
from pathlib import Path
import os
import subprocess

root = Path(__file__).resolve().parents[1]
project = root / 'Temp/ValidationProject'
output = root / 'Temp/WebGLManaged'
output.mkdir(exist_ok=True)
unity = Path('G:/Unity/6000.3.4f1/Editor/Data')
for assembly in ['GameAnalyticsSDK', 'Assembly-CSharp']:
    response = next((project / 'Library/Bee/artifacts').rglob(assembly + '.rsp'))
    lines = response.read_text(encoding='utf-8-sig').splitlines()
    filtered = []
    for line in lines:
        if line.startswith(('-out:', '-refout:')):
            continue
        if line.startswith('-define:') and any(flag in line for flag in
                ['UNITY_EDITOR', 'UNITY_STANDALONE', 'PLATFORM_STANDALONE', 'YandexGamesPlatform_yg']):
            continue
        if line.startswith('-r:') and line.endswith('/GameAnalyticsSDK.ref.dll"'):
            line = '-r:"' + (output / 'GameAnalyticsSDK.dll').as_posix() + '"'
        filtered.append(line)
    filtered.extend(['-define:UNITY_WEBGL', '-define:PLATFORM_WEBGL',
                     '-out:"' + (output / (assembly + '.dll')).as_posix() + '"'])
    target = output / (assembly + '.rsp')
    target.write_text('\n'.join(filtered), encoding='utf-8')
    result = subprocess.run([str(unity / 'NetCoreRuntime/dotnet.exe'), str(unity / 'DotNetSdkRoslyn/csc.dll'),
                             '@' + str(target)], cwd=project, capture_output=True, text=True,
                             creationflags=subprocess.CREATE_NO_WINDOW)
    (output / (assembly + '.log')).write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        print(result.stdout[-8000:])
        raise SystemExit(result.returncode)
(root / 'tasks/webgl-managed-validation.md').write_text(
    'PASS: GameAnalyticsSDK and game runtime C# compile with UNITY_WEBGL + PLATFORM_WEBGL, without UNITY_EDITOR or YandexGamesPlatform_yg. Unity 6000.3.4f1 compiler and resolved project references. WebGL Build Support is absent: this does not verify IL2CPP, JavaScript linking or a browser player.\n', encoding='utf-8')
print('WEBGL_MANAGED_COMPILE_PASS')
