using EffectsSpace.Core;

namespace EffectsSpace.Editing;

/// <summary>One atomic drag across any number of layers/properties. Invalid previews never partially mutate a group.</summary>
public sealed class KeyframeMove : IDisposable
{
    private sealed record Start(KeyframeTarget Target, double Time, double Value);
    private readonly EditorSession _session;
    private readonly Start[] _starts;
    private readonly HashSet<string> _ids;
    private bool _finished;
    public double AppliedTimeDelta { get; private set; }
    public double AppliedValueDelta { get; private set; }

    public KeyframeMove(EditorSession session)
    {
        _session = session;
        _starts = session.SelectedKeys().Where(t => !t.Layer.Locked).Select(t => new Start(t, t.Key.Time, t.Key.Value)).ToArray();
        if (_starts.Length == 0) throw new InvalidOperationException("Select unlocked keyframes first.");
        _ids = _starts.Select(s => s.Target.Key.Id).ToHashSet(StringComparer.Ordinal);
        session.BeginEdit("Move keyframes");
    }
    public bool TryPreview(double seconds, double valueDelta)
    {
        if (_finished) throw new ObjectDisposedException(nameof(KeyframeMove));
        if (!double.IsFinite(seconds) || !double.IsFinite(valueDelta)) return false;
        var min = _starts.Min(s => s.Time); var max = _starts.Max(s => s.Time);
        var delta = Math.Clamp(_session.Composition.FrameRate.Snap(seconds), -min, Math.Max(max, _session.Composition.LastFrameTime) - max);
        var minimumValueDelta = _starts.Max(s => Math.Min(s.Target.Property.Minimum, s.Value) - s.Value);
        var maximumValueDelta = _starts.Min(s => Math.Max(s.Target.Property.Maximum, s.Value) - s.Value);
        var dv = Math.Clamp(valueDelta, minimumValueDelta, maximumValueDelta);
        // Check against unselected keys in every participating channel before writing any key.
        foreach (var start in _starts)
            if (start.Target.Property.Channel.Keys.Any(k => !_ids.Contains(k.Id) && Math.Abs(k.Time - (start.Time + delta)) < 1e-8)) return false;
        foreach (var start in _starts)
        { start.Target.Key.Time = start.Time + delta; start.Target.Key.Value = start.Value + dv; }
        foreach (var channel in _starts.Select(s => s.Target.Property.Channel).Distinct()) channel.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        AppliedTimeDelta = delta; AppliedValueDelta = dv; _session.PreviewChanged(); return true;
    }
    public void Commit()
    {
        if (_finished) return;
        try { _session.CommitEdit(); } finally { _finished = true; }
    }
    public void Dispose()
    {
        if (_finished) return; _finished = true; _session.CancelEdit();
    }
}
