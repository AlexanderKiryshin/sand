"""Install the pinned official packages already downloaded into Temp, without traversal/overwrites."""
from pathlib import Path, PurePosixPath
import json
import tarfile

ROOT = Path(__file__).resolve().parents[1]

def write_new(path, data):
    path = path.resolve()
    if ROOT not in path.parents:
        raise ValueError(f"Outside workspace: {path}")
    if path.exists():
        if path.read_bytes() != data:
            raise ValueError(f"Refusing to overwrite a different existing file: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)

with tarfile.open(ROOT / "Temp/Metrica.unitypackage") as archive:
    for member in archive.getmembers():
        if not member.isfile() or not member.name.endswith("/pathname"):
            continue
        asset_path = PurePosixPath(archive.extractfile(member).read().decode().strip())
        if not str(asset_path).startswith("Assets/PluginYourGames/Modules/Metrica/") or ".." in asset_path.parts:
            if str(asset_path) == "Assets/PluginYourGames/Modules/Metrica":
                pass
            else:
                raise ValueError(asset_path)
        folder = member.name.rsplit("/", 1)[0]
        for filename, suffix in (("asset", ""), ("asset.meta", ".meta")):
            try:
                data = archive.extractfile(folder + "/" + filename).read()
            except KeyError:
                continue
            write_new(ROOT / (str(asset_path) + suffix), data)

with tarfile.open(ROOT / "Temp/gameanalytics.tgz") as archive:
    for member in archive.getmembers():
        if not member.isfile():
            continue
        path = PurePosixPath(member.name)
        if path.parts[0] != "package" or ".." in path.parts:
            raise ValueError(path)
        write_new(ROOT / "Packages/com.gameanalytics.sdk" / str(path.relative_to("package")), archive.extractfile(member).read())

path = ROOT / "Packages/manifest.json"
manifest = json.loads(path.read_text(encoding="utf-8-sig"))
manifest["dependencies"]["com.gameanalytics.sdk"] = "file:com.gameanalytics.sdk"
path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print("Installed Your Games Metrica 1.02 and official GameAnalytics 8.1.0 (embedded UPM).")
