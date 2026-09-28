# Audio compatibility and performance update — 2026-09-28

## Added

- WAVEFORMATEXTENSIBLE left-aligned integer valid bits and stricter format-extension validation.
- Allocation-free contiguous stereo decoding and an 8 KiB caller-owned window per mixer voice.
- Rate-adaptive, phase-interpolated Blackman sinc resampling, nested source-time derivative propagation and exact integer same-rate sampling.
- Forward-only PCM16/PCM24/Float32 WAVE writing, fact sample counts, RIFF padding and explicit completion/size validation.
- Counter-indexed optional TPDF dither for integer output, deterministic across write partitions.
- Audio Delivery Settings in the existing Media panel, WAVE precision/rate controls and preview/AVI/WAVE quality selection.
- Exact block-based waveform construction and prepared-audio invalidation when quality changes.
- Audio Fidelity executable, independent FFmpeg decoding of all delivery formats and two real browser scenarios.

## Compatibility

The workbench explicitly defaults to BandLimited. Public AudioMixer/AviExporter defaults remain Linear. Float output retains finite headroom; integer outputs saturate. Quality/delivery settings are session preferences and do not modify project undo history. Existing AVI output remains PCM16/48 kHz.

## Boundaries

No pitch-preserving stretch, mastering certification, native audio output, MP4/WebM, RF64, general compressed audio or complete After Effects parity is introduced. Sinc rate steps above 32 fail explicitly; changing cutoff bands under rapidly varying speed is not smoothed. See AUDIO-FIDELITY.md for implementation, budgets and benchmark interpretation. The preceding portable-media and compositing changes remain recorded in CHANGELOG.md.
