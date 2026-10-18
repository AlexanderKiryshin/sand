"""Remove victory UI, speed gameplay/audio up, and export both native aspects."""
from pathlib import Path
import argparse
import json
import math
import subprocess

import cv2
import imageio_ffmpeg
import numpy as np

import render_marketing_gameplay as original_audio

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "tasks/marketing"
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()


def run(args):
    subprocess.run([FFMPEG, "-y", "-hide_banner", "-loglevel", "error", *map(str, args)], check=True)


def inspect_video(path):
    video = cv2.VideoCapture(str(path))
    assert video.isOpened(), path
    info = {"width": int(video.get(cv2.CAP_PROP_FRAME_WIDTH)),
            "height": int(video.get(cv2.CAP_PROP_FRAME_HEIGHT)),
            "fps": video.get(cv2.CAP_PROP_FPS),
            "frames": int(video.get(cv2.CAP_PROP_FRAME_COUNT))}
    first_victory = None
    reference = None
    lowest_ratio = 1.0
    for index in range(info["frames"]):
        ok, frame = video.read()
        assert ok, (path, index)
        h, w = frame.shape[:2]
        # This fixed patch is outside the board, controls, and reward card.
        # The lake is static; the victory backdrop darkens the entire screen.
        patch = frame[int(h*.25):int(h*.30), int(w*.85):int(w*.89)]
        mean = float(patch.mean())
        if reference is None:
            reference = mean
        ratio = mean / reference
        lowest_ratio = min(lowest_ratio, ratio)
        if first_victory is None and ratio < .93:
            first_victory = index
    video.release()
    info.update(first_victory_frame=first_victory, minimum_background_ratio=lowest_ratio)
    return info


def tempo_filters(speed):
    filters = []
    while speed > 2:
        filters.append("atempo=2")
        speed /= 2
    while speed < .5:
        filters.append("atempo=0.5")
        speed *= 2
    filters.append(f"atempo={speed:.12g}")
    return ",".join(filters)


def edit(language, speed):
    source = OUTPUT / ("lake-source-en" if language == "en" else "lake-source")
    inputs = [("9x16", source / "source-0.mp4", (1080, 1920)),
              ("16x9", source / "wide-source-0.mp4", (1920, 1080))]
    infos = [inspect_video(path) for _, path, _ in inputs]
    assert all(abs(info["fps"] - 30) < .001 for info in infos)
    assert infos[0]["frames"] == infos[1]["frames"]
    kept = min(info["first_victory_frame"] if info["first_victory_frame"] is not None else info["frames"]
               for info in infos)
    assert kept > 30
    output_frames = math.ceil(kept / speed)
    duration = output_frames / 30
    source_duration = kept / 30
    if language == "en":
        config = json.loads((source / "capture-config.json").read_text())
        assert config["language"] == "en"
        # Reuse the game's music/effect mix, with this recording's actual events.
        audio_folder = OUTPUT / "work/en"
        audio_folder.mkdir(parents=True, exist_ok=True)
        original_audio.OUTPUT = audio_folder
        original_audio.SOURCE = source
        original_audio.CUTS = [(0, 0.0, source_duration)]
        original_audio.make_audio()
        audio = audio_folder / "gameplay-audio.wav"
    else:
        audio = OUTPUT / "gameplay-audio.wav"
    report = {"language": language, "speed": speed, "fps": 30,
              "level": 9, "background": "lake", "tutorials": False,
              "captions": False, "victory_window_included": False,
              "kept_source_frames": kept, "kept_source_seconds": source_duration,
              "output_frames": output_frames, "duration_seconds": duration,
              "audio": "Same speed as video, pitch preserved.", "files": []}
    for (aspect, path, expected), source_info in zip(inputs, infos):
        destination = OUTPUT / f"gameplay-{language}-{aspect}-5x.mp4"
        video_filter = (f"[0:v]trim=end_frame={kept},setpts=(PTS-STARTPTS)/{speed},fps=30,"
                        f"tpad=stop_mode=clone:stop_duration=1,trim=end_frame={output_frames},setsar=1[v]")
        audio_filter = (f"[1:a]atrim=end={source_duration:.12g},asetpts=PTS-STARTPTS,{tempo_filters(speed)},"
                        f"apad,atrim=end={duration:.12g}[a]")
        run(["-i", path, "-i", audio, "-filter_complex", video_filter + ";" + audio_filter,
             "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-preset", "fast", "-crf", "17",
             "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", destination])
        result = inspect_video(destination)
        assert (result["width"], result["height"]) == expected
        assert result["frames"] == output_frames, (destination, result)
        assert result["first_victory_frame"] is None, (destination, result)
        assert abs(result["fps"] - 30) < .001
        run(["-i", destination, "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-"])
        video = cv2.VideoCapture(str(destination))
        thumbnails = []
        for index in np.linspace(0, output_frames-1, 6).astype(int):
            video.set(cv2.CAP_PROP_POS_FRAMES, int(index))
            ok, frame = video.read()
            assert ok
            thumbnails.append(cv2.resize(frame, (270, 480) if aspect == "9x16" else (480, 270)))
        video.release()
        cv2.imwrite(str(destination.with_name(destination.stem + "-contact.jpg")),
                    np.vstack([np.hstack(thumbnails[:3]), np.hstack(thumbnails[3:])]))
        report["files"].append({"name": destination.name, "bytes": destination.stat().st_size,
                                "width": expected[0], "height": expected[1],
                                "source_first_victory_frame": source_info["first_victory_frame"],
                                "full_decode": "PASS", "no_victory_window": "PASS"})
    (OUTPUT / f"verification-{language}-5x.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--language", required=True, choices=["ru", "en"])
    parser.add_argument("--speed", type=float, default=5)
    options = parser.parse_args()
    assert math.isfinite(options.speed) and options.speed > 0
    edit(options.language, options.speed)
