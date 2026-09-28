# Compositing, masks and source time

The Composite tab groups layer compositing and time controls. Open it with **Shift+F5**. Its operations edit the same reusable model and transaction history as the timeline; no preview-only alternate document is used.

## Adjustment layers

Create one from Layer → New Adjustment Layer, the Composite tab, or **Ctrl+Alt+Y**. It covers the full composition initially and affects only layers below it. Apply a catalog effect from Effect Controls or Effects & Presets; Composite also offers Invert and Exposure shortcuts. The adjustment's transform, masks, matte and opacity define its coverage. Layers above it remain unaffected.

Opacity interpolates between the original composite and the effect result. For premultiplied pixels, original `C`, filtered `F`, and mask coverage `M`, the result is `F*M + C*(1-M)`. This applies to alpha as well as color, so lowering adjustment opacity does not fade the entire composition. Multiple adjustment layers respect stacking order.

Implementation opens effect scopes top-to-bottom and closes them while rendering bottom-to-top. Content is drawn once rather than repeatedly rendering a growing prefix for every adjustment. The scope uses Skia image-filter composition, with an independently recorded coverage picture. No full-frame CPU readback is introduced into the preview path.

Current adjustment restrictions are explicit: Normal layer blending only; Fractal Noise and Vignette are fill/overlay generators and are rejected on adjustments; an adjustment cannot itself supply a track matte. Unsupported combinations fail document validation and roll back the edit. These restrictions are not full After Effects adjustment-layer parity.

## Track mattes and blend modes

Properties and Composite expose Alpha, Alpha Inverted, Luma and Luma Inverted. A hidden source layer can supply a matte. Source transforms, opacity, masks and effects participate in the matte. Alpha modes use coverage; luma modes convert the rendered matte's luminance to alpha using Skia's luma color filter, including source transparency. Inverted modes complement the resulting coverage.

Seventeen layer blend modes are available. Hue, Saturation, Color and Luminosity extend the original Normal, Multiply, Screen, Add, Overlay, Soft Light, Hard Light, Difference, Darken, Lighten, Color Dodge, Color Burn and Exclusion set. All use the existing Skia backend. The project remains an 8-bit sRGB workflow, not HDR, linear-light or color-managed Adobe parity.

## Independent masks

Add Mask creates an inset rectangular path. Composite exposes each mask's operation, opacity, feather, signed expansion, inversion and ordering. Operations are Add, Subtract, Intersect and None. None preserves editable path metadata without changing coverage.

Each mask is rasterized independently, expanded or eroded, then feathered. Its opacity scales its resulting coverage. Add combines coverages by source-over union, Subtract removes coverage, and Intersect multiplies coverage. Unlike the previous implementation, a soft mask cannot cause another mask to inherit its feather radius.

Expansion uses raster dilation/erosion with a square kernel, measured in local pixels; it is not an exact analytic offset curve. Feather is isotropic Gaussian softness. Inversion complements the processed coverage within the layer rectangle. Per-vertex variable feather, independent horizontal/vertical feather, mask-path animation and the full Adobe mask-mode set remain outside the implementation.

Choose **Mask name · Edit Path** to edit in the composition. Drag a square node to move it. Alt-drag a node to create a symmetric tangent pair. Drag a circular handle independently, or hold Shift to mirror the opposite handle. The overlay uses the layer's full parent transform and the viewer camera; handle hit targets remain screen-sized. Enter finishes editing; Escape cancels the active drag, or closes the editor when no drag is active. Lost capture and disposal cancel an unfinished transaction.

The reusable `MaskEditView` is separate from the workbench and can be overlaid on a `CompositionView`. The host owns and disposes both. Each gesture creates one undo entry, never a sequence of pointer-move entries.

## Guide layers

Guide Layer marks content that is visible in the directly opened composition preview but omitted from ordinary layer rendering in nested compositions and exports. A guide's solo flag does not suppress ordinary exported layers. Explicit matte references still evaluate their referenced source, including a source marked as a guide; guide status controls ordinary stacking, not reference resolution.

Guide status is persisted in `.effects`, duplicated with the layer, and included in undo/redo. It is distinct from title/action-safe guides, which are viewer overlays and never document content.

## Source-time remapping

Select a nested composition and use Time Remap or **Ctrl+Alt+T**. The channel uses composition seconds for key times and source seconds for values. Initial linear keys preserve the source offset/stretch over the layer's visible frame interval. The final key is at the last visible frame, not the exclusive out point.

Change Source (s), enable auto-key, or use Remap Graph to edit it with the existing keyframe/Bezier tools. The stable property address is `TimeRemap`; it participates in selection, cut/copy/paste, easing, duplication and clip moves. Expressions use the existing bounded scalar grammar.

Freeze or **Ctrl+Alt+F** replaces remapping with the currently evaluated constant source time. Reverse reflects the remap keys and reverses their temporal Bezier controls, preserving the reflected curve. Reversal of expression-driven or discontinuous Hold remaps is rejected rather than silently changing boundary semantics. Disable Remap restores the ordinary source-offset/stretch mapping while retaining the stored channel data.

Source time outside the nested composition is transparent; it is not silently clamped to a frame. Remapping is implemented for real nested-composition rendering. Motion JPEG AVI frames and PCM audio now use the same source-time mapping; see [MEDIA.md](MEDIA.md). General-purpose codecs remain outside this implementation.

## Renderer evaluation and resource ownership

`CompositionFrame` indexes layers once for a composition/time pair and memoizes each parent matrix. Original stacking indices are preserved for `index` expressions. Shared nested instances at the same source time reuse that evaluation snapshot, but retain separate rendering and isolated blend contexts. No frame snapshot survives a top-level Render call, so in-place model edits cannot reuse stale matrices.

Native paths and effect-filter chains use exact-content-checked cache entries. Mutable path points and handles are compared before reuse; effects compare evaluated parameter values and order. The cache is trimmed to 512 layer entries and a 16 MiB estimated metadata/geometry target after a frame. This is not a measurement or hard cap of driver-owned texture memory. Decoded image caching separately uses a 128 MiB target and checks payload identity as well as asset ID. Imported byte arrays must remain immutable.

Direct drawing is limited to opaque, plain-filled geometric layers with Normal blending, no effects, masks, matte, gradient or stroke. Text, images, gradients, strokes and nested compositions retain isolation. Offscreen rejection is limited to safely bounded geometry. The optimized sample image is compared against the isolated reference with a one-byte per-channel tolerance. Cached filter or path invalidation must not alter output.

**Shift+F6** or Render Stats displays CPU submission time, draw/isolation counts and cache/evaluation counts. These are not GPU timestamps or device FPS measurements.

## Reuse

```csharp
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Rendering;
using EffectsSpace.Skia;

var session = new EditorSession(SampleProject.Create());
var adjustment = session.AddLayer(LayerKind.Adjustment);
session.AddEffect(EffectKind.Exposure);
session.AddMask();
session.Edit("Soft adjustment", () =>
{
    adjustment.Transform.Opacity.Value = 60;
    adjustment.Masks[0].Feather = 24;
    adjustment.Masks[0].Opacity = 80;
});

var frame = new CompositionFrame(session.Composition, session.Time);
using var renderer = new SkiaCompositor();
byte[] png = new FrameExporter(renderer).Png(
    session.Project, session.Composition, session.Time);
```

Supply a suitable typeface and platform native Skia assets when embedding the headless renderer. A caller-supplied `SKCanvas` and typeface remain caller-owned; the compositor owns and disposes cached native graphics resources.

## Validation and performance methodology

`dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release` runs rendered-pixel assertions, reference-vs-optimized comparisons, source-time/undo checks, cache invalidation checks, and parent-evaluation counts. The browser suite adds actual pointer, keyboard, numeric-entry, file-open and save operations for compositing, mask editing and remapping.

The executable writes `artifacts/performance/compositor.json`: 400 rounded rectangles at 512×288 on a CPU raster surface, five warm-up frames, seven rounds of five measured frames. It reports median milliseconds and current-thread managed allocation per frame. The reference disables direct drawing and native path/filter caches; both variants use indexed frame evaluation. Thus this benchmark isolates these rendering optimizations and is not a complete old-version/new-version comparison. It is not a physical-GPU benchmark, a universal speedup claim or a comparison against Adobe software. Actions uploads the report with the exact tested revision.
