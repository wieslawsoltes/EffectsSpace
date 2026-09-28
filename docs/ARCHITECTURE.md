# Architecture

## Dependency direction

```text
Core ──► Animation ──► Rendering ──► Skia
  └────► Documents ──► Editing
                         │            │
Controls ────────────────┼────► Viewer│
     └──────────────────┴────► Timeline
                                  │
                              Workbench
                                  │
                         App: Desktop / WASM
```

Core has no Uno or Skia dependency. Animation and document validation can run on a server or in a test process. Rendering plans contain model references, world matrices, source times and evaluated opacity; they do not own graphics resources. The host injects storage through `IWorkspaceStorage`.

## Time model

Frame rates are rational numerator/denominator pairs. Keyframe and source times are seconds; user timeline edits pass through `FrameRate.Snap`. Layer intervals and export schedules are half-open: `[in, out)` and `[workStart, workEnd)`. The final playable frame is one frame before the rounded duration frame count. Timecode labels are non-drop-frame; the application does not pretend nominal labels are NTSC drop-frame.

Channels are ordered unique keyframes with a base value. Evaluation uses binary search and clamps outside the key range. Temporal Bezier interpolation solves the horizontal cubic coordinate by bounded bisection before evaluating the value coordinate. Hold interpolation remains at the left key until the right key time. Expressions are evaluated after the ordinary channel value, except `loopOut()` which remaps time into the key range.

The expression interpreter has no dynamic execution or host access. It limits source length, operations, nesting and result magnitude. Invalid expressions fall back to the underlying channel value; the inspector reports invalid syntax before accepting new text.

## Editing and history

`EditorSession` owns the project, active composition, playhead and selection. Document mutations are atomic: begin, preview repeatedly, validate, commit. Validation failure restores the previous snapshot. Time and selection are not history entries. Pointer cancellation restores the state before the drag.

History snapshots serialize model metadata but share immutable media payloads, avoiding repeated base64 copies of large images. The history budget is 100 entries or 32 MiB of serialized metadata, whichever is reached first. Imported media data is treated as immutable. Consumers must not mutate a `MediaAsset.Data` array after handing it to a session.

## Compositing

Layers are stored front-to-back and rendered back-to-front. The transform chain is anchor translation, scale, rotation, position, then parent world transforms. Parent opacity is not implicitly multiplied into child opacity. Parenting and alpha-matte edges are validated for cycles and depth.

The Skia compositor clips to the composition, uses compositing isolation where semantics require it, applies its ordered image-filter stack, applies its geometric mask, then applies an optional alpha or luma matte. Nested compositions remain transparent unless their own layer content fills them. A hidden matte can still supply alpha. Seventeen blend modes map directly to Skia blend operations.

Masks now produce independent raster coverages. Each mask is expanded/eroded, feathered, optionally inverted and opacity-scaled before its Add/Subtract/Intersect operation. None preserves metadata without affecting pixels. Square-kernel raster expansion is not an exact analytic path offset. Fractal Noise is a procedural fill generator; Vignette is a source-atop radial overlay. Other listed effects are image filters. Text uses the bundled Inter typeface and a simple multiline layout, not a complete shaping/paragraph engine.

Decoded images live in a disposal-aware LRU cache with a 128 MiB decoded-pixel target. Header dimensions are checked before image creation. The renderer limits a frame to 8192 pixels per dimension and 32 megapixels. Compositions and parent/matte dependencies have bounded nesting.

## GPU choice

Uno 6.7's `SKCanvasElement` gives access to the render canvas already selected by the host. This avoids creating a second composition surface and copying its pixels into an unrelated UI pipeline on every preview frame. It also retains the same drawing and effect implementation for desktop and WebAssembly. When the Uno host uses its accelerated path, these operations can use that backend; the application cannot guarantee which backend a particular device/driver selects.

A separate WebGPU engine was not added merely to label the project GPU-first. Without a supported zero-copy texture bridge into the Uno compositor, such a split can introduce readbacks, synchronization and divergent effect implementations. The current renderer is a permissively licensed Skia implementation, not a WebGPU implementation. Software-driver browser tests establish behavior but do not benchmark a physical GPU.

## Export and storage

PNG exports use an explicit sRGB, RGBA8888, premultiplied CPU surface, then encode PNG alpha. PNG sequences enumerate exact rational frame times, write each compressed image once into a ZIP, and include an end-exclusive manifest. The renderer can sample up to eight shutter times for basic composition-level motion blur. This is not per-layer vector motion blur or a full color-management pipeline.

Browser storage uses IndexedDB transactions. Desktop recovery writes a temporary file and atomically renames it. User exports always use browser downloads or native save pickers. Imported filenames are metadata, not filesystem paths. The application never evaluates a project as JavaScript or fetches media from project-supplied URLs.

## UI construction

The application uses custom Uno controls and custom Skia-painted composition/timeline surfaces. StudioButton, StudioIcon, StudioPanel, StudioChoice, NumericField and StudioSplitter provide the visual language. Text entry, focus, scrolling and dialogs rely on Uno platform primitives; those primitives are not rewritten operating-system controls. Timeline painting culls off-screen rows. Project and inspector controls are rebuilt only for document/selection changes; playback refreshes evaluated numeric fields without reconstructing the inspector.

## Reference documentation

- Uno direct canvas: https://platform.uno/docs/articles/controls/SKCanvasElement.html
- Uno SDK pinning: https://platform.uno/docs/articles/uno-publishing-apps.html
- Uno SDK 6.7.30 dependencies: https://www.nuget.org/packages/Uno.Sdk/6.7.30
- SkiaSharp: https://github.com/mono/SkiaSharp
- Adobe workspace terminology: https://helpx.adobe.com/after-effects/using/workspaces-panels-viewers.html

These references explain public APIs and familiar workflow terminology. They are not evidence of complete compatibility with another application.

## Indexed compositor and time-remapping continuation

See [COMPOSITING.md](COMPOSITING.md) for `CompositionFrame`, memoized parent matrices, same-source-time evaluation sharing, content-checked native resources, direct-draw restrictions, masked adjustment filter scopes and the `LayerTime` channel. `FrameExporter` excludes guide layers; nested previews do likewise. CPU submission counters are deliberately distinct from GPU timing.

## Portable media and audio output

`EffectsSpace.Media` depends on Animation/Core, not Uno or Skia. It owns RIFF traversal, AVI/WAVE metadata, zero-copy compressed-frame slices, PCM sampling, nested audio plans and the AVI muxer. Skia consumes indexed JPEG frames and supplies the reusable single-surface AVI exporter. Viewer owns the incremental source-waveform control; Workbench owns media authoring, preview preparation and queue actions. The App injects optional `IAudioPreview` output; the browser implementation uses Web Audio and its clock. Native output is not implemented, while native mixing/video/export use the same libraries.

`EditorSession.CaptureSnapshot` copies model and asset metadata but shares immutable encoded byte arrays. History compares payload identity as well as JSON metadata, detecting same-ID byte replacement. Restoring history copies asset metadata again so later renames cannot mutate old snapshots. The encoded-array immutability contract is required for renderer and media cache correctness.

See [MEDIA.md](MEDIA.md) for rational audio sample partitioning, format rejection, cache ownership, preview limits and independent decoder tests.
