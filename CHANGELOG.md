# Changelog

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
