<div align="center">

# EffectsSpace

### A native C# motion-design workbench. One composition engine. Desktop and browser.

[![Build](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-A994EC.svg)](LICENSE)

[Open the browser studio](https://wieslawsoltes.github.io/EffectsSpace/) · [User guide](docs/USER-GUIDE.md) · [Architecture](docs/ARCHITECTURE.md) · [Feature ledger](docs/FEATURES.md)

</div>

---

EffectsSpace is an independent motion-design and compositing application built with **Uno Platform, .NET and SkiaSharp**. It pairs a familiar dense studio layout with a genuine composition model: editable layers, nested compositions, keyframes, expressions, masks, blend modes and an ordered effect stack.

The browser application is the **same C# Uno workbench compiled to WebAssembly**, not an HTML mock-up. Preview rendering draws into Uno's existing Skia canvas. Project storage, import and download are injected platform services.

> **Development release — `0.1.0-alpha.1`.** This is an implemented 2D motion-design editor, not complete Adobe After Effects parity. Video/audio decoding, 3D compositing, tracking, Adobe project formats and plug-ins are not implemented. Review the [feature ledger](docs/FEATURES.md) before using it for production work.

## Work in a real composition

**Compose.** Create shapes, Bezier paths, text and image layers. Move, scale, rotate and adjust anchors directly in the viewer. Use nested compositions, parenting, thirteen blend modes and alpha track mattes. The original **ORBITAL** motion study is constructed from editable layers rather than a baked preview.

**Animate.** Scrub a frame-quantized timeline, trim and split layers, move keyframes, edit value graphs and apply linear, hold or temporal Bezier interpolation. Use a bounded scalar expression language for procedural motion. Every editing operation participates in transactional undo/redo.

**Refine.** Apply Gaussian Blur, Glow, Drop Shadow, Exposure, Brightness & Contrast, Hue/Saturation, Tint, Invert, Posterize, Fractal Noise and Vignette. Edit mask operations, inversion and feathering. Inspect the actual property values used by the renderer.

**Keep and export.** Save versioned `.effects` projects with embedded images. Recover work from IndexedDB in the browser or an atomic local file on desktop. Export transparent PNG frames or work-area PNG sequences with a rational frame-rate manifest.

## Run locally

The repository pins the toolchain in `global.json`: **.NET SDK 10.0.401** and **Uno SDK 6.7.30**. Managed and native Skia dependencies are aligned at **3.119.2** to match Uno's ABI.

```sh
# Engine and renderer tests
 dotnet run --project tests/EffectsSpace.Tests -c Release
 dotnet run --project tests/EffectsSpace.Skia.Tests -c Release

# Download open-licensed typography; the license is retained with the font
 python3 scripts/fetch-assets.py

# Native desktop host: Windows, macOS or Linux
 dotnet run --project src/EffectsSpace.App -f net10.0-desktop \
   -p:EffectsSpaceDesktopOnly=true

# Real Uno WebAssembly application
 dotnet workload install wasm-tools
 dotnet publish src/EffectsSpace.App -f net10.0-browserwasm -c Release \
   -o artifacts/publish -p:WasmShellWebAppBasePath=/EffectsSpace/
 python3 scripts/collect-site.py artifacts/publish artifacts/site
 python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://127.0.0.1:4173/EffectsSpace/`. Use Python 3, Node 22+ and the pinned .NET SDK for the full validation workflow. The renderer test project includes Linux native Skia assets; desktop application builds resolve their platform-specific assets through Uno.

```sh
npm install --ignore-scripts
npx playwright install chromium
npm run test:browser
```

## Reusable packages

| Package | Responsibility |
|---|---|
| `EffectsSpace.Core` | Compositions, layers, paths, effects, keyframes, rational time and timeline geometry |
| `EffectsSpace.Animation` | Curve evaluation, easing, parent transforms and bounded scalar expressions |
| `EffectsSpace.Documents` | Versioned JSON, validation, resource limits and storage contracts |
| `EffectsSpace.Editing` | Atomic transactions, undo/redo, commands and the original sample project |
| `EffectsSpace.Rendering` | Backend-neutral render plans, hit testing and export schedules |
| `EffectsSpace.Skia` | Direct-canvas compositing, filters, masks, image cache and PNG export |
| `EffectsSpace.Controls` | Studio buttons, vector icons, panel chrome, choices, splitters and numeric scrubbing |
| `EffectsSpace.Viewer` | Composition camera, selection, transforms, path tools and inline text |
| `EffectsSpace.Timeline` | Virtualized timeline painting, clip/keyframe interaction and value graphs |
| `EffectsSpace.Workbench` | Project bins, inspectors, effect catalog, transport, queue and document workflows |

All ten libraries are packable. The application host is intentionally thin. [Embedding examples](docs/EMBEDDING.md) show engine-only and Uno-control integration.

## Keyboard essentials

| Workflow | Shortcut |
|---|---|
| Save / Open / Import | `Ctrl+S` / `Ctrl+O` / `Ctrl+I` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Shift+Z` |
| Duplicate / Split / Pre-compose | `Ctrl+D` / `Ctrl+Shift+D` / `Ctrl+Shift+C` |
| Play / Previous frame / Next frame | `Space` / `Page Up` / `Page Down` |
| Work area start / end | `B` / `N` |
| Position / Scale / Rotation / Opacity | `P` / `S` / `R` / `T` |
| Animated properties / Easy Ease / Graph | `U` / `F9` / `Shift+F3` |
| Select / Hand / Zoom / Rotate / Anchor | `V` / `H` / `Z` / `W` / `Y` |
| Rectangle / Pen / Text tool | `Q` / `G` / `Shift+T` |

Text fields retain ordinary editing behavior. Timeline and viewer interactions have separate transaction lifetimes, so one drag is one undo step.

## Rendering and licensing

`SKCanvasElement` uses Uno's existing render canvas and avoids a full-frame CPU upload for every preview. Hardware acceleration depends on Uno's selected host/backend; a software fallback remains possible. PNG export deliberately uses a deterministic CPU surface. **This release does not claim a WebGPU backend or guaranteed GPU acceleration on every machine.**

The application and libraries are **MIT licensed**. Uno Platform and SkiaSharp are MIT licensed; Skia uses a BSD-style license; Inter uses SIL OFL. No GPL media engine or proprietary codec SDK is bundled. See [third-party notices](THIRD-PARTY-NOTICES.md).

## Automation and validation

The Build workflow runs engine tests, publishes the real WASM application, runs Playwright interactions and packages the reusable libraries. Desktop builds cover Windows, macOS and Linux. Pages publishes only a successful, current-main Build artifact with matching `build-info.json` provenance, then repeats browser tests against the public URL. Release automation produces source, browser, desktop and NuGet artifacts without requiring a package-publishing secret.

See [validation](docs/VALIDATION.md), [deployment](docs/DEPLOYMENT.md), [security](SECURITY.md) and [contributing](CONTRIBUTING.md). A green compile is not a claim of complete visual parity or hardware-driver certification.

## Independence

EffectsSpace is not affiliated with, endorsed by, or distributed by Adobe. Adobe After Effects is a workflow reference only. No Adobe source, binaries, icons, fonts, sample artwork or proprietary project-format implementations are included. All editor icons and the ORBITAL sample were created specifically for this project.
