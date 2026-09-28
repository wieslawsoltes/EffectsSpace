# Portable media pipeline

EffectsSpace now imports real Motion JPEG AVI footage and PCM WAVE audio, renders video at evaluated source times, mixes nested audio, and exports AVI with sound or a separate WAVE file. The same C# readers, mixer and exporter run in the desktop and WebAssembly builds. This is a defined interoperable subset, not a general-purpose Adobe codec or project-format compatibility layer.

## Use the media workspace

Open **Media** with **Shift+F7**. **Ctrl+Alt+L** opens the original **CLOCKWORK** study: two seconds of actual JPEG video frames and stereo PCM audio. The moving satellite, frame counter and synthesized sound are original project content. The sample is generated through the same AVI writer used by export; it is not a video placeholder or a separate browser rendering engine.

Use Import Media or Ctrl+I to select `.avi`, `.wav`, or supported image files. Import validates media metadata before adding the asset and its layer in a single undoable transaction. Encoded media is embedded in `.effects` projects; there are no project-supplied external URLs or server uploads. Clicking a project-bin item adds another layer referring to that asset.

The Media panel displays codec, source dimensions, rational frame rate, duration, PCM format and a source waveform. The waveform uses exact min/max buckets of the source samples, constructed incrementally and cached until the source changes. It is not a post-mix waveform or a waveform drawn inside every timeline clip.

Audio Enabled mutes/unmutes the audio component without changing the video. Gain is in decibels: -96 dB is silence and +24 dB is the maximum. Balance attenuates the opposite stereo channel linearly; it is not equal-power spatial panning. Animate Gain and Gain Graph use the existing keyframe editor. `AudioGain` and `AudioPan` are stable scalar channel addresses with stopwatches, auto-key, expressions, clipboard operations and undo.

## Supported format contract

| Input | Supported | Explicit rejection / boundary |
|---|---|---|
| AVI | Classic little-endian RIFF AVI 1.0, one Motion JPEG video stream, optional single PCM audio stream, ordinary `movi`/`rec ` records | No OpenDML/AVIX segmentation, interlaced-field MJPEG reconstruction, multiple video streams, index-dependent presentation order, dropped/empty video frames, H.264/MPEG-4 or other compressed video |
| Motion JPEG | Independently decodable JPEG frames with dimensions matching the stream header | Corrupt/unsupported JPEG bitstreams fail when consumed; changing frame dimensions is rejected |
| WAVE / AVI PCM | Mono or stereo, 8–192 kHz; integer 8/16/24/32-bit or IEEE float 32/64-bit; little-endian interleaved samples | No compressed WAVE, surround layouts, big-endian RIFX or RF64 |
| WAVEFORMATEXTENSIBLE | PCM/IEEE-float subtype, standard mono/stereo layout, valid bits equal to container bits | Partial-width valid bits and nonstandard channel masks are rejected rather than guessed |
| Images | PNG, JPEG, WebP | Existing image limits still apply |

AVI stream scale/rate values are preserved as a rational `FrameRate`. `FrameAt(sourceTime)` uses the containing frame interval rather than rounding to the nearest frame. Source time outside video is transparent; it is never replaced by an arbitrary previous frame. PCM data is indexed without conversion into a second full-file floating-point allocation. NaN/infinite float samples are sanitized to zero.

RIFF sizes, chunk alignment, list boundaries, stream counts, declared frame/sample lengths and nesting are checked. The parser scans bounded chunk records instead of trusting index offsets as pointers. JPEG decoding remains a Skia/native-decoder security boundary; valid container metadata is not a certificate that every image frame is safe.

## Source time and compositing

Video participates in normal transforms, parenting, masks, effects, blend modes, track mattes and nested compositions. The renderer requests a frame using the already evaluated source time, including stretch, offset, freeze and Time Remap. Two layers referencing the same asset at different times retain different frame-cache keys; repeated use of the same frame shares the image object.

The decoded-image cache is keyed by `(asset ID, frame index)` and checks encoded payload identity. Still images and externally supplied frames use distinct key namespaces. Replacing an asset's byte array invalidates its metadata and image entries even when the ID remains unchanged. Encoded arrays are immutable by contract; mutating a published array in place is unsupported.

Ordinary offscreen video without effects or mattes can be rejected before a JPEG image is created. Video still retains layer isolation for correct alpha/blend semantics. Preview is not a zero-copy hardware video-decoder path: compressed JPEG is copied into native Skia data on a cache miss, and Skia controls subsequent decode/upload. `VideoImageCreations` measures cached image-object creation, not hardware decoding operations or GPU memory.

## Audio mixing

`AudioMixer` builds a bounded plan of source instances and nested time/gain/balance gates. Enabled, solo, mute, work/source intervals and guide-layer rules participate. Transform opacity is not an audio-volume control. Every gate evaluates its channels in its own composition time, then maps to the next source time.

Output sample positions use an absolute integer sample-frame index, so splitting a render into blocks does not accumulate floating-point time-step drift. Stereo channels are summed without hidden normalization. PCM16 delivery saturates at full scale; there is no limiter, automatic loudness normalization or dither. Floating-point mixing can exceed full scale before encoding.

Linear interpolation implements sample-rate conversion and varispeed playback. It changes pitch with playback speed and is not a band-limited/mastering-grade resampler. Frozen or zero-speed source time yields silence rather than a sustained DC value. There are no audio effects, buses, surround mixing, pitch preservation, realtime live-input capture or full Adobe audio compatibility.

A prepared mixer references its channel values and immutable payloads; rebuild it after document edits. The workbench creates an independent metadata snapshot for preview/export. It shares encoded bytes instead of serializing large media into base64 merely to clone the model.

## Playback clock and lifecycle

In the browser, Space prepares the selected work area as bounded stereo PCM and plays it through an `AudioBufferSourceNode`. The editor follows the Web Audio context clock rather than independently advancing a UI timer beside the audio stream. Manual scrubbing restarts the buffer at the corresponding offset. Pause, project edits, cancelled preparation and window deactivation stop output. A generation token prevents an obsolete asynchronous preparation from starting after cancellation.

Prepared audio is reused for the same document revision, composition and work area. Audio preview is limited to **60 seconds**; shorten the work area for previewing longer projects. This is buffered playback, not a streaming AudioWorklet pipeline. It does not compensate physical output latency, certify lip sync on a particular device, or guarantee that visual frame submission meets realtime deadlines. Browser autoplay/site-audio restrictions are reported rather than silently ignored.

**Native timeline preview is currently silent.** Desktop builds use the same video decoding and AVI/WAVE mixing/export, but do not yet provide a native audio output adapter. `IAudioPreview` is the optional interface for integrating an output backend. No browser media element, GPL library or FFmpeg binary is bundled with the application.

## Delivery

**Ctrl+Alt+M** exports the work area as Motion JPEG AVI with mixed stereo PCM16 audio at 48 kHz. **Ctrl+Alt+W** exports the mix as a standalone stereo PCM16 WAVE. AVI is 8-bit sRGB and flattens transparency over the composition background. Use PNG for alpha. PNG frames/sequences are explicitly visual-only and do not carry audio.

AVI export enumerates the rational video frame schedule, reuses one raster surface, encodes one JPEG at a time, and interleaves each corresponding PCM block. The sample boundary for video frame `f` is:

```text
floor(f * audioSampleRate * videoRateDenominator / videoRateNumerator)
```

This distributes samples correctly at rates such as 30000/1001; it does not round every frame to the same audio-block length. A frame's supplied PCM block must match its exact interval before anything is written. The writer finalizes RIFF sizes and `idx1` only after all expected frames have been written. Cancellation never returns an intentionally incomplete AVI as a successful export.

Delivery supports up to 3600 video frames, 32 megapixels per frame and a 256 MiB output budget. Encoded assets are limited to 32 MiB each and 64 MiB total in a project. The media metadata cache targets 32 entries; images target 128 MiB of decoded pixels and at most 256 entries. One oversized image may exceed a caller-selected smaller image-cache target. These are application budgets, not hard measurements of driver-owned memory.

The prepared audio plan permits up to 512 source instances and 32 nesting levels. RIFF traversal permits at most 100000 chunks and eight nested AVI record lists. Container helpers additionally enforce bounded time bases and lengths. Do not remove those bounds to accept hostile files without replacing them with another budget mechanism.

## Reuse

The new **`EffectsSpace.Media`** package has no Uno or Skia dependency. It depends on the existing animation/core types and contains the container readers, PCM sampler, mixer and AVI writer.

```csharp
using EffectsSpace.Core;
using EffectsSpace.Media;

byte[] encoded = File.ReadAllBytes("source.avi");
AviSource source = AviSource.Read(encoded);
int frame = source.FrameAt(0.5);
ReadOnlyMemory<byte> jpeg = source.FrameData(frame); // slice of encoded, not another frame array

var mixer = new AudioMixer(project, composition);
float[] stereo = new float[4096 * 2];
mixer.Mix(startTime: 0, firstFrame: 0, stereo, sampleRate: 48000);
byte[] wave = await mixer.WaveAsync(0, 2, cancellationToken: cancellationToken);
```

Render through the reusable Skia exporter:

```csharp
using EffectsSpace.Skia;

using var renderer = new SkiaCompositor(); // configure a caller-owned typeface for text
byte[] avi = await new AviExporter(renderer).ExportAsync(
    project, composition, width: 1280, quality: 90,
    audio: true, cancellationToken: cancellationToken);
```

The workbench is still constructed with `EditorSession` and `IWorkspaceStorage`. An injected storage object can additionally implement `IAudioPreview`; the browser host demonstrates clock, cancellation and resource-lifetime integration. `AudioWaveformView` can be embedded independently of the full workbench.

## Validation and measured scope

The portable-media executable tests PCM formats, RIFF corruption, rational timestamps, zero-copy indexing, cache identity, mixing, gain/pan, nested source time, cancellation and ordinary block allocation. The Skia integration executable decodes actual JPEGs, compares rendered frames, verifies caching/remapping, exercises undo/snapshots, and creates real AVI/WAVE delivery files.

`verify-media.py` independently probes and decodes the generated AVI files using FFmpeg/ffprobe as **test-only tools**. It checks codec identifiers, frame rates/counts, dimensions and exact decoded PCM sample counts. A self-round-trip alone is not treated as interoperability proof.

Browser scenarios use actual keyboard, pointer, file chooser and download operations for sample footage, source-time scrubbing, audio clock start/pause/resume, gain/mute undo, freeze, AVI/WAVE export and re-import. Diagnostics are read-only. A running headless Web Audio graph demonstrates clock/output scheduling behavior, not that a physical speaker was audited.

`artifacts/performance/media-index.json` compares repeatedly parsing a 120-frame AVI against a prepared metadata-cache lookup, with seven rounds of 100 operations. It reports the workload and raw median times. This is not an overall playback/rendering speedup, hardware-decoder benchmark or comparison with Adobe software. Cache tests also verify that repeated same-frame rendering creates one image object and that static mixing has no per-block managed allocation after preparation.

## Public format/API references

- Microsoft, AVI RIFF File Reference: https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference
- Microsoft, WAVEFORMATEXTENSIBLE: https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ksmedia/ns-ksmedia-waveformatextensible
- W3C, Web Audio API: https://www.w3.org/TR/webaudio/

These specifications describe the source formats and browser APIs. EffectsSpace implements the subset and limitations above, not every option permitted by the standards.
