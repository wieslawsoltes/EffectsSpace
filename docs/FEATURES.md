# Feature ledger

Status describes implemented behavior, not parity marketing. Consult CI for the exact commit's test results.

| Area | Implemented | Explicit boundary |
|---|---|---|
| Studio shell | Dense dark menu/toolbar, project/effect tabs, composition viewer, inspector/effect catalog, timeline/render queue, resizable panels and workspace presets | Not a pixel-certified After Effects replica; no floating native panel windows or arbitrary dock graph |
| Project | Multiple compositions, image bins, embedded assets, versioned `.effects` JSON, undo/redo, local recovery | No `.aep`, `.aepx`, Adobe importers or linked-media relink workflow |
| 2D layers | Solids, rounded rectangles, ellipses, stars, Bezier paths, text, raster images, nulls and nested compositions | Video/audio kinds are reserved in the model, not decoded; enabled unsupported media blocks export |
| Viewer | Fit/zoom/pan, selection, move, eight scale handles, rotation, anchor compensation, shape dragging, path points/handles, inline text | No full pen-tool modifier parity, shape-group operators, 3D gizmos or free transform/perspective tool |
| Timeline | Frame-snapped scrubbing, playback loop, work area, markers, visible/solo/lock, clip move/trim/slip/split, row culling | No audio waveforms, frame cache bar, full multi-key marquee/clipboard or full AE timeline column set |
| Animation | Base values, linear/hold/Bezier keys, stopwatches, auto-key, key retiming, value graph, fade/slide/spin presets | No spatial tangents/roving keys, speed graph, graph handle editor, preset file compatibility or expressions on all possible properties |
| Expressions | Scalar arithmetic, time/value/index/pi, common math, clamp/linear/ease, deterministic wiggle, loopOut | Not JavaScript, ExtendScript or Adobe expression compatibility; no host scripting |
| Transforms | Anchor, position, scale, rotation, opacity, parent chain, inverse hit transforms | 2D only; changing parent does not automatically preserve the previous world transform |
| Composition | Thirteen blend modes, alpha/inverted-alpha mattes, recursive pre-compositions, basic multi-sample motion blur | No luma matte, adjustment layers, collapse transforms, 3D cameras/lights, HDR or linear-light compositing |
| Masks | Editable model paths, add/subtract/intersect, inversion, combined feather | Inspector creates rectangular masks; combined masks use the maximum feather radius, not independent per-mask feather |
| Effects | Gaussian Blur, Glow, Drop Shadow, Exposure, Brightness & Contrast, Hue/Saturation, Tint, Invert, Posterize, Fractal Noise, Vignette | Glow is a colored halo without Adobe threshold/transfer controls; Fractal Noise is a fill generator; not a general plug-in ecosystem |
| Text | Bundled Inter, size/fill, multiline content, inline editing | No full HarfBuzz shaping, paragraph layout, font browser, text animators or rich character runs |
| Images | PNG/JPEG/WebP import, embedded payloads, bounded decoded cache | No RAW, PSD layer import, OpenEXR, image-sequence footage or color-profile workflow |
| Output | PNG frame, transparent PNG sequence ZIP, rational frame manifest, cancellation and queue status | No video/audio encoder, GIF, ProRes/H.264, render farm or external Adobe Media Encoder |
| Native/browser | Shared Uno C# workbench, native file pickers, IndexedDB/browser downloads, GitHub Pages | Desktop CI validates compilation; headless Chromium validates software-rendered browser behavior; device GPU certification is separate |

## Validation categories

Engine tests cover time, curves, expressions, validation, transactions and commands. Renderer tests cover alpha, blend modes, masks, nested compositions, effects, images and PNG output. Browser tests use actual pointer/keyboard interactions and read-only diagnostics. None of those categories establishes complete After Effects compatibility.

## Intended extension boundaries

A media implementation should provide source-frame and audio-clock services rather than hiding media elements in the application shell. A future GPU backend should implement the same render-plan and image-resource contracts without breaking Uno composition. New document versions should migrate explicitly rather than silently drop unknown data. Unsupported export content must remain visible as an error, never a successful but incomplete file.
