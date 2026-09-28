using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Media;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private bool _preparingPlayback, _audioClock, _playheadTick;
    private long _playGeneration;
    private CancellationTokenSource? _previewCancellation;
    private (long Revision, string Composition, double Start, double End)? _preparedAudio;
    public bool IsPreparingPlayback => _preparingPlayback;
    public bool UsesAudioClock => _audioClock;

    public void TogglePlayback()
    {
        if (IsPlaying || _preparingPlayback) { Pause(); return; }
        Viewer.EndText(true);
        if (Session.IsEditing || IsRendering) return;
        _ = RunAsync(StartPlaybackAsync);
    }

    private async Task StartPlaybackAsync()
    {
        var comp = Session.Composition;
        if (Session.Time < comp.WorkStart || Session.Time >= comp.WorkEnd - comp.FrameRate.Seconds(1)) Session.SetTime(comp.WorkStart);
        var generation = ++_playGeneration;
        var cancellation = new CancellationTokenSource(); _previewCancellation = cancellation; _preparingPlayback = true;
        try
        {
            var key = (Session.Revision, comp.Id, comp.WorkStart, comp.WorkEnd);
            var snapshot = Session.CaptureSnapshot();
            var previewComp = snapshot.Compositions.First(c => c.Id == comp.Id);
            var mixer = new AudioMixer(snapshot, previewComp, _mediaCatalog, includeGuides: true, quality: _audioQuality);
            if (mixer.HasAudio && _storage is IAudioPreview output)
            {
                if (comp.WorkEnd - comp.WorkStart > 60) throw new InvalidOperationException("Audio preview is limited to a 60-second work area. Shorten the work area; AVI/WAVE exports support longer bounded output.");
                await output.UnlockAsync(); cancellation.Token.ThrowIfCancellationRequested();
                if (_preparedAudio != key)
                {
                    _preparedAudio = null;
                    ShowStatus("Preparing audio preview…");
                    var wave = await mixer.WaveAsync(comp.WorkStart, comp.WorkEnd - comp.WorkStart, maximumBytes: 12 * 1024 * 1024, cancellationToken: cancellation.Token);
                    await output.LoadAsync(wave); cancellation.Token.ThrowIfCancellationRequested();
                    if (generation != _playGeneration) return;
                    _preparedAudio = key;
                }
                if (generation != _playGeneration) return;
                output.Play(Session.Time - comp.WorkStart, true); _audioClock = true;
                ShowStatus("Playing · browser audio clock");
            }
            else
            {
                _audioClock = false;
                ShowStatus(mixer.HasAudio ? "Playing silent native preview · audio is included in AVI/WAVE export" : "Playing");
            }
            if (generation != _playGeneration || cancellation.IsCancellationRequested) return;
            _playStart = Session.Time; _playWatch.Restart(); _playTimer.Start(); UpdateTransport(); DiagnosticsChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception) when (generation != _playGeneration) { }
        finally
        {
            if (generation == _playGeneration) _preparingPlayback = false;
            if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null;
            cancellation.Dispose(); DiagnosticsChanged?.Invoke();
        }
    }

    public void Pause()
    {
        _playGeneration++; _previewCancellation?.Cancel(); _preparingPlayback = false;
        _playTimer.Stop(); _playWatch.Stop(); _audioClock = false;
        try { (_storage as IAudioPreview)?.Stop(); } catch (Exception ex) { ShowStatus("Audio output stopped: " + ex.Message, true); }
        UpdateTransport();
    }

    private void PlaybackChanged(ChangeKind kind)
    {
        if (kind == ChangeKind.Document)
        {
            _preparedAudio = null;
            if (IsPlaying || _preparingPlayback) Pause();
        }
        else if (kind == ChangeKind.Preview && Session.IsEditing && (IsPlaying || _preparingPlayback)) Pause();
        else if (kind == ChangeKind.Time && !_playheadTick)
        {
            if (_preparingPlayback) { Pause(); return; }
            if (IsPlaying)
            {
                var comp = Session.Composition;
                if (Session.Time < comp.WorkStart || Session.Time >= comp.WorkEnd) { Pause(); return; }
                if (_audioClock && _storage is IAudioPreview audio) audio.Play(Session.Time - comp.WorkStart, true);
                _playStart = Session.Time; _playWatch.Restart();
            }
        }
    }

    private void Tick()
    {
        if (Session.IsEditing) { Pause(); return; }
        var comp = Session.Composition;
        var offset = _audioClock && _storage is IAudioPreview audio
            ? audio.PositionSeconds : (_playStart - comp.WorkStart + _playWatch.Elapsed.TotalSeconds) % (comp.WorkEnd - comp.WorkStart);
        _playheadTick = true;
        try { Session.SetTime(comp.WorkStart + offset); }
        finally { _playheadTick = false; }
    }
}
