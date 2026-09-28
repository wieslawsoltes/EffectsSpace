#!/usr/bin/env python3
"""Independent ffprobe/FFmpeg validation of each new WAVE delivery format; FFmpeg is test-only."""
from pathlib import Path
import json
import math
import struct
import subprocess

root = Path('artifacts/audio')
for encoding, codec in [('Pcm16', 'pcm_s16le'), ('Pcm24', 'pcm_s24le'), ('Float32', 'pcm_f32le')]:
    path = root / f'quality-{encoding}.wav'
    report = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(path)]))
    stream, = report['streams']
    assert stream['codec_name'] == codec, stream
    assert int(stream['sample_rate']) == 44100 and stream['channels'] == 2, stream
    decoded = subprocess.check_output(['ffmpeg', '-v', 'error', '-xerror', '-i', str(path), '-f', 'f32le', '-'])
    assert len(decoded) == 4410 * 2 * 4, len(decoded)
    samples = struct.unpack('<' + 'f' * (len(decoded) // 4), decoded)
    error = max(abs(samples[frame * 2 + channel] - .5 * math.sin(2 * math.pi * 440 * (.1 + frame / 44100)))
                for frame in range(50, 4360) for channel in range(2))
    assert error < 8e-5, (encoding, error)
    (root / f'quality-{encoding}.ffprobe.json').write_text(json.dumps(report, indent=2))
    print('PASS independent WAVE decode:', encoding, '4410 stereo frames at 44100 Hz; max analytic error', error)
