# Feature ledger

Status describes implemented behavior, not complete product parity. Check Actions for validation of the exact source commit.

| Area | Implemented | Boundary |
|---|---|---|
| Studio shell | Dense dark menu/toolbar, project/effect tabs, composition viewer, inspector/catalog, timeline/queue, resizable panels and workspace presets | Not a pixel-certified After Effects replica; no arbitrary dock graph or floating native panel windows |
| Projects | Multiple compositions, image/video/audio bins, embedded assets, versioned `.effects`, transactional history and recovery | No `.aep`, `.aepx`, Adobe importer or linked-media relinking |
| Layers | Solids, rounded rectangles, ellipses, stars, Bezier paths, text, images, Motion JPEG video, PCM audio, nulls, guide layers, adjustments and nested compositions | General video/audio codecs remain unsupported; enabled unsupported video fails rendering |
| Viewer | Fit/zoom/pan, selection, move, eight scale handles, rotation, anchor compensation, shape dragging, path nodes/handles and inline text | No complete pen modifier parity, shape-group operators, perspective or 3D gizmos |
| Timeline | Frame-snapped scrubbing, browser audio-clock or silent elapsed-time playback, work area, markers, switches, clip move/trim/slip/split and cached/cullable rows | Source waveforms live in Media, not individual timeline clips; no footage frame-cache strip or full Adobe timeline columns |
| Multi-key editing | Shift selection, rectangular marquee, cross-channel group retiming, frame nudging, session-local copy/cut/paste, replacement and selection-aware undo | No OS/project-interchange keyframe clipboard or scale-time/stretch-selection gesture |
| Clipboard mapping | Relative time, new IDs, interpolation/handles, same-property or explicit current-property paste, equivalent effect-instance mapping | Missing destinations, time overflow and downsampled incoming collisions are rejected rather than coerced |
| Curves | Linear/Hold/temporal Bezier, stopwatches, auto-key, editable value graph, signed velocity graph, draggable easing handles, Ease In/Out/Both | No roving keys, spatial tangents or full coupled vector speed editor; flat and expression-driven segments have no handle editor |
| Effect animation | Stable property paths, stopwatches, auto-key, expressions, graph editing, multi-key clipboard and evaluated inspector fields | Only catalog-defined scalar parameters; colors and shape paths are not animated channels |
| Expressions | Bounded scalar math, time/value/index/pi, clamp/linear/ease, deterministic wiggle and loopOut | Not JavaScript, ExtendScript, Adobe expression compatibility or host scripting; velocity is numerically estimated |
| Transforms | Anchor, position, scale, rotation, opacity, parent chains and inverse hit transforms | 2D only; parent changes preserve local values, not the old world transform |
| Compositing | Seventeen blend modes, alpha/luma and inverted mattes, masked adjustment layers, guide layers, recursive pre-comps and multi-sample motion blur | Adjustments require Normal blending and filter effects; no collapse transforms, 3D cameras/lights, HDR or linear-light pipeline |
| Masks | Add/Subtract/Intersect/None, independent opacity/feather, raster expansion/erosion, inversion, order and WYSIWYG node/tangent editing | No mask-path animation, variable per-vertex feather, anisotropic feather or exact analytic offset curves |
| Source time | Nested-composition/video/audio Time Remap channels, scalar expressions, graph editing, freeze, continuous-curve reversal and guide-aware export | Only portable AVI/PCM media decoding; reversing remap expressions/Hold jumps requires baking and is explicitly rejected |
| Rendering performance | Indexed frame evaluation, shared-parent matrices, exact-content native path/filter caches, safe direct drawing/culling and measured CPU benchmark | No hardware GPU timestamps, persistent composition-pixel cache or dedicated WebGPU backend; decoded media images have a separate bounded cache |
| Effects | Gaussian Blur, Glow, Drop Shadow, Exposure, Brightness & Contrast, Hue/Saturation, Tint, Invert, Posterize, Fractal Noise, Vignette | Glow is a colored halo without Adobe threshold controls; Fractal Noise is a fill generator; no Adobe plug-ins |
| Text | Bundled Inter, size/fill, multiline content and inline editing | No complete shaping/paragraph engine, rich runs, text animators or font browser |
| Images | PNG/JPEG/WebP import, embedded bytes and bounded decoded cache | No RAW, layered PSD, OpenEXR, footage image sequences or color-profile workflow |
| Media | Classic Motion JPEG AVI, mono/stereo integer/float PCM WAVE, nested audio mixing, gain/balance automation, source waveform and browser audio preview | No general MP4/H.264/WebM, OpenDML, compressed/surround audio, native audible preview, pitch preservation or mastering-grade resampling; browser preview is buffered and limited to 60 seconds |
| Output | Transparent PNG frame/sequence, rational manifest, Motion JPEG AVI with mixed PCM16/48 kHz sound, stereo WAVE, cancellation and queue status | AVI has no alpha; PNG is visual-only; no GIF, ProRes/H.264, render farm or external Media Encoder |
| Hosts | Shared Uno C# native/browser workbench, native file pickers, IndexedDB/downloads and GitHub Pages | Native CI checks compilation; headless Chromium checks software-rendered behavior; physical GPU/device validation remains separate |

## Validation categories

Engine tests cover time, curves, expressions, validation and commands. Animation tests add derivative correctness, multi-key selection/history, group collision handling and clipboard semantics. Renderer tests cover pixels, alpha, masks, effects, images and exports. Browser tests interact with the actual Uno controls using keyboard, pointer and file events.

None of those categories establishes complete After Effects compatibility. Unsupported codecs fail when the requested pipeline consumes them. PNG output is visual-only and omits audio by definition. See [MEDIA.md](MEDIA.md) for exact supported container/codec combinations and preview/output limits.

Detailed operation semantics, adjustment restrictions, guide/matte references and benchmark methodology are in [COMPOSITING.md](COMPOSITING.md).
