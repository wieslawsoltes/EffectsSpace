# Embedding the libraries

## Use the animation engine without Uno

Reference `EffectsSpace.Core` and `EffectsSpace.Animation` from a `net10.0` application:

```csharp
using EffectsSpace.Core;
using EffectsSpace.Animation;

var opacity = new Channel(100);
opacity.SetKey(0, 0, Interpolation.Bezier);
opacity.SetKey(1, 100);
double halfway = CurveEvaluator.Evaluate(opacity, 0.5);
```

The core is graphics- and UI-independent. Scalar expressions are bounded and return a fallback value on evaluation failure. Use `ScalarExpression.TryEvaluate` directly to display authoring diagnostics.

## Edit a project transactionally

```csharp
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Documents;

var session = new EditorSession(MotionProject.Empty());
var layer = session.AddLayer(LayerKind.Rectangle);
session.AutoKey = true;
session.SetProperty("X", 250);
session.SetTime(1);
session.SetProperty("X", 700);
session.Undo();
string json = ProjectJson.Save(session.Project);
```

For a drag, call `BeginEdit`, mutate values and call `PreviewChanged` while moving, then `CommitEdit` once. Call `CancelEdit` for lost capture or Escape. Never replace a failed validation with partial success. Treat imported media byte arrays as immutable.

## Embed the complete Uno workbench

Reference `EffectsSpace.Workbench` from an Uno single-project application using the `SkiaRenderer` feature:

```csharp
using EffectsSpace.Editing;
using EffectsSpace.Workbench;

var session = new EditorSession(SampleProject.Create());
var workbench = new StudioWorkbench(session, workspaceStorage);
window.Content = workbench;
window.Closed += (_, _) => workbench.Dispose();
```

Implement `IWorkspaceStorage` for recovery, import selection and user exports. The interface lives in Documents and does not reference Uno. Desktop and browser host implementations in `EffectsSpace.App` show native pickers and IndexedDB/download integration.

## Embed only the viewer and timeline

```csharp
var session = new EditorSession(SampleProject.Create());
var viewer = new EffectsSpace.Viewer.CompositionView(session);
var timeline = new EffectsSpace.Timeline.TimelineView(session);
```

Both controls share the session and subscribe to its change stream. Add them to your own panels and dispose them when the host closes. The viewer exposes camera, tools, overlays and its compositor. The timeline exposes fit/zoom, graph mode and property visibility. They do not require the full application shell.

## Render without the workbench

```csharp
using EffectsSpace.Skia;
using EffectsSpace.Editing;

var project = SampleProject.Create();
using var renderer = new SkiaCompositor();
var exporter = new FrameExporter(renderer);
byte[] png = exporter.Png(project, project.Compositions[0], time: 2.4, width: 960);
```

On a headless host, deploy the correct Skia native assets and provide a suitable `SKTypeface` for text. `SkiaCompositor.Render` can also draw into an existing compatible `SKCanvas`; the caller owns canvas lifetime and typeface lifetime. The compositor owns decoded cached images and must be disposed.

## Package production

```sh
dotnet pack src/EffectsSpace.Core -c Release -o artifacts/packages
dotnet pack src/EffectsSpace.Workbench -c Release -o artifacts/packages
```

The Uno packages multi-target desktop and browser. Install `wasm-tools` before packing them. Tagged releases publish every package to NuGet.org, e.g. `dotnet add package EffectsSpace.Workbench`.

## Portable media

`EffectsSpace.Media` is the eleventh packable library and has no Uno/Skia dependency. Use `AviSource`, `WaveFile`, `PcmSource`, `MediaCatalog`, `AudioMixer` and `MjpegAviWriter` independently. `EffectsSpace.Skia.AviExporter` connects these components to rendered composition frames. A storage adapter can additionally implement `IAudioPreview` to supply audio output and a playback clock; `AudioWaveformView` is reusable without the full workbench. [Media contracts and complete examples](MEDIA.md).

Use `session.CaptureSnapshot()` for independent export model metadata while retaining shared immutable media buffers. Mutating the returned model does not mutate the editor, but writing into either snapshot's `MediaAsset.Data` array violates the shared-buffer contract.
