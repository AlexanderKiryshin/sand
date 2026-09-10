"""Read/write the same lossless S1 cell format as LevelCellCodec.cs (no dependencies)."""
import base64
import re
import zlib

MAX_CELLS = 16 * 1024 * 1024


def encode_cells(cells):
    if not cells:
        return ''
    if len(cells) > MAX_CELLS:
        raise ValueError('Level cell array is too large')
    bits = max(1, max(cells).bit_length())
    packed = bytearray((len(cells) * bits + 7) // 8)
    for i, cell in enumerate(cells):
        offset = i * bits
        value = cell << (offset & 7)
        packed[offset >> 3] |= value & 255
        if (offset & 7) + bits > 8:
            packed[(offset >> 3) + 1] |= value >> 8
    compressor = zlib.compressobj(level=9, wbits=-15)
    data = base64.b64encode(compressor.compress(packed) + compressor.flush()).decode('ascii')
    return f'S1:{bits}:{len(cells)}:{zlib.crc32(cells):08X}:{data}'


def decode_cells(encoded):
    if not encoded:
        return b''
    version, bits, count, checksum, data = encoded.split(':')
    bits, count = int(bits), int(count)
    if version != 'S1' or not 1 <= bits <= 8 or not 1 <= count <= MAX_CELLS:
        raise ValueError('Invalid level cell header')
    length = (count * bits + 7) // 8
    inflater = zlib.decompressobj(wbits=-15)
    packed = inflater.decompress(base64.b64decode(data, validate=True), length + 1)
    if len(packed) != length or not inflater.eof or inflater.unused_data:
        raise ValueError('Invalid compressed level length')
    mask = (1 << bits) - 1
    cells = bytearray(count)
    for i in range(count):
        offset = i * bits
        value = packed[offset >> 3]
        if (offset & 7) + bits > 8:
            value |= packed[(offset >> 3) + 1] << 8
        cells[i] = (value >> (offset & 7)) & mask
    if zlib.crc32(cells) != int(checksum, 16):
        raise ValueError('Level cell checksum mismatch')
    return bytes(cells)


def read_cells(text):
    encoded = re.search(r'^  encodedCells:[ \t]*(.*)$', text, re.M)
    if encoded and encoded[1].strip():
        return decode_cells(encoded[1].strip().strip('"'))
    legacy = re.search(r'^  (?:cells|legacyCells):[ \t]*([0-9a-fA-F]*)$', text, re.M)
    if legacy:
        return bytes.fromhex(legacy[1])
    raise ValueError('Level has no cell data')


def other_fields(text):
    """Byte-stable authored fields, excluding only the migrated representation/preview."""
    return re.sub(r'^  (?:cells|legacyCells|encodedCells|previewTexture):[^\n]*\n?', '', text, flags=re.M)


def preview_image(text):
    """Reproduce ColorQuantizer's logical preview pixels, including row order and grain."""
    from PIL import Image
    width = int(re.search(r'^  gridWidth: (\d+)', text, re.M)[1])
    height = int(re.search(r'^  gridHeight: (\d+)', text, re.M)[1])
    # Palette items are also indented two spaces, so match up to the next named field.
    palette_text = re.search(r'^  palette:\n(.*?)(?=^  \w+:)', text, re.M | re.S)[1]
    palette = [tuple(int(n).to_bytes(4, 'little')) for n in re.findall(r'rgba: (\d+)', palette_text)]
    cells = read_cells(text)
    if len(cells) != width * height:
        raise ValueError('Grid dimensions do not match cells')
    pixels = []
    for i, cell in enumerate(cells):
        color = palette[cell - 1] if 0 < cell <= len(palette) else (0, 0, 0, 0)
        noise = (((((17 * 31 + i % width) * 31 + i // width) * 31 + 92837111) & 0x7fffffff) % 31) - 15
        pixels.append(tuple(max(0, min(255, c + noise)) for c in color[:3]) + (color[3],) if color[3] else color)
    image = Image.new('RGBA', (width, height))
    image.putdata(pixels)
    return image
