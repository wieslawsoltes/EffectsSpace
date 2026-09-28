#!/usr/bin/env python3
"""Independent test-only container/codec verification with FFmpeg. Not an application dependency."""
from pathlib import Path
import json
import subprocess

root = Path('artifacts/media')
for name in ('clockwork-source.avi', 'clockwork-export.avi'):
    path = root / name
    report = json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]))
    video = next(s for s in report['streams'] if s['codec_type'] == 'video')
    audio = next(s for s in report['streams'] if s['codec_type'] == 'audio')
    assert video['codec_name'] == 'mjpeg' and video['width'] == 320 and video['height'] == 180
    assert video['r_frame_rate'] == '24/1' and int(video['nb_frames']) == 48
    assert audio['codec_name'] == 'pcm_s16le' and audio['channels'] == 2 and audio['sample_rate'] == '48000'
    subprocess.run(['ffmpeg', '-v', 'error', '-xerror', '-i', str(path), '-f', 'null', '-'], check=True)
    pcm = subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(path), '-map', '0:a:0', '-f', 's16le', '-'])
    assert len(pcm) == 96000 * 4, (name, len(pcm))
    (root / (name + '.ffprobe.json')).write_text(json.dumps(report, indent=2))
    print('PASS independent decode:', name, '48 JPEG frames and 96000 stereo sample frames')
