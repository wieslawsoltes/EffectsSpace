#!/usr/bin/env python3
"""Download the SIL-OFL Inter font used by the Uno controls and Skia text renderer."""
from pathlib import Path
from urllib.request import Request, urlopen
import hashlib

root = Path(__file__).resolve().parents[1]
output = root / 'src/EffectsSpace.App/Assets/Fonts'
output.mkdir(parents=True, exist_ok=True)
base = 'https://raw.githubusercontent.com/google/fonts/main/ofl/inter/'
for remote, local in [('Inter%5Bopsz,wght%5D.ttf', 'Inter.ttf'), ('OFL.txt', 'OFL.txt')]:
    destination = output / local
    if not destination.exists():
        with urlopen(Request(base + remote, headers={'User-Agent': 'EffectsSpace-build'}), timeout=45) as response:
            data = response.read(8 * 1024 * 1024)
        if not data:
            raise RuntimeError('Empty font response')
        destination.write_bytes(data)
    print(local, hashlib.sha256(destination.read_bytes()).hexdigest())
