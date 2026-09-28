# Audio fidelity, WAVE delivery and decoding performance

## Workspace controls

Open **Media → Audio Delivery Settings**. Select PCM 16-bit, PCM 24-bit or IEEE float 32-bit; choose 44.1, 48 or 96 kHz; optionally enable TPDF dither for integer delivery. **Ctrl+Alt+W** renders the composition work area with these settings. Back to Media restores the source waveform and gain/balance controls. These settings are session preferences, not project mutations, and do not add undo entries.

The resampling choice applies to browser preview, AVI audio and WAVE output. The workbench defaults to Band-limited. Changing it pauses playback and invalidates prepared audio; a subsequent preview cannot reuse a buffer produced at the old quality. AVI still writes stereo PCM16/48 kHz; the WAVE bit depth/rate controls do not alter its container contract. Native audible preview remains unimplemented.

## PCM compatibility

WAVEFORMATEXTENSIBLE integer files may have fewer valid bits than their 8/16/24/32-bit container. Valid samples are left-aligned: the decoder removes unused low bits using arithmetic sign-preserving shifts and scales according to precision. This supports, for example, 20 valid bits in 24-bit storage and 24 valid bits in 32-bit storage. Floating point still requires a full 32- or 64-bit representation. Invalid precision, overflowing format extensions and unsupported speaker layouts are rejected.

The Media panel displays precision/container depth separately. Encoded source buffers remain immutable. Only mono/stereo little-endian PCM is supported; this is not compressed audio, surround, RF64 or general media-codec support.

Reference: Microsoft WAVEFORMATEXTENSIBLE — https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ksmedia/ns-ksmedia-waveformatextensible

## Band-limited sampling

`PcmResampler` reconstructs stereo at a requested source time and source-seconds-per-output-second derivative. The derivative includes the chain rule through nested source offsets, stretches and remap channels. Its absolute value determines the source-sample step per output sample; reversal changes traversal direction, not the cutoff sign. Frozen source time produces silence rather than repeated DC samples.

The filter is an original Blackman-windowed sinc implementation. It uses 256 fractional phases plus a terminal phase and linearly interpolates adjacent coefficient rows. Each row is normalized for DC gain. The source step is conservatively rounded upward to an eighth-octave band, so quantization never widens the requested antialias cutoff. The cutoff is `0.94 / roundedStep` relative to source Nyquist. Tap counts grow from 64 to a maximum of 512. Same-rate, integer-aligned samples use an exact direct path instead of filtering.

A shared LRU retains at most twelve immutable coefficient banks, below approximately 6.1 MiB of table storage at the maximum tap count. Active readers may keep a reference to their last bank after cache eviction; this cache target is not a total-process memory cap. An instance retains only its last coefficient-bank reference and an 8 KiB decoded source window. Cold bank construction has a finite CPU cost; warmed sampling allocates no per-sample managed objects for static unexpressed audio.

The supported band-limited step is at most 32 source frames per output frame. More extreme speeds fail explicitly. Linear remains available as an explicit compatibility/low-cost mode. Filtering is a finite approximation, not an ideal brick-wall filter or a certified mastering resampler. Eighth-octave cutoff changes under rapidly varying speed may produce modulation; no pitch preservation, adaptive mastering filter or transition crossfade is claimed. Source boundaries are zero-extended in filtered mode; legacy Linear retains its final-sample hold.

## Delivery precision and dither

`WavePcmWriter` streams known-length WAVE files forward without seeking. PCM16 and PCM24 use canonical PCM format chunks. Float32 uses WAVEFORMATEX with zero extension bytes plus a `fact` chunk holding sample **frames**, not interleaved scalar samples. Odd PCM24 mono payloads receive RIFF padding that is excluded from the `data` size. The writer refuses incomplete finalization, extra frames, invalid partial channel frames or writes after completion, and does not close the caller's stream.

Integer output rounds and saturates at full scale. Optional triangular PDF dither adds +/- one target LSB before quantization. Two counter-derived uniform values generate the dither for each absolute scalar sample index and seed, so changing write-block size does not change the output file. Dither decorrelates quantization error; it is not noise shaping or a limiter.

Float32 output preserves finite headroom, including values above 1 or below -1. Nonfinite values are replaced with silence. Dither is rejected for float output. A float file therefore avoids integer saturation in this delivery stage, but downstream hardware or importers may still clip it. The application does not claim a full professional loudness/mastering pipeline.

Classic RIFF sizes and sample counts remain 32-bit. `WaveRenderer` checks the exact selected-format byte budget before allocating output, checks cancellation between 4096-frame blocks, and reports progress. Export owns a snapshot; changing editor content cannot alter an in-progress file. The full archive/download still resides in memory, with the existing 256 MiB cap; streaming directly to persistent browser storage is not implemented.

## Reuse

```csharp
using EffectsSpace.Media;

var mixer = new AudioMixer(project, composition,
    quality: AudioResamplingQuality.BandLimited);

byte[] wave = await WaveRenderer.RenderAsync(
    mixer, composition.WorkStart,
    composition.WorkEnd - composition.WorkStart,
    encoding: WaveEncoding.Pcm24,
    sampleRate: 48000,
    dither: true,
    cancellationToken: cancellationToken);
```

The existing `AudioMixer` and `AviExporter` defaults remain Linear for source compatibility; the workbench explicitly selects BandLimited. `AudioMixer.WaveAsync` retains PCM16 delivery and uses the mixer's configured resampling choice. Callers needing custom delivery use `WaveRenderer` or `WavePcmWriter`.

`PcmSource.ReadStereoFrames` decodes a contiguous range with one segment-index lookup, walks encoded segments sequentially, duplicates mono and zero-pads outside the source. It has no mutable shared cursor. `PcmResampler` and `AudioMixer` hold caller-owned decoding windows and are single-reader objects; create separate instances for concurrent render jobs. Source waveforms now use block decoding as well, preserving exact peak buckets rather than skipping samples.

## Validation

The Audio Fidelity executable checks scalar/block equivalence across PCM formats, left-aligned precision, malformed extensions, source boundaries, exact identity/reverse paths, DC gain, sine reconstruction, passband retention, out-of-band suppression, nested derivative propagation, block partition invariance, seek determinism and warmed allocation behavior. Writer tests cover all three formats, headroom, sample counts, odd padding, deterministic dither, cancellation and budget failures.

`verify-audio.py` uses test-only FFmpeg/ffprobe to decode every delivery format independently. It checks codec, sample rate, channel count, exact decoded frame count and waveform error against a known analytic signal. Browser tests operate through genuine settings buttons, numeric fields, keyboard, Web Audio scheduling and downloads. They verify selected delivery formats, deterministic repeated PCM24 export, float headroom and audio-preview cache invalidation.

The measured `artifacts/performance/audio-window.json` compares 48,000 stereo float source frames split into 495 encoded segments: scalar legacy linear reads versus the bounded stereo linear reader. It reports the median of nine passes after three warmups, source-window size and reload counts. This isolates lookup/decoding performance; it does not compare sinc filtering against linear quality, establish whole-application speed, or benchmark a physical GPU/audio device.
