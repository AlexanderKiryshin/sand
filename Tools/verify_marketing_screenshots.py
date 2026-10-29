"""Verify a localized capture report and publish lossless PNGs plus contact sheets."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "tasks/marketing"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("--output", type=Path, default=OUTPUT)
    parser.add_argument("--final-root", type=Path, default=ROOT / "Marketing_Originals")
    parser.add_argument("--min-level", type=int, default=1)
    args = parser.parse_args()
    output = args.output.resolve()
    final_root = args.final_root.resolve()
    capture = args.capture.resolve()
    assert (capture / "capture-status.txt").read_text().startswith("PASS\n")
    report = json.loads((capture / "screenshots.json").read_text(encoding="utf-8"))
    records = report["screenshots"]
    languages = sorted({record["language"] for record in records})
    scenes = list(dict.fromkeys(record["scene"] for record in records))
    assert len(scenes) == 6 and len(records) == 6 * len(languages)
    state_keys = ["databaseIndex", "backgroundIndex", "conveyorIndex", "casingsIndex", "remainingGrains", "occupiedSlots"]
    level_words = {"RU": "Уровень", "EN": "Level", "TR": "Seviye", "DE": "Stufe"}
    for scene in scenes:
        group = [record for record in records if record["scene"] == scene]
        assert sorted(record["language"] for record in group) == languages
        assert len({tuple(record[key] for key in state_keys) for record in group}) == 1
    files = []
    for record in records:
        assert record["level"] >= args.min_level, record
        assert not record["tutorialHintVisible"]
        assert any(level_words[record["language"]] in label for label in record["visibleLabels"])
        if record["language"] != "RU":
            assert not any("\u0400" <= char <= "\u04ff" for label in record["visibleLabels"] for char in label)
        for orientation, size in [("portrait", (1440, 2560)), ("landscape", (2560, 1440))]:
            relative = record[orientation]
            source = capture / relative
            target = final_root / "Screenshots" / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            with Image.open(source) as picture:
                picture.load()
                assert picture.size == size and picture.format == "PNG"
                pixels = picture.convert("RGB").tobytes()
                assert np.asarray(picture).std() > 15, source
                picture.save(target, optimize=True)
            with Image.open(target) as optimized:
                assert optimized.convert("RGB").tobytes() == pixels
            files.append({"file": relative, "size": size, "bytes": target.stat().st_size,
                          "sha256": digest(target.read_bytes()), "rgb_sha256": digest(pixels)})
    for language in languages:
        group = [record for record in records if record["language"] == language]
        for orientation, size in [("portrait", (270, 480)), ("landscape", (480, 270))]:
            width, height = size
            sheet = Image.new("RGB", (width * 3, (height + 30) * 2), "#edf3f2")
            draw = ImageDraw.Draw(sheet)
            for index, record in enumerate(group):
                x, y = (index % 3) * width, (index // 3) * (height + 30)
                with Image.open(final_root / "Screenshots" / record[orientation]) as picture:
                    sheet.paste(picture.resize(size, Image.Resampling.LANCZOS), (x, y))
                draw.text((x + 8, y + height + 8), language + " " + record["scene"], fill="#142c30")
            sheet.save(output / f"screenshots-{language.lower()}-{orientation}-overview.jpg", quality=92)
    verification = {"status": "PASS", "png_count": len(files), "languages": languages,
                    "levels": len(scenes), "minimum_level": args.min_level,
                    "captured_level_numbers": sorted({record["level"] for record in records}),
                    "backgrounds": len({record["backgroundIndex"] for record in records}),
                    "same_gameplay_state_across_languages": True, "losslessly_compressed": True,
                    "localized_labels_verified": True, "files": files}
    destination = output / ("screenshots-verification-" + "-".join(language.lower() for language in languages) + ".json")
    destination.write_text(json.dumps(verification, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"PASS: {len(files)} lossless screenshots, languages {languages}; {destination}")


if __name__ == "__main__":
    main()
