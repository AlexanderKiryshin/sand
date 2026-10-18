"""Assemble native Unity gameplay captures into two caption-free 30 s MP4s."""
from pathlib import Path
import argparse
import csv
import json
import subprocess
import wave

import cv2
import imageio_ffmpeg
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "tasks/marketing"
SOURCE = OUTPUT / "lake-source"
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()
FPS = 30
CUTS = [(0, 0.0, 30.0)]


def run(args):
    subprocess.run([FFMPEG, "-hide_banner", "-loglevel", "error", "-y", *map(str, args)], check=True)


def read_audio(name, rate=44100):
    raw = subprocess.check_output([FFMPEG, "-v", "error", "-i", str(ROOT / "Assets/Resources/Audio" / name),
                                   "-f", "f32le", "-ar", str(rate), "-ac", "2", "pipe:1"])
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()


def make_audio():
    rate = 44100
    count = 30 * rate
    music = read_audio("SandGarden.wav")
    # Use the game's own music and effects; no narration or externally sourced assets.
    audio = music[:count].copy() * .35
    music_envelope = np.ones(count, np.float32)
    music_envelope[:rate] = np.linspace(0, 1, rate)
    music_envelope[-rate:] = np.linspace(1, 0, rate)
    audio *= music_envelope[:, None]
    effects = {"tap": read_audio("BucketTap.wav"), "filled": read_audio("BucketFilled.wav")}
    events = []
    if (SOURCE / "events.csv").exists():
        for row in csv.reader((SOURCE / "events.csv").read_text().splitlines()):
            events.append((float(row[0].replace(",", ".")), row[1], int(row[2])))
    envelope = np.zeros(count, np.float32)
    destination = 0.0
    for shot, start, duration in CUTS:
        for timestamp, kind, event_shot in events:
            relative = timestamp - shot * 11.0
            if event_shot != shot or not start <= relative < start + duration:
                continue
            index = int((destination + relative - start) * rate)
            if kind == "flow":
                envelope[index:min(count, index + int(.15 * rate))] = .25
                continue
            if kind not in effects:
                continue
            sample = effects[kind]
            end = min(count, index + len(sample))
            audio[index:end] += sample[:end - index] * (.62 if kind == "tap" else .5)
        destination += duration
    # The existing sand loop follows actual grain collection, with a soft release.
    sand = read_audio("SandPour.wav")
    sand = np.tile(sand, (int(np.ceil(count / len(sand))), 1))[:count]
    ramp = int(.07 * rate)
    summed = np.concatenate(([0.0], np.cumsum(np.pad(envelope, (ramp // 2, ramp - 1 - ramp // 2)), dtype=np.float64)))
    envelope = ((summed[ramp:] - summed[:-ramp]) / ramp).astype(np.float32)
    audio += sand * envelope[:, None]
    peak = float(np.max(np.abs(audio)))
    if peak > .92:
        audio *= .92 / peak
    with wave.open(str(OUTPUT / "gameplay-audio.wav"), "wb") as stream:
        stream.setnchannels(2)
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes((np.clip(audio, -1, 1) * 32767).astype("<i2").tobytes())


def assemble():
    make_audio()
    for prefix, aspect in [("source-", "9x16"), ("wide-source-", "16x9")]:
        args = []
        filters = []
        for index, (shot, start, duration) in enumerate(CUTS):
            args += ["-ss", start, "-t", duration, "-i", SOURCE / f"{prefix}{shot}.mp4"]
            filters.append(f"[{index}:v]setpts=PTS-STARTPTS,setsar=1[v{index}]")
        filters.append("".join(f"[v{i}]" for i in range(len(CUTS))) + f"concat=n={len(CUTS)}:v=1:a=0[v]")
        args += ["-i", OUTPUT / "gameplay-audio.wav"]
        run([*args, "-filter_complex", ";".join(filters), "-map", "[v]", "-map", f"{len(CUTS)}:a:0", "-r", FPS,
             "-c:v", "libx264", "-preset", "fast", "-crf", "17", "-pix_fmt", "yuv420p",
             "-c:a", "aac", "-b:a", "192k", "-t", "30", "-movflags", "+faststart",
             OUTPUT / f"gameplay-{aspect}-30s.mp4"])


def verify():
    report = {"captions": False, "title_cards": False, "fps": FPS, "duration_seconds": 30,
              "level": 9, "background": "lake", "background_index": 27, "tutorials": False,
              "source": "Real Unity scene and sand simulation, recorded in a disposable editor profile.",
              "landscape_layout": "Native 1920x1080 render of the same gameplay state; complete field, extended scene background.",
              "audio": "Existing game music and sound assets; effects aligned to logged gameplay events.",
              "files": []}
    for name, expected in [("gameplay-9x16-30s.mp4", (1080, 1920)), ("gameplay-16x9-30s.mp4", (1920, 1080))]:
        path = OUTPUT / name
        video = cv2.VideoCapture(str(path))
        width, height = int(video.get(cv2.CAP_PROP_FRAME_WIDTH)), int(video.get(cv2.CAP_PROP_FRAME_HEIGHT))
        fps = video.get(cv2.CAP_PROP_FPS)
        frames = int(video.get(cv2.CAP_PROP_FRAME_COUNT))
        assert (width, height) == expected, (name, width, height)
        assert frames == 900 and abs(fps - 30) < .001, (name, frames, fps)
        thumbnails = []
        checks = []
        for second in [1, 4, 7, 9, 12, 15, 17, 20, 22, 24, 27, 29]:
            video.set(cv2.CAP_PROP_POS_MSEC, second * 1000)
            ok, frame = video.read()
            assert ok, (name, second)
            assert frame.std() > 15, (name, second, "Empty or blank capture")
            thumb = cv2.resize(frame, (270, 480) if width < height else (480, 270))
            thumbnails.append(thumb)
            checks.append(float(frame.std()))
        video.release()
        rows = [np.hstack(thumbnails[n:n+4]) for n in range(0, len(thumbnails), 4)]
        cv2.imwrite(str(OUTPUT / (path.stem + "-contact.jpg")), np.vstack(rows))
        run(["-v", "error", "-i", path, "-f", "null", "-"])
        report["files"].append({"name": name, "width": width, "height": height,
                                "frames": frames, "fps": fps, "bytes": path.stat().st_size,
                                "full_decode": "PASS"})
    (OUTPUT / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--verify-only", action="store_true")
    options = parser.parse_args()
    if not options.verify_only:
        assemble()
    verify()
