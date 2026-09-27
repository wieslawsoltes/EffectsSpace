#!/usr/bin/env python3
"""Fetch the open-licensed font, verify locked checksums and preserve its OFL notice."""
from pathlib import Path
from urllib.request import Request, urlopen
import hashlib

root = Path(__file__).resolve().parents[1]
output = root / 'src/EffectsSpace.App/Assets/Fonts'
output.mkdir(parents=True, exist_ok=True)
base = 'https://raw.githubusercontent.com/google/fonts/main/ofl/inter/'
assets = [
    ('Inter%5Bopsz,wght%5D.ttf', 'Inter.ttf', '29160a80ff49ddcab2c97711247e08b1fab27a484a329ce8b813d820dc559031'),
    ('OFL.txt', 'OFL.txt', '5b9321a4298cfeb6b34354164a1c3afc3db114569984c502b9b35d988fd58c57'),
]
for remote, local, expected in assets:
    destination = output / local
    if destination.exists():
        data = destination.read_bytes()
    else:
        with urlopen(Request(base + remote, headers={'User-Agent': 'EffectsSpace-build'}), timeout=45) as response:
            data = response.read(8 * 1024 * 1024)
    actual = hashlib.sha256(data).hexdigest()
    if actual != expected:
        raise SystemExit(f'{local}: checksum mismatch; review the upstream change before updating the lock.')
    destination.write_bytes(data)
    print(local, actual)
