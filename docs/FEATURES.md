# Feature ledger

Status describes implemented behavior, not complete product parity. Check Actions for the exact source commit's validation.

| Area | Implemented | Boundary |
|---|---|---|
| Studio shell | Dense dark menu/toolbar, project/effect tabs, viewer, inspector/catalog, timeline/queue, media/delivery panels, resizable panels and workspace presets | Not pixel-certified After Effects; no arbitrary dock graph or floating native panel windows |
| Projects | Multiple compositions, media bins, embedded assets, versioned `.effects`, transactional history and recovery | No `.aep`, `.aepx`, Adobe importer or linked-media relinking |
| Layers | Solids, rounded rectangles, ellipses, stars, Bezier paths, text, images, MJPEG video, PCM audio, nulls, guides, adjustments and pre-comps | General video/audio codecs remain unsupported; unsupported video fails rendering |
| Viewer | Fit/zoom/pan, selection, move, eight scale handles, rotation, anchor compensation, shape dragging, path nodes/handles and inline text | No complete pen modifier parity, shape-group operators, perspective or 3D gizmos |
| Timeline | Frame-snapped time, browser audio-clock or silent elapsed-time playback, work area, markers, switches, clip move/trim/slip/split and row caching | Source waveforms live in Media, not timeline clips; no footage-cache strip or full Adobe column set |
| Multi-key editing | Shift/marquee selection, cross-channel group retiming, nudging, session-local copy/cut/paste and selection-aware undo | No OS/project-interchange clipboard or scale-time selection gesture |
| Clipboard mapping | Relative time, new IDs, interpolation/handles, property-role and effect-instance mapping | Missing destinations, overflow and snapped incoming collisions are rejected atomically |
| Curves | Linear/Hold/Bezier, auto-key, value and signed-velocity graphs, easing handles, Ease In/Out/Both | No roving keys, spatial tangents or coupled vector speed editor; flat/expression-driven curves have no handle editor |
| Effect animation | Stable paths, stopwatches, expressions, graph editing and live numeric fields | Catalog scalar parameters only; colors and shape paths are not animated channels |
| Expressions | Bounded scalar math, time/value/index/pi, clamp/linear/ease, wiggle and loopOut | Not JavaScript, Adobe expressions or host scripting; expression velocity is numerical |
| Transforms | Anchor, position, scale, rotation, opacity, parent chains and inverse hit transforms | 2D only; parent changes preserve local values rather than old world transforms |
| Compositing | Seventeen blend modes, alpha/luma and inverted mattes, masked adjustments, guides, recursive pre-comps and multi-sample motion blur | Adjustments require Normal blending/filter effects; no collapse transforms, 3D, HDR or linear-light pipeline |
| Masks | Add/Subtract/Intersect/None, independent opacity/feather, raster expansion/erosion, inversion, ordering and direct node/tangent editing | No animated mask paths, variable/anisotropic feather or exact analytic offsets |
| Source time | Pre-comp/video/audio remapping, scalar expressions, graph editing, freeze and continuous-curve reversal | General media codecs absent; reversing remap expressions/Hold jumps is rejected |
| Render performance | Indexed frame evaluation, parent memoization, content-checked native caches, safe direct drawing/culling and measured CPU benchmark | No hardware timestamps, persistent composition-pixel cache or dedicated WebGPU backend |
| Effects | Blur, Glow, Drop Shadow, Exposure, Brightness/Contrast, Hue/Saturation, Tint, Invert, Posterize, Fractal Noise and Vignette | Not complete Adobe effect algorithms or plug-ins; noise is a fill generator |
| Text | Inter, size/fill, multiline content and inline editing | No full shaping/paragraph engine, rich runs, text animators or font browser |
| Images | PNG/JPEG/WebP, embedded bytes and decoded cache | No RAW, layered PSD, OpenEXR, footage sequences or color-profile workflow |
| Media | Classic MJPEG AVI; mono/stereo integer/float WAVE; nested audio, gain/balance, waveform and browser output clock | No general MP4/H.264/WebM, OpenDML, compressed/surround audio or native audible preview; buffered preview is limited to 60 seconds |
| PCM compatibility | Integer 8/16/24/32, float 32/64, extensible left-aligned valid bits, zero-copy indexes and block stereo reads | Little-endian mono/stereo only; no RF64, arbitrary speaker layouts or compressed WAVE |
| Resampling | Exact same-rate integer path; legacy Linear; rate-adaptive phase-interpolated Blackman sinc; nested derivative-aware cutoff; bounded decoding windows | Sinc step limited to 32; no pitch preservation, mastering certification or smoothed rate-band transitions |
| Audio delivery | PCM16/PCM24/Float32 WAVE, 44.1/48/96 kHz UI, optional deterministic integer TPDF, float headroom, forward-only length-checked writer | No limiter, loudness normalization, noise shaping, RF64 or direct-to-persistent-storage streaming |
| Output | Transparent PNG/sequence, rational manifest, MJPEG AVI with PCM16/48 kHz, selected-format WAVE, cancellation and queue | AVI has no alpha; PNG is visual-only; no GIF, ProRes/H.264, render farm or external Media Encoder |
| Hosts | Shared Uno native/browser workbench, native pickers, IndexedDB/downloads and Pages | Native CI is compilation; browser CI is headless behavior; physical GPU/audio-device certification remains separate |

## Validation categories

Engine and animation suites check time, curves, validation, commands, selection/history, derivatives and clipboard semantics. Rendered suites check pixels, masks, effects, media frames and export. Audio Fidelity tests reconstruction, passband/stopband behavior, precision, deterministic blocks/dither, RIFF safety and warmed allocations. Independent FFmpeg checks consume the actual generated AVI and all three WAVE output formats. Browser tests use pointer, keyboard and file events on the real Uno application.

None establishes complete After Effects compatibility. See [MEDIA.md](MEDIA.md), [AUDIO-FIDELITY.md](AUDIO-FIDELITY.md), [COMPOSITING.md](COMPOSITING.md) and [ANIMATION-EDITING.md](ANIMATION-EDITING.md) for precise semantics, restrictions and benchmark methodology.
