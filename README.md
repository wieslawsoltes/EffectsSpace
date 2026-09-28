<div align="center">

# EffectsSpace

### A native C# motion-design workbench. One composition engine. Desktop and browser.

[![Build](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-A994EC.svg)](LICENSE)

[Open the browser studio](https://wieslawsoltes.github.io/EffectsSpace/) · [User guide](docs/USER-GUIDE.md) · [Animation editing](docs/ANIMATION-EDITING.md) · [Media](docs/MEDIA.md) · [Architecture](docs/ARCHITECTURE.md) · [Feature ledger](docs/FEATURES.md)

</div>

---

EffectsSpace is an independent motion-design and compositing application built with **Uno Platform, .NET and SkiaSharp**. Its compact, dark studio contains a genuine editable composition model: layers, nested compositions, keyframes, expressions, masks, blend modes and ordered effects.

The browser application is the **same C# Uno workbench compiled to WebAssembly**, not an HTML imitation. The viewer and timeline draw directly into Uno's Skia canvas. Storage, file selection and download are injected platform services.

> **Development software.** This is an implemented 2D motion-design editor, not complete Adobe After Effects parity. Portable Motion JPEG AVI and PCM WAVE are supported; general MP4/H.264/WebM codecs, native audible preview, 3D compositing, tracking, Adobe project formats and plug-ins are not implemented. The [feature ledger](docs/FEATURES.md) identifies the supported behavior and its boundaries.

## Compose, animate, refine

**Compose.** Create solids, rounded rectangles, ellipses, stars, Bezier paths, text and images. Move, scale, rotate and adjust anchors directly in the viewer. Edit mask nodes/tangents, independent feather/expansion and guide layers. Use nested compositions, parenting, seventeen blend modes, alpha/luma track mattes and masked adjustment layers. The original **ORBITAL** motion study is built entirely from editable layers.

**Remap.** Animate nested-composition source time, freeze a displayed frame, or reverse temporal Bezier curves. The new Composite panel brings mask, matte, guide and source-time controls together. [Compositing guide](docs/COMPOSITING.md).

**Animate.** Scrub a frame-quantized timeline, trim and split layers, select multiple keys by Shift-click or marquee, and retime the selection as one atomic operation. Copy/cut/paste preserves relative timing, fresh identifiers and interpolation. Cross-layer effect pasting resolves equivalent effect instances rather than copying invalid source IDs.

**Shape motion.** Edit value curves with draggable temporal Bezier handles. Switch to a signed velocity graph measured in property units per second. Easy Ease, Ease In and Ease Out affect the corresponding segment endpoints. Graph scaling is held fixed during drags; Escape and lost capture roll back the whole edit. Effect parameters use the same timeline, stopwatches, clipboard and graph as transform channels.

**Refine.** Apply Gaussian Blur, Glow, Drop Shadow, Exposure, Brightness & Contrast, Hue/Saturation, Tint, Invert, Posterize, Fractal Noise and Vignette. Edit enabled states, ordering, masks and feathering. Scalar expressions provide bounded procedural motion without executable project scripts.

**Media.** Import Motion JPEG AVI and PCM WAVE, scrub actual source frames, animate audio gain/balance, and inspect source waveforms. Browser playback follows the prepared audio-buffer clock. The original **CLOCKWORK** study contains real JPEG video and stereo PCM; open it with `Ctrl+Alt+L`. Native video/mixing/export uses the same engine; native timeline preview is currently silent.

**Keep and export.** Save versioned `.effects` projects with embedded media. Recover from IndexedDB in the browser or an atomic local file on desktop. Export transparent PNG frames/sequences, Motion JPEG AVI with mixed PCM audio, or stereo WAVE. AVI export reuses one raster surface and partitions audio samples with exact rational frame timing. [Formats, limits and embedding](docs/MEDIA.md).

## Run locally

The toolchain is pinned in `global.json`: **.NET SDK 10.0.401** and **Uno SDK 6.7.30**. Managed and native Skia dependencies are aligned at **3.119.2** to match Uno's ABI.

```sh
# Independent engine, editing and renderer checks
dotnet run --project tests/EffectsSpace.Tests -c Release
dotnet run --project tests/EffectsSpace.Animation.Tests -c Release
dotnet run --project tests/EffectsSpace.Skia.Tests -c Release
dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release
dotnet run --project tests/EffectsSpace.Media.Tests -c Release
dotnet run --project tests/EffectsSpace.Media.Skia.Tests -c Release

# Fetch checksum-verified open typography and preserve its license
python3 scripts/fetch-assets.py

# Native desktop host: Windows, macOS or Linux
dotnet run --project src/EffectsSpace.App -f net10.0-desktop \
  -p:EffectsSpaceDesktopOnly=true

# Actual Uno WebAssembly application
dotnet workload install wasm-tools
dotnet publish src/EffectsSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/EffectsSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://127.0.0.1:4173/EffectsSpace/`. Full validation also uses Python 3 and Node 22+. The renderer test project includes Linux native Skia assets; the desktop application resolves platform-specific assets through Uno.

```sh
npm ci --ignore-scripts
npx playwright install chromium
npm run test:browser
```

## Reusable libraries

| Package | Responsibility |
|---|---|
| `EffectsSpace.Core` | Composition/layer models, stable animated-property addresses, rational time, paths and effects |
| `EffectsSpace.Animation` | Curve evaluation, analytical Bezier velocity, parent transforms, bounded scalar expressions |
| `EffectsSpace.Documents` | Versioned JSON, validation, resource limits and storage contracts |
| `EffectsSpace.Editing` | Atomic transactions, selection-aware history, multi-key operations and immutable clipboard |
| `EffectsSpace.Rendering` | Backend-neutral render plans, hit testing and export schedules |
| `EffectsSpace.Media` | Bounded AVI/WAVE containers, zero-copy frame indexing, PCM sampling, nested audio mixing and AVI muxing |
| `EffectsSpace.Skia` | Direct-canvas compositing, filters, masks, timestamped footage cache, PNG and AVI export |
| `EffectsSpace.Controls` | Original studio buttons, icons, panel chrome, choices, splitters and numeric scrubbing |
| `EffectsSpace.Viewer` | Composition camera, selection, transforms, path tools and inline text |
| `EffectsSpace.Timeline` | Cached row layout, clip/keyframe input, marquee selection, value/velocity graphs and easing handles |
| `EffectsSpace.Workbench` | Project bins, inspectors, effects, transport, render queue and document workflows |

All eleven libraries are packable. The application host is intentionally thin. [Embedding examples](docs/EMBEDDING.md) cover engine-only and Uno integration; the [animation guide](docs/ANIMATION-EDITING.md) includes reusable multi-key APIs. Build artifacts contain NuGet packages; that does not imply publication to nuget.org.

## Essential shortcuts

| Workflow | Shortcut |
|---|---|
| Save / Open / Import | `Ctrl+S` / `Ctrl+O` / `Ctrl+I` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Shift+Z` |
| Duplicate / Split / Pre-compose layers | `Ctrl+D` / `Ctrl+Shift+D` / `Ctrl+Shift+C` |
| Copy / Cut / Paste keys | `Ctrl+C` / `Ctrl+X` / `Ctrl+V` |
| Paste keys to current property | `Ctrl+Shift+V` |
| Select every key in current property | `Ctrl+Shift+A` |
| Previous / Next key | `J` / `K` |
| Play / Previous frame / Next frame | `Space` / `Page Up` / `Page Down` |
| Work area start / end | `B` / `N` |
| Position / Scale / Rotation / Opacity / Effect tracks | `P` / `S` / `R` / `T` / `E` |
| Easy Ease / In / Out | `F9` / `Shift+F9` / `Ctrl+F9` |
| Animation workspace / Graph / Value–velocity | `Shift+F2` / `Shift+F3` / `Shift+F4` |
| Select / Hand / Zoom / Rotate / Anchor | `V` / `H` / `Z` / `W` / `Y` |
| Rectangle / Pen / Text tool | `Q` / `G` / `Shift+T` |
| Media / CLOCKWORK study | `Shift+F7` / `Ctrl+Alt+L` |
| AVI with audio / WAVE mix | `Ctrl+Alt+M` / `Ctrl+Alt+W` |

Text fields retain normal editing shortcuts. The keyframe clipboard is session-local. Multi-key drags preserve spacing, reject occupied destination times as a group, and produce one undo step.

## Rendering and distribution

`SKCanvasElement` draws into Uno's existing render canvas, avoiding a full-frame CPU upload on every preview. Hardware acceleration depends on the selected Uno host/backend; a software fallback remains possible. PNG and AVI export deliberately use explicit CPU raster surfaces. **This is not a WebGPU backend or a guarantee of hardware acceleration on every device.**

EffectsSpace code is **MIT licensed**. Uno Platform core is **Apache-2.0**, SkiaSharp is **MIT**, Skia is **BSD-style**, and Inter is **SIL OFL**. No GPL media engine or proprietary codec SDK is bundled. Preserve resolved dependency notices; see [third-party notices](THIRD-PARTY-NOTICES.md).

## Validation and automation

Build gates deployment on engine tests, animation/derivative tests, renderer/compositing/media pixel and export tests, independent FFmpeg media decoding, and real Playwright UI interactions. FFmpeg is a test-only verifier, not an application dependency. Browser cases exercise multi-key retiming, clipboard replacement, Escape rollback, draggable easing handles, velocity views, effect stopwatches/numeric entry, marquee selection, downloads and recovery. Tests observe opt-in read-only diagnostics but mutate the application only through actual user-input events.

Desktop CI compiles Windows, macOS and Linux. Pages deploys only a successful, current-main Build artifact whose `build-info.json` matches its source SHA, then runs the same browser suite against the public URL. Release workflows build native/browser/source/package archives and checksums; external NuGet publication is an explicit separate action requiring owner-supplied credentials.

See [validation](docs/VALIDATION.md), [deployment](docs/DEPLOYMENT.md), [security](SECURITY.md), [contributing](CONTRIBUTING.md) and [changes](CHANGELOG.md). Compilation and software-rendered browser checks do not establish physical-GPU performance or complete visual parity.

## Independence

EffectsSpace is not affiliated with, endorsed by, or distributed by Adobe. Adobe After Effects is a workflow reference only. No Adobe source, binaries, icons, fonts, sample artwork or proprietary project-format implementation is included. All application icons, the ORBITAL sample and the CLOCKWORK footage/audio study are original project assets.

## Compositing and performance update

The compositor now evaluates indexed parent transforms once per frame, reuses content-checked native paths/filter chains, and avoids unnecessary isolation for eligible plain filled shapes. Text, gradients, strokes and nested blends keep their compositing isolation. Render Stats reports CPU submission and structural counters, not GPU timestamps. The compositing suite compares reference/optimized pixels and uploads a reproducible CPU-raster benchmark; see [methodology and constraints](docs/COMPOSITING.md).

Run the additional regression suite with `dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release`. Open Composite with `Shift+F5`; create an adjustment with `Ctrl+Alt+Y`; enable source remapping with `Ctrl+Alt+T`.
