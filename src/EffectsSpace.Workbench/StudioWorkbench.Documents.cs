using System.Globalization;
using System.Text;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Skia;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private async Task SaveRecoveryAsync()
    {
        if (_savingRecovery || Session.IsEditing || _disposed) { if (!_disposed) _recoveryTimer.Start(); return; }
        _savingRecovery = true; var revision = Session.Revision;
        try { await _storage.WriteRecoveryAsync(ProjectJson.Save(Session.Project)); if (revision == Session.Revision) ShowStatus("Recovery copy saved locally"); else _recoveryTimer.Start(); }
        catch (Exception ex) { ShowStatus("Recovery unavailable: " + ex.Message + ". Save a project file to keep your work.", true); }
        finally { _savingRecovery = false; }
    }
    public async Task SaveAsync()
    {
        Viewer.EndText(true); var json = ProjectJson.Save(Session.Project);
        await _storage.SaveFileAsync(SafeName(Session.Project.Name) + ".effects", "application/json", Encoding.UTF8.GetBytes(json)); ShowStatus("Project exported · " + Session.Project.Name);
    }
    public async Task OpenAsync()
    {
        Pause(); Viewer.EndText(true); var files = await _storage.PickFilesAsync(true); if (files.Count == 0) return;
        var project = ProjectJson.Load(Encoding.UTF8.GetString(files[0].Data));
        Session.Replace(project); Viewer.Renderer.ClearImages(); Viewer.Fit(); Timeline.Fit(); ShowStatus("Opened " + files[0].Name);
    }
    public async Task ImportAsync()
    {
        Pause(); var files = await _storage.PickFilesAsync();
        foreach (var file in files)
        {
            if (file.Name.EndsWith(".effects", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            { var project = ProjectJson.Load(Encoding.UTF8.GetString(file.Data)); Session.Replace(project); Viewer.Renderer.ClearImages(); Viewer.Fit(); Timeline.Fit(); continue; }
            if (file.MimeType is not ("image/png" or "image/jpeg" or "image/webp")) throw new InvalidDataException("This release imports PNG, JPEG and WebP images. Video/audio decoding is not implemented; no placeholder footage will be created.");
            var (width, height) = SkiaCompositor.ImageInfo(file.Data);
            var asset = new MediaAsset { Name = file.Name, MimeType = file.MimeType, Data = file.Data, Width = width, Height = height };
            Session.Edit("Import " + file.Name, () => Session.Project.Assets.Add(asset)); AddAssetLayer(asset);
        }
        if (files.Count > 0) ShowStatus($"Imported {files.Count} item(s)");
    }
    private void AddAssetLayer(MediaAsset asset)
    {
        if (!asset.MimeType.StartsWith("image/")) throw new NotSupportedException("Video and audio layers are reserved in the document model but are not decoded by this release.");
        var layer = new Layer { Name = asset.Name, Kind = LayerKind.Image, SourceId = asset.Id, Width = asset.Width, Height = asset.Height, OutPoint = Session.Composition.Duration, Label = "#8EC6B3" };
        layer.Transform.AnchorX.Value = layer.Width / 2; layer.Transform.AnchorY.Value = layer.Height / 2; layer.Transform.X.Value = Session.Composition.Width / 2d; layer.Transform.Y.Value = Session.Composition.Height / 2d;
        var scale = Math.Min(1, Math.Min(Session.Composition.Width / layer.Width, Session.Composition.Height / layer.Height)); layer.Transform.ScaleX.Value = layer.Transform.ScaleY.Value = scale * 100;
        Session.Edit("Add footage layer", () => Session.Composition.Layers.Insert(0, layer)); Session.Select(layer.Id);
    }
    private async Task NewCompositionAsync()
    {
        var composition = new Composition { Name = "Composition " + (Session.Project.Compositions.Count + 1) };
        if (!await EditCompositionDialogAsync(composition, true)) return;
        Session.Edit("New composition", () => Session.Project.Compositions.Add(composition)); Session.Activate(composition.Id); Viewer.Fit(); Timeline.Fit();
    }
    private async Task CompositionSettingsAsync()
    {
        var original = Session.Composition;
        var copy = new Composition { Name = original.Name, Width = original.Width, Height = original.Height, FrameRate = original.FrameRate, Duration = original.Duration, WorkStart = original.WorkStart, WorkEnd = original.WorkEnd, Background = original.Background };
        if (!await EditCompositionDialogAsync(copy, false)) return;
        Session.Edit("Composition settings", () =>
        {
            original.Name = copy.Name; original.Width = copy.Width; original.Height = copy.Height; original.FrameRate = copy.FrameRate; original.Duration = copy.Duration; original.Background = copy.Background;
            original.WorkStart = Math.Min(original.WorkStart, copy.Duration - copy.FrameRate.Seconds(1)); original.WorkEnd = Math.Min(original.WorkEnd, copy.Duration);
            original.Layers.RemoveAll(l => l.InPoint >= copy.Duration);
            foreach (var l in original.Layers) l.OutPoint = Math.Min(l.OutPoint, copy.Duration);
            var ids = original.Layers.Select(l => l.Id).ToHashSet(); foreach (var l in original.Layers) { if (l.ParentId is { } parent && !ids.Contains(parent)) l.ParentId = null; if (l.MatteId is { } matte && !ids.Contains(matte)) l.MatteId = null; }
            original.Markers.RemoveAll(m => m.Time >= copy.Duration);
        }); Viewer.Fit(); Timeline.Fit();
    }
    private async Task<bool> EditCompositionDialogAsync(Composition composition, bool create)
    {
        Pause(); var form = new StackPanel { Spacing = 9, MinWidth = 350 };
        var name = Studio.Input(composition.Name, "Composition name"); var width = Studio.Input(composition.Width.ToString(), "Composition width"); var height = Studio.Input(composition.Height.ToString(), "Composition height");
        var duration = Studio.Input(composition.Duration.ToString(CultureInfo.InvariantCulture), "Composition duration"); var fps = Studio.Input(composition.FrameRate.FramesPerSecond.ToString(CultureInfo.InvariantCulture), "Composition frame rate"); var background = Studio.Input(composition.Background, "Composition background");
        form.Children.Add(FieldRow("Name", name)); form.Children.Add(FieldRow("Width", width)); form.Children.Add(FieldRow("Height", height)); form.Children.Add(FieldRow("Duration (s)", duration)); form.Children.Add(FieldRow("Frame rate", fps)); form.Children.Add(FieldRow("Background", background));
        var message = new TextBlock { Text = "Maximum 8192 px per dimension; rendering is limited to 32 megapixels.\nFrame rates 23.976 / 29.97 / 59.94 use exact 1001-denominator rates.", TextWrapping = TextWrapping.Wrap, FontSize = 10, Foreground = Studio.Brush(Studio.Muted) }; form.Children.Add(message);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = create ? "New Composition" : "Composition Settings", Content = form, PrimaryButtonText = create ? "Create" : "Apply", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, RequestedTheme = ElementTheme.Dark };
        dialog.PrimaryButtonClick += (_, e) =>
        {
            try
            {
                var w = int.Parse(width.Text, CultureInfo.InvariantCulture); var h = int.Parse(height.Text, CultureInfo.InvariantCulture); var seconds = double.Parse(duration.Text, CultureInfo.InvariantCulture); var rate = double.Parse(fps.Text, CultureInfo.InvariantCulture);
                var rational = Math.Abs(rate - 23.976) < .001 ? new FrameRate(24000, 1001) : Math.Abs(rate - 29.97) < .001 ? new(30000, 1001) : Math.Abs(rate - 59.94) < .001 ? new(60000, 1001) : new((int)Math.Round(rate * 1000), 1000);
                var test = new Composition { Name = name.Text, Width = w, Height = h, Duration = seconds, WorkEnd = seconds, FrameRate = rational, Background = background.Text };
                ProjectValidator.Validate(new MotionProject { ActiveCompositionId = test.Id, Compositions = [test] });
                composition.Name = test.Name; composition.Width = w; composition.Height = h; composition.Duration = seconds; composition.FrameRate = rational; composition.Background = test.Background;
                if (create) composition.WorkEnd = seconds;
            }
            catch (Exception ex) { e.Cancel = true; message.Text = ex.Message; message.Foreground = Studio.Brush("#E49A90"); }
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    public async Task ExportFrameAsync()
    {
        Pause(); Viewer.EndText(true);
        RejectUndecodedMedia(Session.Project);
        var bytes = new FrameExporter(Viewer.Renderer).Png(Session.Project, Session.Composition, Session.Time);
        await _storage.SaveFileAsync(SafeName(Session.Composition.Name) + "-" + Session.Composition.FrameRate.Frame(Session.Time).ToString("000000") + ".png", "image/png", bytes);
        ShowStatus("Exported current frame as lossless PNG");
    }
    public async Task ExportSequenceAsync()
    {
        if (IsRendering) throw new InvalidOperationException("A render is already running.");
        Pause(); Viewer.EndText(true); RejectUndecodedMedia(Session.Project);
        var project = ProjectJson.Clone(Session.Project); var comp = project.Compositions.First(c => c.Id == project.ActiveCompositionId);
        var item = new RenderQueueItem { Name = comp.Name, Status = "Rendering" }; _renderItems.Add(item); _bottomPanel.Select("Render Queue"); RefreshQueue();
        _renderCancellation = new(); var token = _renderCancellation.Token;
        try
        {
            using var renderer = new SkiaCompositor { Typeface = Viewer.Renderer.Typeface, MotionBlurSamples = Viewer.Renderer.MotionBlurSamples };
            var progress = new Progress<double>(p => { item.Progress = p; RefreshQueue(); ShowStatus($"Rendering {comp.Name}: {p:P0}"); });
            var bytes = await new FrameExporter(renderer).PngSequenceAsync(project, comp, comp.Width, progress, cancellationToken: token);
            await _storage.SaveFileAsync(SafeName(comp.Name) + "-frames.zip", "application/zip", bytes); item.Progress = 1; item.Status = "Completed"; ShowStatus("PNG sequence exported with frame-rate manifest");
        }
        catch (OperationCanceledException) { item.Status = "Cancelled"; ShowStatus("Render cancelled; the project was not changed."); }
        catch (Exception ex) { item.Status = "Failed"; item.Error = ex.Message; ShowStatus(ex.Message, true); }
        finally { _renderCancellation.Dispose(); _renderCancellation = null; RefreshQueue(); }
    }
    private static void RejectUndecodedMedia(MotionProject project)
    {
        if (project.Compositions.Any(c => c.Layers.Any(l => l.Enabled && l.Kind is LayerKind.Video or LayerKind.Audio)))
            throw new NotSupportedException("This project contains video/audio layers. This release does not decode them; export is blocked instead of silently omitting media.");
    }
    private static string SafeName(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-'); return safe.Length == 0 ? "EffectsSpace" : safe[..Math.Min(100, safe.Length)];
    }
    private async Task ShowHelpAsync()
    {
        var text = "EFFECTSSPACE · USER GUIDE\n\nCreate and animate\nSelect a layer in the viewer or timeline. Drag to move it; drag one of its eight handles to scale. Shift constrains shape proportions and rotation. The Rotation and Anchor tools edit those transforms directly. Double-click a text layer to edit in the composition. The Pen tool adds points; drag to create Bezier handles, click the first point to close, or press Enter to finish.\n\nTimeline\nDrag the blue time ruler to scrub. Drag a layer bar to move it; drag its ends to trim. Alt-drag slips source time. Eye, solo and lock switches are live. Expand a layer to see animated properties. P / S / R / T / A show position, scale, rotation, opacity and anchor. U shows animated properties. Drag a diamond to retime a key. In the Graph Editor, drag a key vertically to change its value; Ctrl-click adds a key. F9 applies temporal Easy Ease.\n\nAnimation\nClick a stopwatch to enable animation. Move to another frame and change a property to add another key. Auto-key also creates a first key. Linear, Hold and Bezier interpolation are supported. The scalar expression field accepts time, value, index, pi, arithmetic, sin, cos, abs, sqrt, min, max, clamp, linear, ease, wiggle and loopOut(). It is deliberately not JavaScript or Adobe's expression engine.\n\nProjects and footage\nSave an .effects project to preserve compositions, embedded images, masks, effects, expressions and keyframes. PNG, JPEG and WebP imports are supported. Project data remains on your device. A recovery copy is stored locally; export a project file for a durable backup. Video/audio files are not decoded by this release.\n\nEffects and compositing\nSelect a layer, open Effects & Presets, and choose an effect. The left Effect Controls tab contains live parameters, enable switches and ordering. Properties contains blend, parent and alpha-matte selectors. Pre-compose moves selected layers into a nested composition and retains a composition layer in the parent. Connected parents and mattes must be included together.\n\nRender\nSnapshot PNG exports the current composition frame with alpha. Render Queue exports the work area as a ZIP of PNG frames and a rational frame-rate manifest. Rendering is limited to 32 megapixels per frame, 3600 frames and 256 MiB per archive. Exports use 8-bit sRGB, not a professional HDR/color-management pipeline.\n\nShortcuts\nCtrl+S Save · Ctrl+O Open · Ctrl+I Import · Ctrl+N New Composition\nCtrl+Z Undo · Ctrl+Shift+Z Redo · Ctrl+D Duplicate · Delete Remove\nCtrl+Shift+C Pre-compose · Ctrl+Shift+D Split · Ctrl+M Render\nSpace Play/Pause · Page Up/Down Previous/Next Frame · Home/End\nB/N Work Area Start/End · F9 Easy Ease · Shift+F3 Graph Editor\nV Select · H Hand · Z Zoom · W Rotate · Y Anchor · Q Shape · G Pen\nShift+T Text Tool · Arrow Keys Nudge · Shift+Arrows Nudge 10 px\n\nRelease boundaries\nNo .aep/.aepx import, Adobe plug-ins, video codec pipeline, audio mixing, 3D cameras/lights, tracking, rotoscoping, paint engine, scripting, collaboration or full Adobe parity is claimed. The editable composition and animation core is real; reserved unsupported document types are not silently exported.";
        await new ContentDialog { XamlRoot = XamlRoot, Title = "EffectsSpace Documentation", Content = new ScrollViewer { MaxHeight = 540, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontFamily = Studio.Font, FontSize = 12, Foreground = Studio.Brush(Studio.TextColor), MaxWidth = 650 } }, CloseButtonText = "Close", RequestedTheme = ElementTheme.Dark }.ShowAsync();
    }
    private async Task ShowAboutAsync() => await new ContentDialog { XamlRoot = XamlRoot, Title = "EffectsSpace 0.1.0-alpha.1", Content = new TextBlock { Text = "Independent motion-design and compositing workbench.\n\nUno Platform 6.7 · .NET 10 · SkiaSharp 3.119.2\nOriginal code, editable sample artwork and vector iconography.\nMIT licensed.\n\nEffectsSpace is not affiliated with Adobe and does not claim complete After Effects feature or file compatibility.", TextWrapping = TextWrapping.Wrap, FontSize = 13 }, CloseButtonText = "Close", RequestedTheme = ElementTheme.Dark }.ShowAsync();
}
