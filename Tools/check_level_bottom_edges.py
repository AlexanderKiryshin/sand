"""Check campaign paintings for a foreign-color strip at the bottom atlas edge."""
from pathlib import Path
import re
import sys

from level_storage import read_cells

root = Path(__file__).resolve().parents[1]
failed = False
for number in range(1, 301):
    text = (root / f'Assets/Levels/Campaign100/Level_{number:03}.asset').read_text(encoding='utf-8-sig')
    cells = read_cells(text)
    width = int(re.search(r'^  gridWidth: (\d+)', text, re.M)[1])
    neighbors = set(cells[-3 * width:-width])
    foreign = sum(color not in neighbors for color in cells[-width:])
    passed = foreign <= width // 3
    failed |= not passed
    if not passed or number in (51, 52, 53):
        print(f'{"PASS" if passed else "FAIL"}: level {number}, {foreign}/{width} bottom pixels use colors absent from the preceding two rows')
if not failed:
    print('PASS: all 300 campaign paintings have no foreign-color bottom strip')
sys.exit(1 if failed else 0)
