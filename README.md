<div align="center">

# EffectsSpace

### A C# motion-design workbench. One composition engine. Desktop and browser.

[![Build](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml)
[![Audio Fidelity](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/audio.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/audio.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-A994EC.svg)](LICENSE)

[Open the browser studio](https://wieslawsoltes.github.io/EffectsSpace/) · [User guide](docs/USER-GUIDE.md) · [Architecture](docs/ARCHITECTURE.md) · [Feature ledger](docs/FEATURES.md)

</div>

---

EffectsSpace is an independent motion-design and compositing application built with **Uno Platform, .NET and SkiaSharp**. Its dense studio layout operates on a real composition model: editable layers, nested compositions, keyframes, masks, mattes, effects, video frames and audio samples.

The browser application is the **same C# Uno workbench compiled to WebAssembly**, not an HTML imitation. Preview draws into Uno's existing Skia canvas. Platform file access and optional audio output are injected services.

> **Development release — `0.1.0-alpha.1`.** Implemented 2D editing and portable media support are not complete Adobe After Effects parity. MP4/H.264/WebM, native audible preview, 3D, tracking, Adobe formats/plugins and a dedicated WebGPU renderer remain absent. The [feature ledger](docs/FEATURES.md) records these boundaries explicitly.

## Compose, animate and deliver

**Compose.** Create shapes, Bezier paths, text, images and Motion JPEG footage. Manipulate transforms directly in the viewer. Use parent chains, nested compositions, seventeen blend modes, alpha/luma mattes, masked adjustment layers and independently feathered masks. Direct mask-node and tangent editing is transactional and supports cancellation.

**Animate.** Use frame-snapped time, stopwatches, auto-key, linear/hold/Bezier interpolation and bounded scalar expressions. Select, marquee, retime, copy, cut and paste multiple keys across transform/effect/audio channels. Edit temporal handles and inspect value or signed-velocity graphs. Remap source time, freeze a frame or reverse continuous remap curves.

**Work with actual media.** Import classic Motion JPEG AVI and mono/stereo PCM WAVE. The original CLOCKWORK study contains real JPEG frames and synthesized stereo samples. Video uses the same compositing path as graphics. Nested audio supports gain/balance animation and varispeed; browser preview follows a prepared Web Audio buffer's clock.

**Choose audio precision.** Media → Audio Delivery Settings exposes PCM16, PCM24 and Float32 WAVE, 44.1/48/96 kHz, integer TPDF dither and a resampling-quality choice. The workbench defaults to rate-adaptive, band-limited windowed-sinc sampling. Integer-aligned same-rate reads use an exact direct path. Float delivery preserves finite headroom instead of saturating at full scale.

**Keep and export.** Save versioned `.effects` projects with embedded media and local recovery. Export transparent PNG frames/sequences, Motion JPEG AVI with stereo PCM16/48 kHz sound, or a separate WAVE mix. Exports use snapshots, cancellation and byte/frame budgets. ORBITAL and CLOCKWORK are original editable samples, not baked application previews.

## Documentation

| Guide | Contents |
|---|---|
| [User guide](docs/USER-GUIDE.md) | Workspace, layer tools, projects and basic animation |
| [Animation editing](docs/ANIMATION-EDITING.md) | Multi-key transactions, clipboard mapping, easing and velocity |
| [Compositing](docs/COMPOSITING.md) | Adjustments, masks, mattes, source time and render caches |
| [Portable media](docs/MEDIA.md) | AVI/WAVE contracts, video rendering, audio clock and limits |
| [Audio fidelity](docs/AUDIO-FIDELITY.md) | Precision, resampling, dither, delivery settings and measurements |
| [Embedding](docs/EMBEDDING.md) | Reusable engine and Uno-control integration |
| [Validation](docs/VALIDATION.md) / [Deployment](docs/DEPLOYMENT.md) | Reproducible checks, artifacts, Pages and releases |

## Run locally

The toolchain is pinned in `global.json`: **.NET SDK 10.0.401**, **Uno SDK 6.7.30**, with managed/native **SkiaSharp 3.119.2** aligned to Uno's ABI. Python 3 and Node 22+ support build scripts and browser validation.

```sh
# Fetch checksum-verified open typography and retain its license
python3 scripts/fetch-assets.py

# Native desktop: Windows, macOS or Linux
dotnet run --project src/EffectsSpace.App -f net10.0-desktop \
  -p:EffectsSpaceDesktopOnly=true

# Real Uno WebAssembly application
dotnet workload install wasm-tools
dotnet publish src/EffectsSpace.App -f net10.0-browserwasm -c Release \
  -o artifacts/publish -p:WasmShellWebAppBasePath=/EffectsSpace/
python3 scripts/collect-site.py artifacts/publish artifacts/site
python3 scripts/serve-site.py --directory artifacts/site --port 4173
```

Open `http://127.0.0.1:4173/EffectsSpace/`.

```sh
# Engine and media regression executables
dotnet run --project tests/EffectsSpace.Tests -c Release
dotnet run --project tests/EffectsSpace.Animation.Tests -c Release
dotnet run --project tests/EffectsSpace.Skia.Tests -c Release
dotnet run --project tests/EffectsSpace.Compositing.Tests -c Release
dotnet run --project tests/EffectsSpace.Media.Tests -c Release
dotnet run --project tests/EffectsSpace.Media.Skia.Tests -c Release
dotnet run --project tests/EffectsSpace.Audio.Tests -c Release

# Independent media validation: requires test-only FFmpeg/ffprobe
python3 scripts/verify-media.py
python3 scripts/verify-audio.py

# Real keyboard, pointer, file-picker and download interactions
npm ci --ignore-scripts
npx playwright install chromium
npm run test:browser
```

The Skia test projects include Linux native assets. Native applications resolve platform-specific assets through Uno. A green native build is not native interactive or device-output certification.

## Download

Every [release](https://github.com/wieslawsoltes/EffectsSpace/releases/latest) ships a self-contained, single-file desktop app — no .NET install needed:

| OS | x64 | Arm64 |
| --- | --- | --- |
| Windows | `EffectsSpace-<version>-win-x64.zip` | `EffectsSpace-<version>-win-arm64.zip` |
| macOS | `EffectsSpace-<version>-osx-x64.tar.gz` | `EffectsSpace-<version>-osx-arm64.tar.gz` |
| Linux | `EffectsSpace-<version>-linux-x64.tar.gz` | `EffectsSpace-<version>-linux-arm64.tar.gz` |

Extract and run `EffectsSpace` (`EffectsSpace.exe` on Windows). Builds are not code-signed yet: on macOS clear the quarantine flag with `xattr -d com.apple.quarantine EffectsSpace`; on Windows choose **More info → Run anyway** in SmartScreen. Verify downloads against `SHA256SUMS.txt`. Releases also include the browser build and a source archive.

The libraries below are published to [NuGet.org](https://www.nuget.org/packages?q=EffectsSpace), e.g. `dotnet add package EffectsSpace.Core`.

## Eleven reusable packages

| Package | Responsibility |
|---|---|
| `EffectsSpace.Core` | Compositions, layers, paths, effects, channels and rational time |
| `EffectsSpace.Animation` | Curves, derivatives, easing, transforms and bounded expressions |
| `EffectsSpace.Documents` | Versioned JSON, validation, budgets and storage/audio-output contracts |
| `EffectsSpace.Editing` | Transactions, selection-aware history, keyframe clipboard and commands |
| `EffectsSpace.Rendering` | Indexed frame evaluation, render plans, hit testing and export schedules |
| `EffectsSpace.Media` | RIFF/AVI/WAVE, PCM indexing, stereo windows, sinc sampling, mixing and muxing |
| `EffectsSpace.Skia` | Compositing, filters/masks, resource caches and PNG/AVI rendering |
| `EffectsSpace.Controls` | Original studio chrome, icons, choices, splitters and numeric scrubbing |
| `EffectsSpace.Viewer` | Camera, selection, transforms, paths, inline text, mask editing and waveform |
| `EffectsSpace.Timeline` | Row-cached timeline, clip/key editing, marquee and value/velocity graphs |
| `EffectsSpace.Workbench` | Panels, media/delivery settings, transport, queue and document workflows |

All eleven libraries are published to NuGet.org with symbols; the app is a thin platform host.

## Keyboard essentials

| Workflow | Shortcut |
|---|---|
| Save / Open / Import | `Ctrl+S` / `Ctrl+O` / `Ctrl+I` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Shift+Z` |
| Duplicate / Split / Pre-compose | `Ctrl+D` / `Ctrl+Shift+D` / `Ctrl+Shift+C` |
| Key copy / cut / paste | `Ctrl+C` / `Ctrl+X` / `Ctrl+V` |
| Play / Previous frame / Next frame | `Space` / `Page Up` / `Page Down` |
| Work area start / end | `B` / `N` |
| Transform / effect properties | `P`, `S`, `R`, `T`, `A` / `E` |
| Animated properties / Easy Ease | `U` / `F9` |
| Graph / value–velocity switch | `Shift+F3` / `Shift+F4` |
| Composite / Render stats / Media | `Shift+F5` / `Shift+F6` / `Shift+F7` |
| New adjustment / Time remap / Freeze | `Ctrl+Alt+Y` / `Ctrl+Alt+T` / `Ctrl+Alt+F` |
| CLOCKWORK sample / AVI / WAVE export | `Ctrl+Alt+L` / `Ctrl+Alt+M` / `Ctrl+Alt+W` |

Text entry retains ordinary text-editing behavior. Keyframe clipboard content is session-local, not system clipboard interchange.

## Performance and correctness

The renderer indexes composition state, memoizes shared parents and reuses bounded native geometry/filter resources. Eligible plain opaque shapes avoid unnecessary isolated surfaces; text, gradients, strokes, images and nested composites retain isolation. Decoded media images are keyed by payload and source frame. AVI export reuses its raster surface. Inspector/toolbar updates avoid unnecessary reconstruction during playback.

Audio uses zero-copy encoded indexes and an 8 KiB decoded stereo window per voice. Block decoding performs one segment search for a range instead of separate searches for every sample/channel. Waveform construction uses the same exact block decoder. Warmed static sampling has allocation regression coverage; coefficient-bank creation is explicitly a cold cost.

Benchmarks are emitted under `artifacts/performance`: compositor raster submission, AVI metadata lookup and bounded PCM reads. Each report defines its input and baseline. They are **not** whole-application speedups, hardware decoder/GPU timings or comparisons against Adobe. See the linked guides before interpreting a result.

## Rendering, licensing and distribution

`SKCanvasElement` draws into Uno's existing canvas. Hardware acceleration depends on the selected host/backend and device; software fallback remains possible. PNG/AVI delivery uses CPU raster surfaces. **No dedicated WebGPU backend or universal hardware-acceleration guarantee is claimed.**

EffectsSpace is **MIT licensed**. Uno Platform core is **Apache-2.0**, SkiaSharp **MIT**, Skia **BSD-style**, and Inter **SIL OFL**. FFmpeg/ffprobe are test-only interoperability tools, not bundled runtime dependencies. Preserve actual transitive notices; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Build gates engine, rendered-pixel, signal-quality and real-browser tests before Pages can deploy its matching artifact. Pages verifies the merged commit and repeats browser tests against the public site. Desktop builds cover Windows, macOS and Linux. **Release** runs for `v*` tags or a supplied manual version. It runs the engine, media, audio and real-browser gates, publishes self-contained single-file desktop executables for Windows, macOS and Linux (x64 and arm64), builds the browser and source archives, packs all eleven versioned libraries with symbols and emits `SHA256SUMS.txt`. Tags attach all assets to a GitHub Release and publish the packages to NuGet.org with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC, no stored API key) from the protected `nuget` environment. Manual runs are dry runs: they build and upload every asset as workflow artifacts but publish nothing. Signing/notarization is not configured by default.

## Independence

EffectsSpace is not affiliated with or endorsed by Adobe. After Effects is a workflow reference, not a compatibility guarantee. No Adobe source, binaries, icons, fonts, sample artwork or proprietary SDKs are included. The studio icons and ORBITAL/CLOCKWORK studies are original project content. Read [SECURITY.md](SECURITY.md), [CONTRIBUTING.md](CONTRIBUTING.md) and the [feature ledger](docs/FEATURES.md) before production use.
