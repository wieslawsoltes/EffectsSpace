using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Media;
using EffectsSpace.Skia;
using EffectsSpace.Viewer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private readonly StackPanel _mediaPanel = new() { Spacing = 5 };
    private readonly MediaCatalog _mediaCatalog = new();
    private readonly AudioWaveformView _waveform = new() { Height = 104, HorizontalAlignment = HorizontalAlignment.Stretch };
    private NumericField? _audioGainField, _audioPanField;
    private MediaContent? _selectedMedia;
    public bool WaveformReady => _waveform.IsReady;
    public int SelectedVideoFrame => Session.Primary is { } layer && _selectedMedia?.Video is { } video
        ? video.FrameAt(LayerTime.Evaluate(layer, Session.Time, Session.Composition.Layers.IndexOf(layer) + 1)) : -1;
    private void InitializeMedia() => _rightPanel.AddTab("Media", Scroll(_mediaPanel));
    private void OpenMedia() { _rightPanel.Select("Media"); RefreshMedia(); Focus(FocusState.Programmatic); }

    private void RefreshMedia()
    {
        _mediaPanel.Children.Clear(); _audioGainField = _audioPanField = null; _selectedMedia = null;
        _mediaPanel.Children.Add(Heading("FOOTAGE / AUDIO"));
        _mediaPanel.Children.Add(Studio.Row(Button("Import Media", () => _ = RunAsync(ImportAsync), IconKind.Import),
            Button("Clockwork Sample", LoadMediaStudy, IconKind.Composition)));
        _mediaPanel.Children.Add(Note("Motion JPEG AVI · PCM WAVE · PNG/JPEG/WebP\nMedia stays on your device."));
        var layer = Session.Primary;
        if (layer?.SourceId is { } id && Session.Project.Assets.FirstOrDefault(a => a.Id == id) is { } asset)
        {
            _mediaPanel.Children.Add(Heading(asset.Name));
            if (MediaCatalog.SupportsAudio(asset.MimeType))
            {
                try
                {
                    _selectedMedia = _mediaCatalog.Get(asset);
                    if (_selectedMedia.Video is { } video)
                        _mediaPanel.Children.Add(Note($"Motion JPEG · {video.Width} × {video.Height}\n{video.FrameRate} · {video.FrameCount} frames · {video.Duration:0.###}s"));
                    if (_selectedMedia.Audio is { } audio)
                        _mediaPanel.Children.Add(Note($"PCM {(audio.Format.FloatingPoint ? "float" : "integer")} {audio.Format.BitsPerSample}-bit · {audio.Format.SampleRate} Hz\n{audio.Format.Channels} channels · {audio.Duration:0.###}s"));
                }
                catch (Exception ex) { _mediaPanel.Children.Add(Note(ex.Message)); }
            }
            else _mediaPanel.Children.Add(Note($"{asset.MimeType} · {asset.Width} × {asset.Height}"));
        }
        _waveform.SetSource(_selectedMedia?.Audio); _mediaPanel.Children.Add(_waveform);
        if (layer is not null && layer.Kind is LayerKind.Audio or LayerKind.Video or LayerKind.Composition)
        {
            var mute = Button(layer.AudioEnabled ? "Audio Enabled" : "Audio Muted", () => EditLayer(layer, "Toggle audio", () => layer.AudioEnabled = !layer.AudioEnabled), IconKind.Audio);
            mute.Active = layer.AudioEnabled; _mediaPanel.Children.Add(mute);
            _audioGainField = Number("Audio gain dB", CurveEvaluator.Evaluate(layer.AudioGain, Session.Time),
                value => EditorCommands.SetChannel(layer.AudioGain, Session.Time, value, Session.AutoKey), -96, 24, .25);
            _audioPanField = Number("Audio balance", CurveEvaluator.Evaluate(layer.AudioPan, Session.Time),
                value => EditorCommands.SetChannel(layer.AudioPan, Session.Time, value, Session.AutoKey), -100, 100);
            _mediaPanel.Children.Add(FieldRow("Gain (dB)", _audioGainField));
            _mediaPanel.Children.Add(FieldRow("Balance", _audioPanField));
            _mediaPanel.Children.Add(Studio.Row(Button("Animate Gain", () => { Session.ToggleAnimation("AudioGain"); Timeline.RevealProperty("AudioGain"); }, IconKind.Stopwatch),
                Button("Gain Graph", () => { Timeline.RevealProperty("AudioGain"); Timeline.GraphMode = true; Timeline.Invalidate(); }, IconKind.Graph)));
        }
        _mediaPanel.Children.Add(Heading("DELIVERY"));
        _mediaPanel.Children.Add(Button("Export AVI + Audio", () => _ = RunAsync(ExportAviAsync), IconKind.Render));
        _mediaPanel.Children.Add(Button("Export Audio WAVE", () => _ = RunAsync(ExportAudioAsync), IconKind.Audio));
        _mediaPanel.Children.Add(Note("AVI: Motion JPEG, 8-bit sRGB, composition background, stereo PCM16/48 kHz. No alpha.\nWAVE: stereo PCM16/48 kHz. Exports use the work area."));
        _mediaPanel.Children.Add(Note(_storage is IAudioPreview
            ? "Space previews mixed audio on the browser audio clock. Prepared previews are limited to 60 seconds."
            : "This native host has silent timeline preview. Mixed audio is included in AVI and WAVE exports."));
        UpdateMediaValues();
    }

    private void UpdateMediaValues()
    {
        if (Session.Primary is not { } layer) return;
        var index = Session.Composition.Layers.IndexOf(layer) + 1;
        if (_audioGainField is not null) _audioGainField.Value = CurveEvaluator.Evaluate(layer.AudioGain, Session.Time, index);
        if (_audioPanField is not null) _audioPanField.Value = CurveEvaluator.Evaluate(layer.AudioPan, Session.Time, index);
        _waveform.Playhead = LayerTime.Evaluate(layer, Session.Time, index);
    }

    private void LoadMediaStudy()
    {
        if (IsRendering) throw new InvalidOperationException("Finish or cancel the current export before replacing the project.");
        Pause(); Viewer.EndText(true); MaskEditor.Stop();
        Session.Replace(MediaStudy.Create(Viewer.Renderer.Typeface)); Viewer.Renderer.ClearResources(); _mediaCatalog.Clear();
        Session.Select(Session.Composition.Layers[0].Id); Viewer.Fit(); Timeline.Fit(); OpenMedia();
        ShowStatus("Clockwork: editable Motion JPEG footage with stereo PCM audio");
    }

    public Task ExportAviAsync() => ExportMediaAsync(true);
    public Task ExportAudioAsync() => ExportMediaAsync(false);
    private async Task ExportMediaAsync(bool video)
    {
        if (IsRendering) throw new InvalidOperationException("A render is already running.");
        Pause(); Viewer.EndText(true);
        var project = Session.CaptureSnapshot(); var comp = project.Compositions.First(c => c.Id == project.ActiveCompositionId);
        var item = new RenderQueueItem { Name = comp.Name, Format = video ? "MJPEG AVI / PCM audio" : "PCM WAVE", Status = "Rendering" };
        _renderItems.Add(item); _bottomPanel.Select("Render Queue"); RefreshQueue(); Focus(FocusState.Programmatic);
        var cancellation = new CancellationTokenSource(); _renderCancellation = cancellation;
        try
        {
            byte[] bytes;
            if (video)
            {
                using var renderer = new SkiaCompositor { Typeface = Viewer.Renderer.Typeface, MotionBlurSamples = Viewer.Renderer.MotionBlurSamples };
                var progress = new Progress<double>(value =>
                {
                    if (!ReferenceEquals(_renderCancellation, cancellation)) return;
                    item.Progress = value; RefreshQueue(); ShowStatus($"Encoding {comp.Name}: {value:P0}");
                });
                bytes = await new AviExporter(renderer).ExportAsync(project, comp, progress: progress, cancellationToken: cancellation.Token);
            }
            else bytes = await new AudioMixer(project, comp).WaveAsync(comp.WorkStart, comp.WorkEnd - comp.WorkStart, cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            await _storage.SaveFileAsync(SafeName(comp.Name) + (video ? ".avi" : ".wav"), video ? "video/x-msvideo" : "audio/wav", bytes);
            item.Progress = 1; item.Status = "Completed"; ShowStatus(video ? "Exported Motion JPEG AVI with mixed PCM audio" : "Exported stereo PCM WAVE");
        }
        catch (OperationCanceledException) { item.Status = "Cancelled"; ShowStatus("Media export cancelled; the project was not changed."); }
        catch (Exception ex) { item.Status = "Failed"; item.Error = ex.Message; ShowStatus(ex.Message, true); }
        finally
        {
            if (ReferenceEquals(_renderCancellation, cancellation)) _renderCancellation = null;
            cancellation.Dispose(); RefreshQueue();
            if (!_disposed && XamlRoot is not null && FocusManager.GetFocusedElement(XamlRoot) is not TextBox)
                Focus(FocusState.Programmatic);
        }
    }
}
