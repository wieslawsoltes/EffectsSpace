# Changelog

## Portable media update — 2026-09-28

- Added the MIT-licensed `EffectsSpace.Media` package: bounded RIFF/WAVE and Motion JPEG AVI parsing/writing, zero-copy encoded-frame indexing, PCM integer/float sampling and nested-composition audio mixing.
- Connected real AVI source frames to Skia transforms, masks/effects, remapping/freezing and the bounded image cache. Added mixed-audio AVI and WAVE exports, exact rational sample partitioning and one reused AVI raster surface.
- Added Media controls, gain/balance channels, source waveforms, the original CLOCKWORK footage/audio study and buffered Web Audio clock-driven preview. Native preview remains silent; general MP4/H.264/WebM codecs remain unsupported.
- Fixed history and export snapshots to copy asset metadata while sharing immutable payloads, and to detect asset-byte replacement without metadata changes.
- Added portable, rendered-media, independent FFmpeg and actual browser-input validation; included the suites in build/release/package gates.


## Unreleased — compositing parity and performance

### Added

- Luma and inverted-luma track mattes with source transparency and transformed source coverage.
- Masked adjustment layers with opacity-preserving interpolation and ordered compositing scopes.
- Hue, Saturation, Color and Luminosity blend modes.
- Independent mask feather, opacity, signed raster expansion, None mode and ordering.
- Reusable WYSIWYG mask node/tangent editor with transaction/cancel/undo behavior.
- Guide layers excluded from ordinary nested rendering and PNG/sequence exports.
- Nested-composition Time Remap channels, freeze and continuous Bezier time reversal.
- Composite panel, menu/keyboard integration, read-only render counters and interaction tests.

### Performance and correctness

- Index layers/assets/compositions and evaluate each shared parent matrix once per frame.
- Share evaluation for repeated nested source/time pairs without sharing blend backdrops.
- Cache native geometry and filter chains with exact mutable-content checks and bounded retention.
- Cull safely bounded offscreen plain shapes and avoid their unnecessary save-layer surfaces.
- Preserve isolation for text, gradients, strokes, opacity groups and nested composition blends.
- Reuse image LRU entries without per-hit record allocation and invalidate same-ID changed payloads.
- Skip unchanged button/icon visual-state invalidation during playback.
- Add reference-pixel comparisons and a reproducible CPU-raster benchmark to Build and release gates.


## Unreleased — animation editing continuation

### Added

- Multi-key selection with Shift-click, marquee and current-property selection commands.
- Atomic group retiming/value drags, frame nudging, collision preflight and cancellation rollback.
- Immutable session-local keyframe clipboard, cut/copy/paste and explicit paste-to-current-property.
- Relative-time and frame-rate handling with rejection of incoming snap collisions and out-of-range pastes.
- Stable effect-channel addresses and equivalent effect-kind/occurrence mapping across layers.
- Effect-parameter timeline tracks, stopwatches, expressions, graphs and live evaluated numeric fields.
- Analytical Bezier velocity, signed velocity graphs and labeled numerical expression estimates.
- Draggable temporal Bezier controls and endpoint-correct Easy Ease / In / Out.
- Previous/next-key commands, animation toolbar controls and an in-app animation guide.
- Independent animation tests and real-browser coverage of the new interactions.

### Changed

- Undo/redo snapshots retain layer/key selection and current property.
- Timeline row layout is cached between structural changes instead of rebuilt for every playback tick.
- Timeline clip offsets include effect keyframes in their group time constraints.
- Key selection does not reconstruct the full property inspector on every click.
- Build/Pages validation now gates on the animation editing tests as well as the original engine and renderer suites.

## 0.1.0-alpha.1 — initial implementation

- Shared Uno desktop/WebAssembly workbench and ten reusable packable libraries.
- Original editable ORBITAL sample, 2D composition engine, timeline, basic curves and scalar expressions.
- Skia effects, masks, alpha mattes, image import, project recovery and PNG/sequence output.
- Build, desktop, Pages, release and optional NuGet workflows.

This changelog records implemented changes, not Adobe feature or format compatibility. See the feature ledger for remaining scope.
