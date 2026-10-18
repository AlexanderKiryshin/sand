"""Losslessly optimize added PNGs and deliverables; preserve pixels and color metadata."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import zlib

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SIGNATURE = b'\x89PNG\r\n\x1a\n'


def chunks(data):
    assert data.startswith(SIGNATURE)
    offset = 8
    while offset < len(data):
        length = struct.unpack('>I', data[offset:offset + 4])[0]
        end = offset + 12 + length
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:end - 4]
        assert zlib.crc32(kind + payload) == struct.unpack('>I', data[end - 4:end])[0]
        yield kind, payload
        offset = end
    assert offset == len(data)


def pack(parts):
    return SIGNATURE + b''.join(struct.pack('>I', len(body)) + kind + body
                               + struct.pack('>I', zlib.crc32(kind + body))
                               for kind, body in parts)


def retain_metadata(encoded, original):
    # Keep original color space, density and descriptive chunks byte for byte.
    # Background/palette bookkeeping depends on the PNG color type.
    metadata = [(k, v) for k, v in original
                if k not in {b'IHDR', b'PLTE', b'tRNS', b'IDAT', b'IEND', b'bKGD', b'hIST'}]
    new = list(chunks(encoded))
    old_header = original[0][1]
    if new[0][1] == old_header:
        metadata.extend((k, v) for k, v in original if k in {b'bKGD', b'hIST'})
    core = [(k, v) for k, v in new if k in {b'PLTE', b'tRNS', b'IDAT'}]
    return pack([new[0], *metadata, *core, (b'IEND', b'')])


def sha(data):
    return hashlib.sha256(data).hexdigest()


def optimize_group(paths):
    original_bytes = paths[0].read_bytes()
    original = list(chunks(original_bytes))
    assert not any(kind == b'acTL' for kind, _ in original), 'Animated PNG needs a different optimizer.'
    with Image.open(BytesIO(original_bytes)) as source:
        source.load()
        pixels = source.convert('RGBA').tobytes()
        size = source.size
        image = source.copy()
        if image.mode == 'RGBA' and image.getchannel('A').getextrema() == (255, 255):
            image = image.convert('RGB')
        encoded = BytesIO()
        image.save(encoded, format='PNG', optimize=True, compress_level=9)
        best = min(original_bytes, retain_metadata(encoded.getvalue(), original), key=len)
    # Also try recompressing the original PNG filters without changing any chunks.
    filtered = zlib.decompress(b''.join(v for k, v in original if k == b'IDAT'))
    for strategy in (zlib.Z_DEFAULT_STRATEGY, zlib.Z_FILTERED):
        compressor = zlib.compressobj(9, zlib.DEFLATED, 15, 9, strategy)
        compressed = compressor.compress(filtered) + compressor.flush()
        parts = []
        wrote_data = False
        for kind, body in original:
            if kind == b'IDAT':
                if not wrote_data:
                    parts.append((kind, compressed))
                    wrote_data = True
            else:
                parts.append((kind, body))
        candidate = pack(parts)
        if len(candidate) < len(best):
            best = candidate
    list(chunks(best))
    with Image.open(BytesIO(best)) as result:
        assert result.size == size
        assert result.convert('RGBA').tobytes() == pixels, 'Pixel data changed.'
    results = []
    for path in paths:
        assert path.resolve().is_relative_to(ROOT)
        assert path.read_bytes() == original_bytes, 'File changed during optimization.'
        if len(best) < len(original_bytes):
            backup = ROOT / 'Temp/graphics-compression-backup' / path.relative_to(ROOT)
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copy2(path, backup)
            staging = path.with_name(path.name + '.optimizing')
            staging.write_bytes(best)
            staging.replace(path)
        with Image.open(path) as saved:
            assert saved.size == size and saved.convert('RGBA').tobytes() == pixels
        results.append({'path': path.relative_to(ROOT).as_posix(), 'before': len(original_bytes),
                        'after': len(best), 'size': list(size), 'rgba_sha256': sha(pixels),
                        'before_sha256': sha(original_bytes), 'after_sha256': sha(best)})
    return results


def discover():
    def git(*args):
        return subprocess.check_output(['git', '-c', 'safe.directory=' + ROOT.as_posix(),
                                        '-c', 'core.quotepath=false', *args],
                                       cwd=ROOT, stderr=subprocess.DEVNULL).decode('utf-8').splitlines()
    names = set(git('ls-files', '--others', '--exclude-standard', 'Assets'))
    names.update(git('diff', '--name-only', '--', 'Assets'))
    paths = {ROOT / p for p in names if p.lower().endswith('.png')}
    paths.update((ROOT / 'Marketing_Originals').rglob('*.png'))
    return sorted(p for p in paths if p.is_file())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true', help='Optimize files in place with backups.')
    args = parser.parse_args()
    paths = discover()
    if not args.apply:
        print(json.dumps({'files': len(paths), 'bytes': sum(p.stat().st_size for p in paths)}))
        return
    groups = {}
    for path in paths:
        groups.setdefault(sha(path.read_bytes()), []).append(path)
    records = []
    with ThreadPoolExecutor(max_workers=4) as executor:
        for result in executor.map(optimize_group, groups.values()):
            records.extend(result)
            print('Verified %d/%d PNGs' % (len(records), len(paths)), flush=True)
    before = sum(r['before'] for r in records)
    after = sum(r['after'] for r in records)
    report = {'status': 'PASS', 'files': len(records), 'changed': sum(r['after'] < r['before'] for r in records),
              'before_bytes': before, 'after_bytes': after, 'saved_bytes': before - after,
              'saved_percent': round((before - after) * 100 / before, 2),
              'pixels_and_dimensions_unchanged': True, 'records': records}
    destination = ROOT / 'tasks/graphics-compression-report.json'
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'records'}, indent=2))


if __name__ == '__main__':
    main()
