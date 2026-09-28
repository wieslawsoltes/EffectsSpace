# Validation

Validation is split into model/animation, rendered pixels, actual browser interactions and native compilation. Results must be associated with the exact Actions run and source commit; a screenshot or green check from an older implementation is not evidence for a newer change.

## Engine tests

```sh
dotnet run --project tests/EffectsSpace.Tests -c Release
```

The 97 checks cover rational timing, labels, timeline coordinates, curves, expressions, validation, dependency cycles, transforms, rollback, history, layer operations, stopwatches, work areas and export schedules. A failed assertion returns a non-zero process exit code.

## Animation editing tests

```sh
dotnet run --project tests/EffectsSpace.Animation.Tests -c Release
```

The 38 checks cover stable effect paths, key selection/history, immutable clipboard contents, cross-layer/parameter mapping, atomic failure, time/value bounds, locked layers, cross-channel collision handling, easing endpoints and velocity derivatives. The derivative suite includes finite differences as an independent numerical check, finite degenerate endpoint limits and undefined slopes/jumps.

The Build workflow runs this suite before deployment eligibility. It is also independently available through the Animation workflow.

## Renderer tests

```sh
dotnet run --project tests/EffectsSpace.Skia.Tests -c Release
```

The 37 checks cover transparent compositions, stacking, opacity, transforms, geometry, blend modes, masks, feather, hidden mattes, nested transparency, all catalog effects, image cache behavior, PNG bytes/dimensions and sequence manifests. Linux native Skia assets are supplied by the test project.

These are CPU raster checks. They do not certify a physical graphics driver or establish performance on a discrete/integrated GPU.

## Browser acceptance

```sh
npm ci --ignore-scripts
npx playwright install chromium
npm run test:browser
```

Twelve scenarios run against the actual Uno WebAssembly application:

1. Startup, original editable artwork, pointer scrubbing and elapsed-time playback.
2. Shape drawing, duplicate/undo/redo/delete and a real project download.
3. Timeline stopwatches, graph mode and IndexedDB recovery across reload.
4. Multi-key pointer retiming, undo selection, clipboard paste/replacement and cut rollback.
5. Bezier-handle dragging, Escape rollback and signed velocity graph rendering.
6. Effect stopwatches, actual numeric text entry, pointer marquee and effect-key clipboard mapping.
7. Luma input, masked adjustment controls, guide status, undo and saved project data.
8. Mask node/tangent dragging, cancellation and undo.
9. Nested source-time remapping, freeze, reverse and graph controls.
10. Motion JPEG source frames, source waveform and browser audio-clock start/pause/resume.
11. Audio gain/mute undo and real video freeze/remap.
12. AVI and WAVE downloads, file-picker re-import and media project persistence.

Tests perform real keyboard, mouse, text-entry and download events. The opt-in `?test=1` diagnostics report model state and actual rendered bounds; they expose no mutation endpoint. Screenshots and failure traces are retained. Pages repeats the suite against the public deployment after checking the live source SHA.

## Native build matrix

The same workbench compiles on Windows, macOS and Linux. Native file-dialog behavior, high-DPI input, accessibility, IME and hardware-driver paths require separate interactive validation. A successful compile is not reported as a successful native UI acceptance run.

## Scope and interpretation

The original suites contain 226 engine/animation/renderer/compositing checks; two portable-media suites add container, audio, real-frame and export regression checks. Refer to the current run for exact counts and outcomes. The application is not pixel-certified against Adobe After Effects and does not implement Adobe formats, general-purpose codecs, 3D, tracking or a professional HDR/color-management pipeline.

## Compositing regression and benchmark suite

Run `dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release`. It checks luma coverage, independent mask processing, adjustment ordering/opacity/mattes, guides, source-time remapping, native-resource invalidation, indexed transform counts and reference/optimized pixel comparisons. The CPU-raster benchmark report is uploaded under `EffectsSpace-renderer-validation/performance/compositor.json`. It measures a fixed 400-rectangle workload, not real GPU throughput. Browser compositing tests use actual file-open, numeric-entry, pointer and key events. See [COMPOSITING.md](COMPOSITING.md) for methodology.

## Portable-media and independent decoder validation

```sh
dotnet run --project tests/EffectsSpace.Media.Tests -c Release
dotnet run --project tests/EffectsSpace.Media.Skia.Tests -c Release
python3 scripts/verify-media.py
```

The first executable covers PCM formats, RIFF corruption and bounds, exact AVI/PCM timing, zero-copy frame slices, cache invalidation, nested gain/pan/source-time mixing, block partitioning, cancellation and allocation. Container-only tests explicitly use synthetic JPEG markers; they do not claim those are decoded images.

The second executable generates and decodes real JPEG video and PCM, verifies source-frame pixels and caching, tests snapshot/undo isolation and outputs actual AVI/WAVE files. `verify-media.py` uses independent FFmpeg/ffprobe binaries available only in the test environment, decodes every frame, and asserts codec identifiers, frame rate, frame count and PCM sample length. Runtime packages do not depend on FFmpeg.

Media artifacts and the metadata-lookup microbenchmark are uploaded with the tested revision. The benchmark compares repeated indexing to cached lookup, not overall application speed. Headless Web Audio tests check scheduling/clock state and exported sample data, not physical speaker output. Native audible preview is not implemented. See [MEDIA.md](MEDIA.md).
