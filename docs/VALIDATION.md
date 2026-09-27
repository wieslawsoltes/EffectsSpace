# Validation

The source includes three independent validation layers. Exact outcomes belong to the linked Actions run for the same commit; this document does not hard-code a success claim ahead of execution.

## Engine tests

Run `dotnet run --project tests/EffectsSpace.Tests -c Release`.

Coverage includes rational frame timing, non-drop-frame labels, timeline coordinates, linear/hold/Bezier curves, bounded expression evaluation, malformed expression rejection, project validation, duplicate identifiers, missing references, parent/matte/composition cycles, affine transforms, transaction rollback, undo/redo, locking, duplication, split/pre-compose operations, animation stopwatches, work areas and export schedules.

The executable returns a non-zero exit code on any failed assertion. It does not swallow errors or turn failed checks into warnings.

## Renderer tests

Run `dotnet run --project tests/EffectsSpace.Skia.Tests -c Release` on Linux with the packaged native Skia dependency.

Pixel assertions cover transparent compositions, stacking, opacity, transforms, ellipses, inversion, blend modes, geometric masks, feathered alpha, hidden track mattes and nested composition transparency. Every listed effect is rendered. Additional checks cover PNG signatures and sizes, image decoding/cache disposal, the original sample, and sequence frame counts/manifests.

These tests exercise a deterministic CPU raster surface. They do not certify that a hardware driver renders identically, nor prove performance on an integrated or discrete GPU.

## Browser acceptance

`npm run test:browser` uses Playwright and the real Uno WASM output. The suite verifies startup and editable sample content, pointer scrubbing, elapsed-time playback, shape drawing, keyboard duplicate/undo/redo/delete, project downloads, timeline animation, graph mode and recovery across reload.

The test-only diagnostics are read-only and opt-in. They report real model state, actual control bounds and render errors; they cannot add layers or bypass the user interface. Screenshots and failure traces are uploaded even if a test fails.

## Desktop build matrix

Windows, macOS and Linux compile the same workbench with the native Uno host. Native drag/drop, operating-system file dialogs, high-DPI behavior, IME, accessibility and device-specific GPU paths require additional interactive validation. A successful native build is not represented as those tests.

## Known quality boundaries

The application is not pixel-certified against After Effects. It has original studio controls but still relies on Uno primitives for text entry, scrolling, focus and dialogs. See FEATURES.md for unimplemented product areas. No success statement should imply `.aep` compatibility, video/audio processing or a complete professional color-management pipeline.
