<div align="center">

# EffectsSpace

### A C# motion-design workbench. One composition engine. Desktop and browser.

[![Build](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/build.yml)
[![Desktop](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/desktop.yml)
[![Pages](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/pages.yml)
[![Audio Fidelity](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/audio.yml/badge.svg)](https://github.com/wieslawsoltes/EffectsSpace/actions/workflows/audio.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-A994EC.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Core.svg?label=NuGet)](https://www.nuget.org/packages/EffectsSpace.Core)
[![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Core.svg)](https://www.nuget.org/packages/EffectsSpace.Core)

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

## NuGet packages

The studio is built from eleven MIT-licensed packages that are versioned and released together; the app itself is a thin platform host. Seven packages (`Core` through `Skia`) target plain `net10.0` and have no UI dependency; only `EffectsSpace.Skia` pulls in SkiaSharp. `Controls`, `Viewer`, `Timeline` and `Workbench` are Uno Platform libraries targeting `net10.0-desktop` and `net10.0-browserwasm`. Every package ships symbols to NuGet.org (`.snupkg`) with SourceLink.

```sh
dotnet add package EffectsSpace.Core --prerelease
```

| Package | Version | Downloads | Description |
| --- | --- | --- | --- |
| [EffectsSpace.Core](https://www.nuget.org/packages/EffectsSpace.Core) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Core.svg)](https://www.nuget.org/packages/EffectsSpace.Core) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Core.svg)](https://www.nuget.org/packages/EffectsSpace.Core) | Compositions, layers, channels, keyframes, paths, effects and rational time |
| [EffectsSpace.Animation](https://www.nuget.org/packages/EffectsSpace.Animation) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Animation.svg)](https://www.nuget.org/packages/EffectsSpace.Animation) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Animation.svg)](https://www.nuget.org/packages/EffectsSpace.Animation) | Keyframe curves, velocity, transform evaluation and bounded scalar expressions |
| [EffectsSpace.Documents](https://www.nuget.org/packages/EffectsSpace.Documents) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Documents.svg)](https://www.nuget.org/packages/EffectsSpace.Documents) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Documents.svg)](https://www.nuget.org/packages/EffectsSpace.Documents) | Versioned JSON, validation, budgets and storage/audio-output contracts |
| [EffectsSpace.Editing](https://www.nuget.org/packages/EffectsSpace.Editing) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Editing.svg)](https://www.nuget.org/packages/EffectsSpace.Editing) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Editing.svg)](https://www.nuget.org/packages/EffectsSpace.Editing) | Atomic transactions, undo/redo, layer/keyframe commands and sample compositions |
| [EffectsSpace.Rendering](https://www.nuget.org/packages/EffectsSpace.Rendering) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Rendering.svg)](https://www.nuget.org/packages/EffectsSpace.Rendering) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Rendering.svg)](https://www.nuget.org/packages/EffectsSpace.Rendering) | Renderer-neutral frame evaluation, hit testing, budgets and export schedules |
| [EffectsSpace.Media](https://www.nuget.org/packages/EffectsSpace.Media) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Media.svg)](https://www.nuget.org/packages/EffectsSpace.Media) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Media.svg)](https://www.nuget.org/packages/EffectsSpace.Media) | RIFF/WAVE and Motion JPEG AVI, PCM indexing, sinc resampling, mixing and muxing |
| [EffectsSpace.Skia](https://www.nuget.org/packages/EffectsSpace.Skia) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Skia.svg)](https://www.nuget.org/packages/EffectsSpace.Skia) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Skia.svg)](https://www.nuget.org/packages/EffectsSpace.Skia) | Skia compositing, effects, masks, resource caches and PNG/AVI export |
| [EffectsSpace.Controls](https://www.nuget.org/packages/EffectsSpace.Controls) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Controls.svg)](https://www.nuget.org/packages/EffectsSpace.Controls) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Controls.svg)](https://www.nuget.org/packages/EffectsSpace.Controls) | Compact Uno studio chrome, icons, choices, panels, splitters and numeric scrubbing |
| [EffectsSpace.Viewer](https://www.nuget.org/packages/EffectsSpace.Viewer) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Viewer.svg)](https://www.nuget.org/packages/EffectsSpace.Viewer) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Viewer.svg)](https://www.nuget.org/packages/EffectsSpace.Viewer) | Uno composition viewer: camera, tools, transforms, paths, text, masks and waveform |
| [EffectsSpace.Timeline](https://www.nuget.org/packages/EffectsSpace.Timeline) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Timeline.svg)](https://www.nuget.org/packages/EffectsSpace.Timeline) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Timeline.svg)](https://www.nuget.org/packages/EffectsSpace.Timeline) | Uno timeline: clip/key editing, marquee and value/velocity graphs |
| [EffectsSpace.Workbench](https://www.nuget.org/packages/EffectsSpace.Workbench) | [![NuGet](https://img.shields.io/nuget/vpre/EffectsSpace.Workbench.svg)](https://www.nuget.org/packages/EffectsSpace.Workbench) | [![Downloads](https://img.shields.io/nuget/dt/EffectsSpace.Workbench.svg)](https://www.nuget.org/packages/EffectsSpace.Workbench) | Complete Uno studio: panels, inspectors, transport, render queue and document workflows |

Dependencies (from project references):

```text
Core ← Animation, Documents
Animation + Documents ← Editing
Animation ← Rendering, Media
Rendering + Media (+ SkiaSharp) ← Skia
Core ← Controls (Uno)
Controls + Editing + Skia ← Viewer
Controls + Editing + Rendering ← Timeline
Viewer + Timeline ← Workbench
```

More integration detail: [Embedding](docs/EMBEDDING.md) and [Portable media](docs/MEDIA.md).

### EffectsSpace.Core

The serializable motion-design model: projects, compositions, layers (shapes, text, media, nested compositions, adjustments), animatable channels, keyframes, masks, paths, effects and markers, plus exact rational frame rates. Use it alone to generate or inspect compositions. No dependencies beyond .NET; no UI.

```sh
dotnet add package EffectsSpace.Core --prerelease
```

**Key types**

- `MotionProject` / `Composition` / `Layer` — the document tree; `MotionProject.Empty()` creates one composition.
- `Channel` / `Keyframe` / `Interpolation` — animatable values with Linear, Hold and Bezier keys and optional expressions.
- `AnimatedTransform` — position, anchor, scale, rotation and opacity channels.
- `EffectCatalog` / `LayerEffect` / `EffectKind` — effect definitions and parameter channels.
- `LayerChannels` — stable property paths (`"X"`, `"fx/<id>/Radius"`) for editors and tooling.
- `FrameRate` — `Frame`, `Seconds`, `Snap`, `Timecode`.

**Usage**

```csharp
using EffectsSpace.Core;

var comp = new Composition { Name = "Title card", FrameRate = new(30, 1), Duration = 4, WorkEnd = 4 };
var card = new Layer { Kind = LayerKind.Rectangle, Name = "Card", Width = 640, Height = 360, OutPoint = 4 };
card.Transform.X.Value = 960; card.Transform.Y.Value = 540;
card.Transform.AnchorX.Value = 320; card.Transform.AnchorY.Value = 180;
card.Transform.Opacity.SetKey(0, 0);
card.Transform.Opacity.SetKey(1, 100, Interpolation.Linear);
card.Effects.Add(EffectCatalog.Create(EffectKind.DropShadow));
comp.Layers.Add(card);

var project = new MotionProject { Name = "Demo", ActiveCompositionId = comp.Id, Compositions = [comp] };
foreach (AnimatedProperty property in LayerChannels.Enumerate(card))
    Console.WriteLine($"{property.Path}: {property.Name}");
string tc = comp.FrameRate.Timecode(2.5);   // "00:00:02:15"
```

### EffectsSpace.Animation

Deterministic evaluation of channels: Bezier/linear/hold curves, signed velocity, layer transforms through parent chains, time remapping and a bounded, side-effect-free scalar expression language (not JavaScript). Depends on `EffectsSpace.Core`; no UI.

```sh
dotnet add package EffectsSpace.Animation --prerelease
```

**Key types**

- `CurveEvaluator` — `Evaluate(channel, time)` including expressions; `EvaluateKeys`, `Bezier`.
- `CurveVelocity.TryEvaluate` — signed derivative for graphs and motion blur.
- `TransformEvaluator` — `Local`, `World`, `ToWorld`, `ToLocal` matrices/points.
- `ScalarExpression.TryEvaluate` — bounded expressions with authoring diagnostics.
- `LayerTime.Evaluate` — source time from offsets, stretch or time remap.

**Usage**

```csharp
using EffectsSpace.Animation;
using EffectsSpace.Core;

var opacity = new Channel(100);
opacity.SetKey(0, 0, Interpolation.Bezier);
opacity.SetKey(1, 100);
double halfway = CurveEvaluator.Evaluate(opacity, 0.5);
bool moving = CurveVelocity.TryEvaluate(opacity, 0.5, out double perSecond);

var rotation = new Channel(0) { Expression = "value + time * 45" };
double angle = CurveEvaluator.Evaluate(rotation, 2);   // 90

if (!ScalarExpression.TryEvaluate("sin(time) * 100", time: 1, value: 0, index: 1, out double result, out string? error))
    Console.WriteLine(error);
```

### EffectsSpace.Documents

Persistence and host contracts: versioned `.effects` JSON with validation of schema, references, budgets and embedded media, plus UI-free interfaces that platform hosts implement for recovery, file picking/saving and audio output. Depends on `EffectsSpace.Core`; no UI.

```sh
dotnet add package EffectsSpace.Documents --prerelease
```

**Key types**

- `ProjectJson` — `Save`, `Load` (validating), `Clone`, `CloneLayer`, shared `Options`.
- `ProjectValidator.Validate` — throws on invalid or over-budget documents.
- `IWorkspaceStorage` / `ImportedFile` — recovery, import and export boundary used by the workbench.
- `IAudioPreview` — optional platform audio output and playback clock.

**Usage**

```csharp
using EffectsSpace.Core;
using EffectsSpace.Documents;

string json = ProjectJson.Save(MotionProject.Empty());
MotionProject project = ProjectJson.Load(json);   // validates on load

sealed class FolderStorage(string root) : IWorkspaceStorage
{
    string Recovery => Path.Combine(root, "recovery.effects");
    public async Task<string?> ReadRecoveryAsync() => File.Exists(Recovery) ? await File.ReadAllTextAsync(Recovery) : null;
    public Task WriteRecoveryAsync(string json) => File.WriteAllTextAsync(Recovery, json);
    public Task<IReadOnlyList<ImportedFile>> PickFilesAsync(bool projectOnly = false) => Task.FromResult<IReadOnlyList<ImportedFile>>([]);
    public Task SaveFileAsync(string name, string mimeType, byte[] data) => File.WriteAllBytesAsync(Path.Combine(root, name), data);
}
```

### EffectsSpace.Editing

The editing model behind the studio: `EditorSession` owns selection, current time, auto-key and snapshot-based undo/redo; extension methods implement layer, keyframe, masking, precompose and time-remap commands. Also contains the original ORBITAL `SampleProject`. Depends on `Animation` and `Documents`; no UI.

```sh
dotnet add package EffectsSpace.Editing --prerelease
```

**Key types**

- `EditorSession` — `Edit`, `BeginEdit`/`PreviewChanged`/`CommitEdit`/`CancelEdit`, `Undo`/`Redo`, `SetTime`, `Select`, `Changed`.
- `EditorCommands` — `AddLayer`, `SetProperty`, `AddKey`, `AddEffect`, `AddMask`, `Precompose`, `SplitSelection`, …
- `KeyframeCommands` — multi-key copy/cut/paste, ease, interpolate, nudge.
- `CompositingCommands` — `EnableTimeRemap`, `FreezeFrame`, `ReverseTime`, `ToggleGuide`.
- `SampleProject.Create()` — the editable ORBITAL study.

**Usage**

```csharp
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;

var session = new EditorSession(MotionProject.Empty());
Layer layer = session.AddLayer(LayerKind.Rectangle);   // added and selected
session.AutoKey = true;
session.SetProperty("X", 250);
session.SetTime(1);
session.SetProperty("X", 700);
session.AddEffect(EffectKind.GaussianBlur);

session.BeginEdit("Drag");                             // interactive gesture
layer.Transform.Y.Value += 40; session.PreviewChanged();
session.CommitEdit();                                  // one undo step; CancelEdit on Escape

session.Undo();
string json = ProjectJson.Save(session.Project);
```

### EffectsSpace.Rendering

Renderer-neutral evaluation: resolves which layers are visible at a time, their world matrices, opacity and source time, with shared parent transforms evaluated once. Also provides hit testing, render budgets and deterministic export frame schedules. Depends on `EffectsSpace.Animation`; no UI or graphics library.

```sh
dotnet add package EffectsSpace.Rendering --prerelease
```

**Key types**

- `CompositionFrame` — evaluation snapshot for one composition/time; `Layers`, `Find(id)`.
- `RenderLayer` / `RenderPlan` — layer + `World` matrix, `Opacity`, `SourceTime`, stacking `Index`.
- `RenderPlanner` — `Build`, `HitTest`, `ExportTimes`.
- `RenderBudget` — dimension, pixel and nesting limits.

**Usage**

```csharp
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Rendering;

Composition comp = SampleProject.Create().Compositions[0];

var frame = new CompositionFrame(comp, time: 2.4);
foreach (RenderLayer item in frame.Layers)
    Console.WriteLine($"{item.Layer.Name}: opacity {item.Opacity:0.00}, world {item.World}");

Layer? hit = RenderPlanner.HitTest(comp, 2.4, new Vec2(960, 540));
double[] times = RenderPlanner.ExportTimes(comp).ToArray();   // work-area frame times
RenderBudget.Default.Check(3840, 2160);                         // throws when over budget
```

### EffectsSpace.Media

Portable, bounded media without native codecs: RIFF/WAVE and classic Motion JPEG AVI readers, zero-copy PCM indexing, band-limited sinc resampling, a nested-composition audio mixer and AVI/WAVE writers. Runs identically on desktop and WebAssembly. Depends on `EffectsSpace.Animation`; no Uno or Skia.

```sh
dotnet add package EffectsSpace.Media --prerelease
```

**Key types**

- `AviSource` — `Read`, `FrameAt`, `FrameData` (JPEG slices), optional `Audio`.
- `WaveFile` / `PcmSource` — WAVE parsing and sample access; `WaveFile.Encode` for quick output.
- `AudioMixer` — prepared composition mix; `Mix`, `WaveAsync`.
- `WaveRenderer` / `WavePcmWriter` — PCM16/PCM24/Float32 delivery with optional TPDF dither.
- `MjpegAviWriter` — forward-only AVI muxing of JPEG frames and PCM16.
- `MediaCatalog` — cached metadata/indexes keyed by asset payload.

**Usage**

```csharp
using EffectsSpace.Media;

AviSource video = AviSource.Read(aviBytes);                  // MJPEG AVI, optional PCM
ReadOnlyMemory<byte> jpeg = video.FrameData(video.FrameAt(0.5));
PcmSource audio = WaveFile.Read(wavBytes);
float left = audio.Sample(0.25, channel: 0);

var mixer = new AudioMixer(project, composition, quality: AudioResamplingQuality.BandLimited);
var stereo = new float[4096 * 2];
mixer.Mix(startTime: 0, firstFrame: 0, stereo, sampleRate: 48000);
byte[] wave = await WaveRenderer.RenderAsync(mixer, 0, 2, WaveEncoding.Pcm24,
    dither: true, cancellationToken: cancellationToken);
```

### EffectsSpace.Skia

The compositor: draws a composition at a time into any `SKCanvas` with blend modes, mattes, masks, effects, motion blur, nested compositions and decoded video frames, using bounded geometry/filter/image caches. Exporters produce PNG frames, PNG sequences and Motion JPEG AVI with sound. Depends on `Rendering`, `Media` and SkiaSharp; no UI framework (headless hosts need Skia native assets and a typeface for text).

```sh
dotnet add package EffectsSpace.Skia --prerelease
```

**Key types**

- `SkiaCompositor` — `Render(canvas, project, composition, time)`, `Typeface`, `Budget`, `Metrics`; `IDisposable`.
- `FrameExporter` — `Png`, `PngSequenceAsync`.
- `AviExporter` — `ExportAsync` to Motion JPEG AVI with mixed PCM audio.
- `EffectPipeline`, `PathGeometry` — Skia filters and paths for layers.
- `MediaStudy.Create` — the CLOCKWORK media sample.

**Usage**

```csharp
using EffectsSpace.Editing;
using EffectsSpace.Skia;
using SkiaSharp;

var project = SampleProject.Create();
var composition = project.Compositions[0];

using var compositor = new SkiaCompositor { Typeface = SKTypeface.Default };
byte[] png = new FrameExporter(compositor).Png(project, composition, time: 2.4, width: 960);
byte[] avi = await new AviExporter(compositor).ExportAsync(project, composition, width: 960);

using var surface = SKSurface.Create(new SKImageInfo(composition.Width, composition.Height));
compositor.Render(surface.Canvas, project, composition, time: 2.4, includeGuides: false);
```

### EffectsSpace.Controls

Original compact studio chrome for Uno: vector icons, buttons, drop-down choices, tabbed panels, splitters and scrub-to-edit numeric fields with begin/preview/commit/cancel events. Depends on `EffectsSpace.Core`; requires Uno Platform (Skia renderer).

```sh
dotnet add package EffectsSpace.Controls --prerelease
```

**Key types**

- `NumericField` — scrub/type numeric input; `EditStarted`, `ValueChanging`, `EditCompleted`, `EditCancelled`.
- `StudioButton` / `StudioIcon` / `IconKind` — icon buttons and standalone vector icons.
- `StudioPanel` — tabbed panel; `AddTab`, `Select`, `TabChanged`.
- `StudioChoice`, `StudioSplitter` — compact choice list and draggable splitter.
- `Studio` — palette constants, `Brush`, `Text`, `Input`, `Row` helpers.

**Usage**

```csharp
using EffectsSpace.Controls;

var opacity = new NumericField("Opacity", 100) { Minimum = 0, Maximum = 100 };
opacity.ValueChanging += value => Preview(value);
opacity.EditCompleted += Commit;

var panel = new StudioPanel();
panel.AddTab("Properties", Studio.Row(Studio.Text("Opacity"), opacity));
panel.AddTab("Actions", new StudioButton("Render", () => Render(), IconKind.Render));
window.Content = panel;   // your Uno Window
```

### EffectsSpace.Viewer

The interactive composition viewer: draws through `SkiaCompositor` into Uno's Skia canvas and supports pan/zoom, selection, direct transform/anchor/rotation manipulation, pen paths, inline text, mask editing and an audio waveform view. Depends on `Controls`, `Editing` and `Skia`; requires Uno Platform (Skia renderer).

```sh
dotnet add package EffectsSpace.Viewer --prerelease
```

**Key types**

- `CompositionView(EditorSession)` — `Tool`, `Fit`, `SetZoom`, `ShowGrid`, `ShowGuides`, `ShowTransparency`, `Renderer`; `IDisposable`.
- `ViewerTool` — Select, Hand, Zoom, Rotate, Anchor, shapes, Pen, Text.
- `MaskEditView(CompositionView)` — transactional mask node/tangent editing overlay.
- `AudioWaveformView` — `SetSource(PcmSource)`, `Playhead`.

**Usage**

```csharp
using EffectsSpace.Editing;
using EffectsSpace.Viewer;

var session = new EditorSession(SampleProject.Create());
var viewer = new CompositionView(session) { Tool = ViewerTool.Select, ShowGrid = true };
viewer.Error += message => Console.Error.WriteLine(message);
window.Content = viewer;                     // your Uno Window
viewer.Loaded += (_, _) => viewer.Fit();
window.Closed += (_, _) => viewer.Dispose();
```

### EffectsSpace.Timeline

The composition timeline: row-cached layer bars, trimming, keyframe selection/marquee/drag, property filtering and value/velocity graph editing with Bezier handles, all bound to an `EditorSession`. Depends on `Controls`, `Editing` and `Rendering`; requires Uno Platform (Skia renderer).

```sh
dotnet add package EffectsSpace.Timeline --prerelease
```

**Key types**

- `TimelineView(EditorSession)` — `Fit`, `ZoomBy`, `GraphMode`, `GraphKind`, `ShowProperties`, `RevealProperty`; `IDisposable`.
- `GraphKind` — `Value` or `Velocity`.
- `TimelineRow` — the laid-out layer/property rows.

**Usage**

```csharp
using EffectsSpace.Editing;
using EffectsSpace.Timeline;

var session = new EditorSession(SampleProject.Create());
var timeline = new TimelineView(session);
timeline.Error += message => Console.Error.WriteLine(message);
window.Content = timeline;                   // your Uno Window
timeline.Loaded += (_, _) => timeline.Fit();

timeline.RevealProperty("Opacity");           // expand and scroll to a property
timeline.GraphMode = true;                    // curve editor
timeline.GraphKind = GraphKind.Velocity;
```

`CompositionView` and `TimelineView` can share one session and do not require the full workbench.

### EffectsSpace.Workbench

The complete studio as one `UserControl`: project bin, viewer, timeline and graph, inspectors, effect/mask panels, media and audio-delivery settings, transport with audio clock, render queue and open/save/import/export workflows. The host supplies an `IWorkspaceStorage` (and optionally `IAudioPreview`). Depends on `Viewer` and `Timeline`; requires Uno Platform (Skia renderer).

```sh
dotnet add package EffectsSpace.Workbench --prerelease
```

**Key types**

- `StudioWorkbench(EditorSession, IWorkspaceStorage)` — the studio control; `IDisposable`.
- `Session`, `Viewer`, `Timeline`, `MaskEditor` — access to the embedded parts.
- `SaveAsync`, `OpenAsync`, `ImportAsync`, `ExportFrameAsync`, `ExportAviAsync`, `ExportAudioAsync`, `TogglePlayback`, `ShowStatus`.

**Usage**

```csharp
using EffectsSpace.Controls;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Workbench;

protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    var window = new Window { Title = "EffectsSpace" };
    IWorkspaceStorage storage = new MyWorkspaceStorage();   // your platform implementation
    var workbench = new StudioWorkbench(new EditorSession(SampleProject.Create()), storage);
    workbench.Viewer.Renderer.Typeface = Studio.Typeface;
    window.Content = workbench;
    window.Closed += (_, _) => workbench.Dispose();
    window.Activate();
}
```

`src/EffectsSpace.App` contains complete desktop (`DesktopWorkspaceStorage`) and browser (`BrowserWorkspaceStorage`) implementations.

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
