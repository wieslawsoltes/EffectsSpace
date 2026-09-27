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

Nine scenarios run against the actual Uno WebAssembly application:

1. Startup, original editable artwork, pointer scrubbing and elapsed-time playback.
2. Shape drawing, duplicate/undo/redo/delete and a real project download.
3. Timeline stopwatches, graph mode and IndexedDB recovery across reload.
4. Multi-key pointer retiming, undo selection, clipboard paste/replacement and cut rollback.
5. Bezier-handle dragging, Escape rollback and signed velocity graph rendering.
6. Effect stopwatches, actual numeric text entry, pointer marquee and effect-key clipboard mapping.
7. Luma input, masked adjustment controls, guide status, undo and saved project data.
8. Mask node/tangent dragging, cancellation and undo.
9. Nested source-time remapping, freeze, reverse and graph controls.

Tests perform real keyboard, mouse, text-entry and download events. The opt-in `?test=1` diagnostics report model state and actual rendered bounds; they expose no mutation endpoint. Screenshots and failure traces are retained. Pages repeats the suite against the public deployment after checking the live source SHA.

## Native build matrix

The same workbench compiles on Windows, macOS and Linux. Native file-dialog behavior, high-DPI input, accessibility, IME and hardware-driver paths require separate interactive validation. A successful compile is not reported as a successful native UI acceptance run.

## Scope and interpretation

The suite currently contains 226 engine/animation/renderer/compositing checks plus nine browser scenarios. Refer to the current run rather than assuming historical counts imply success. The application is not pixel-certified against Adobe After Effects and does not implement Adobe formats, video/audio processing, 3D, tracking or a professional HDR/color-management pipeline.

## Compositing regression and benchmark suite

Run `dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release`. It checks luma coverage, independent mask processing, adjustment ordering/opacity/mattes, guides, source-time remapping, native-resource invalidation, indexed transform counts and reference/optimized pixel comparisons. The CPU-raster benchmark report is uploaded under `EffectsSpace-renderer-validation/performance/compositor.json`. It measures a fixed 400-rectangle workload, not real GPU throughput. Browser compositing tests use actual file-open, numeric-entry, pointer and key events. See [COMPOSITING.md](COMPOSITING.md) for methodology.
