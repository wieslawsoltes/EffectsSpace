# Portable media pipeline

EffectsSpace imports actual Motion JPEG AVI footage and PCM WAVE audio, renders video at evaluated source times, mixes nested audio, and exports AVI with sound or a separate WAVE mix. The same C# readers, mixer and exporters run in desktop and WebAssembly. This is a defined interoperable subset, not general Adobe media/project compatibility.

## Media workspace

Open Media with **Shift+F7**. **Ctrl+Alt+L** loads the original CLOCKWORK study: two seconds of actual JPEG frames and synthesized stereo PCM audio. The moving satellite, counter and sound are original project content. The sample uses the same AVI writer as export, not a video placeholder or alternate browser renderer.

Import Media / Ctrl+I accepts `.avi`, `.wav` and supported images. Import validates metadata before adding asset and layer atomically. Encoded media is embedded in `.effects`; projects cannot specify external media URLs or upload destinations. Clicking a project-bin item adds another referencing layer.

The Media panel displays codec, dimensions, rational frame rate, duration, PCM precision/container format and an exact source min/max waveform. Its buckets are constructed incrementally with contiguous stereo block reads and cached until the source changes. This is not a post-mix waveform or a waveform inside every timeline clip.

Audio Enabled mutes sound without hiding video. Gain runs from -96 dB (silence) to +24 dB. Balance attenuates the opposite stereo channel linearly; it is not equal-power spatial panning. Animate Gain / Gain Graph use the existing key editor. AudioGain and AudioPan support stopwatches, auto-key, expressions, clipboard and undo.

**Audio Delivery Settings** selects PCM16, PCM24 or Float32 WAVE, 44.1/48/96 kHz, optional integer TPDF dither and preview/export resampling quality. These are workspace preferences rather than document/history mutations. See [AUDIO-FIDELITY.md](AUDIO-FIDELITY.md) for the filter, precision and output contracts.

## Format contract

| Input | Supported | Rejected or outside scope |
|---|---|---|
| AVI | Classic little-endian RIFF AVI 1.0; one MJPEG video stream; optional one PCM audio stream; movi/rec lists and idx1 | General compressed video, OpenDML/AVIX, multiple video/audio streams or unsupported stream types |
| WAVE | Little-endian RIFF; mono/stereo 8–192 kHz; integer 8/16/24/32-bit and IEEE float 32/64-bit; extensible standard layouts and left-aligned integer valid bits | Compressed/surround audio, RF64/RIFX and arbitrary channel masks |
| Images | PNG, JPEG and WebP | RAW, PSD, OpenEXR and image-sequence footage |

Container parsing checks chunk lengths/padding, stream metadata, alignment, time bases and frame/data ranges. Encoded frame entries are slices of the original immutable source buffer, not duplicate arrays. Unsupported formats fail when consumed; an export does not substitute decorative content for undecoded video.

PCM nonfinite values become silence; decoded input floats are bounded to +/-16. Integer extensible precision may be narrower than storage and is interpreted as left-aligned valid bits. Buffer data must remain immutable for the lifetime of an index or decoded cache entry. Metadata/cache identity includes payload identity and MIME type, not only a document asset ID.

## Source-time rendering and mixing

Video source time follows offsets/stretch or the TimeRemap channel. The frame index is resolved from the source rational rate; source time outside the media is transparent. Rendering goes through the same Skia transforms, masks, effects, opacity and matte paths as other content. Cached decoded frames are keyed by asset payload and source frame. Repeated same-frame use avoids recreating the image; Skia may still defer decode/upload work, so counters are not hardware-decoder timings.

An AudioMixer prepares a tree of active source instances. Gain, balance, mute, solo and guide semantics apply through nested compositions. Each output sample uses an absolute output frame index rather than accumulated floating-point increments, preserving results across output-block partitions. Frozen/undefined remap velocity produces silence instead of repeatedly emitting DC samples. Remap derivatives multiply through nesting to select an appropriate antialias cutoff.

The workbench selects BandLimited resampling by default. A bounded, phase-interpolated Blackman sinc reconstructs fractional positions and suppresses out-of-band energy when downsampling/accelerating. Same-rate integer reads remain exact. The public mixer/exporter defaults remain Linear for source compatibility; consumers can explicitly select either mode. Varispeed changes pitch. Neither mode implements pitch-preserving stretch, a mastering limiter or automatic loudness normalization. Detailed finite-filter/rate limits are documented in the audio fidelity guide.

## Browser audio clock and native boundary

Space prepares the current work area's stereo audio, then starts an AudioBufferSourceNode through Web Audio. Preview caches the prepared buffer by revision/composition/work interval. Changing the resampling choice or editing invalidates it. The composition playhead follows the audio context clock; pause/resume and seek schedule playback from the appropriate offset. Preparation uses generation-safe cancellation to prevent an older asynchronous result from starting playback after a newer action.

Prepared preview is limited to **60 seconds**. It is not streaming AudioWorklet delivery and does not compensate physical-device output latency or certify lip sync. Browser autoplay/site restrictions are reported. A headless running audio graph validates scheduling, not audible speaker output.

**Native timeline preview remains silent.** Native builds render video and export the same audio mix but lack a native output adapter. IAudioPreview is the optional output interface. No FFmpeg binary, GPL media library or proprietary codec SDK is bundled in the application.

## Delivery

**Ctrl+Alt+M** exports Motion JPEG AVI with stereo PCM16 audio at 48 kHz. **Ctrl+Alt+W** exports standalone WAVE using the delivery settings. AVI is 8-bit sRGB and flattens alpha over the composition background; PNG preserves alpha but is visual-only.

AVI reuses one raster surface, encodes each JPEG once and interleaves the exact PCM interval. For video frame f, its audio boundary is `floor(f * audioSampleRate * videoRateDenominator / videoRateNumerator)`. This handles fractional rates without rounding every frame to a fixed audio-block length. The writer verifies complete PCM intervals and finalizes RIFF sizes/idx1 only after expected frames arrive. Cancellation does not return an incomplete file as a successful export.

Standalone WAVE supports PCM16/PCM24 saturation and optional deterministic TPDF, or Float32 headroom with a fact frame-count chunk. Exact size checks precede output allocation. Forward-only writing rejects incomplete finalization and excess/partial sample frames. The caller owns its stream. Workbench export uses a snapshot with independent metadata and shared immutable encoded buffers.

## Resource bounds

Exports are limited to 3600 video frames, 32 megapixels per frame and 256 MiB output. Encoded assets are at most 32 MiB each and 64 MiB total. The metadata cache targets 32 entries; decoded images target 128 MiB and at most 256 entries. One oversized image can exceed a caller-selected smaller target. These are application budgets, not hard driver-memory caps.

The mixer permits 512 source instances and 32 composition levels. PCM windows use 8 KiB per voice; coefficient banks have a bounded shared LRU, with last-bank references retained by active readers. RIFF traversal is limited to 100000 chunks and eight AVI record-list levels. Band-limited sampling rejects steps above 32 source frames per output frame. Full exports/downloads still reside in memory; output size is not a total-process peak-memory bound.

## Reuse

EffectsSpace.Media depends on core/animation, not Uno or Skia:

```csharp
using EffectsSpace.Media;

AviSource source = AviSource.Read(encodedBytes);
ReadOnlyMemory<byte> jpeg = source.FrameData(source.FrameAt(0.5));

var mixer = new AudioMixer(project, composition,
    quality: AudioResamplingQuality.BandLimited);
float[] stereo = new float[4096 * 2];
mixer.Mix(startTime: 0, firstFrame: 0, stereo, sampleRate: 48000);
byte[] wave = await WaveRenderer.RenderAsync(mixer, 0, 2,
    WaveEncoding.Pcm24, dither: true,
    cancellationToken: cancellationToken);
```

Use AviExporter from EffectsSpace.Skia for rendered delivery. Supply a caller-owned typeface, install native Skia assets on a headless host and dispose render resources. PcmSource is immutable; mixer/resampler windows are single-reader. Create separate readers for concurrent rendering.

## Validation

Portable-media tests cover PCM, RIFF corruption, rational timestamps, indexing, cache identity, mixing, gain/pan, nesting, cancellation and allocation. Skia media tests consume real JPEG frames and exercise remap, caching, snapshots and AVI output. Audio Fidelity adds filtering, precision and deterministic WAVE tests.

Test-only FFmpeg/ffprobe independently decodes generated AVI and each WAVE delivery format, checking codec, dimensions, exact rate/count and analytic waveform error. A self-round-trip alone is not interoperability proof. Browser tests use actual input/file events for footage scrubbing, waveform, preview clock, gain/mute undo, freeze, AVI/WAVE downloads, re-import and delivery choices. Diagnostics are read-only.

Reports under artifacts/performance distinguish cached AVI metadata lookup, compositor raster work and PCM window reads. They identify their workloads and reference paths, and are not whole-app speedups or physical GPU/audio-device benchmarks.

## Public references

- Microsoft AVI RIFF: https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference
- Microsoft WAVEFORMATEXTENSIBLE: https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ksmedia/ns-ksmedia-waveformatextensible
- W3C Web Audio: https://www.w3.org/TR/webaudio/

These references describe source formats/APIs; implemented support is limited to the contract above.
