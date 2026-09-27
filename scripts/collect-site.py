#!/usr/bin/env python3
"""Collect the published Uno application; never substitute a static imitation of the editor."""
from pathlib import Path
import json, os, shutil, subprocess, sys

source, target = map(Path, sys.argv[1:3])
indexes = sorted(source.rglob('index.html'), key=lambda p: (p.parent.name != 'wwwroot', len(p.parts)))
if not indexes:
    raise SystemExit('No Uno browser index.html was published.')
site = indexes[0].parent
if not any(site.rglob('*.wasm')):
    raise SystemExit('The publish output has no WebAssembly runtime.')
if target.exists():
    shutil.rmtree(target)
shutil.copytree(site, target)
(target / '.nojekyll').touch()
sha = os.environ.get('GITHUB_SHA') or subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
(target / 'build-info.json').write_text(json.dumps({'product': 'EffectsSpace', 'version': '0.1.0-alpha.1', 'commit': sha, 'runtime': 'Uno WebAssembly', 'source': 'https://github.com/wieslawsoltes/EffectsSpace'}), encoding='utf-8')
print(f'Collected {site} -> {target}')
