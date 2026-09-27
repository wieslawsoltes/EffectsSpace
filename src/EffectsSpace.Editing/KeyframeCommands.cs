using EffectsSpace.Animation;
using EffectsSpace.Core;

namespace EffectsSpace.Editing;

public enum EaseDirection { Both, In, Out }

public static class KeyframeCommands
{
    public static KeyframeTarget[] SelectedKeys(this EditorSession session) => session.Selection
        .SelectMany(l => LayerChannels.Enumerate(l).SelectMany(p => p.Channel.Keys
            .Where(k => session.SelectedKeyIds.Contains(k.Id)).Select(k => new KeyframeTarget(l, p, k)))).ToArray();

    public static void SelectPropertyKeys(this EditorSession session)
    {
        var ids = session.Selection.Where(l => !l.Locked).SelectMany(l => LayerChannels.Find(l, session.Property)?.Channel.Keys ?? []).Select(k => k.Id).ToArray();
        session.SelectKeys(ids);
    }

    public static void CopyKeys(this EditorSession session)
    {
        var selected = session.SelectedKeys();
        if (selected.Length == 0) throw new InvalidOperationException("Select one or more keyframes to copy.");
        var origin = selected.Min(t => t.Key.Time);
        var layers = selected.Select(t => t.Layer.Id).Distinct().ToArray();
        var tracks = selected.GroupBy(t => (t.Layer.Id, t.Property.Path)).Select(group =>
        {
            var property = group.First().Property;
            return new ClipboardTrack(Array.IndexOf(layers, group.Key.Id), property.Path, property.EffectKind, property.EffectOccurrence,
                property.Parameter, Array.AsReadOnly(group.OrderBy(t => t.Key.Time).Select(t => KeyframeValue.Capture(t.Key, origin)).ToArray()));
        });
        session.KeyClipboard = new(tracks, layers.Length);
    }

    public static void CutKeys(this EditorSession session)
    {
        if (session.SelectedKeys().Any(k => k.Layer.Locked)) throw new InvalidOperationException("Unlock all selected keyframe layers before cutting.");
        session.CopyKeys(); session.DeleteKeys();
    }

    public static void PasteKeys(this EditorSession session, bool toCurrentProperty = false)
    {
        var clipboard = session.KeyClipboard ?? throw new InvalidOperationException("The keyframe clipboard is empty.");
        var targets = session.Selection.Where(l => !l.Locked).ToArray();
        if (targets.Length == 0) throw new InvalidOperationException("Select an unlocked destination layer.");
        if (clipboard.LayerCount > 1 && targets.Length != clipboard.LayerCount)
            throw new InvalidOperationException($"Select {clipboard.LayerCount} destination layers in stacking order.");
        if (toCurrentProperty && clipboard.Tracks.Count != 1)
            throw new InvalidOperationException("Paste to current property requires a single copied channel.");
        var writes = new List<(Channel Channel, Keyframe Key)>();
        foreach (var track in clipboard.Tracks)
        foreach (var layer in clipboard.LayerCount == 1 ? targets : new[] { targets[track.LayerSlot] })
        {
            AnimatedProperty? destination;
            if (toCurrentProperty) destination = LayerChannels.Find(layer, session.Property);
            else if (track.EffectKind is null) destination = LayerChannels.Find(layer, track.Property);
            else destination = LayerChannels.Enumerate(layer).FirstOrDefault(p => p.EffectKind == track.EffectKind && p.EffectOccurrence == track.EffectOccurrence && p.Parameter == track.Parameter);
            if (destination is null) throw new InvalidOperationException($"{layer.Name} has no matching destination for {track.Parameter ?? track.Property}. Add the matching effect first.");
            foreach (var data in track.Keys)
            {
                var time = session.Composition.FrameRate.Snap(session.Time + data.Time);
                if (time < 0 || time > session.Composition.LastFrameTime + 1e-9)
                    throw new InvalidOperationException("The pasted keyframes would extend beyond the composition. Move the playhead earlier.");
                if (writes.Any(w => ReferenceEquals(w.Channel, destination.Channel) && Math.Abs(w.Key.Time - time) < 1e-8))
                    throw new InvalidOperationException("Two copied keys map to the same destination frame at this frame rate.");
                var key = data.Create(time); key.Value = Math.Clamp(key.Value, destination.Minimum, destination.Maximum);
                writes.Add((destination.Channel, key));
            }
        }
        session.Edit("Paste keyframes", () =>
        {
            foreach (var (channel, key) in writes)
            { channel.Keys.RemoveAll(k => Math.Abs(k.Time - key.Time) < 1e-8); channel.Keys.Add(key); }
            foreach (var channel in writes.Select(w => w.Channel).Distinct()) channel.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
            session.SelectKeys(writes.Select(w => w.Key.Id));
        });
    }

    public static void DeleteKeys(this EditorSession session)
    {
        var selected = session.SelectedKeys().Where(t => !t.Layer.Locked).ToArray();
        if (selected.Length == 0) return;
        session.Edit("Delete keyframes", () =>
        {
            foreach (var group in selected.GroupBy(t => t.Property.Channel))
            {
                var ids = group.Select(t => t.Key.Id).ToHashSet();
                var value = CurveEvaluator.Evaluate(group.Key, session.Time);
                group.Key.Keys.RemoveAll(k => ids.Contains(k.Id));
                if (group.Key.Keys.Count == 0) group.Key.Value = value;
            }
            session.ClearKeySelection();
        });
    }

    public static void InterpolateKeys(this EditorSession session, Interpolation interpolation)
    {
        if (interpolation == Interpolation.Bezier) { session.EaseKeys(EaseDirection.Both); return; }
        var targets = EditingTargets(session);
        if (targets.Length == 0) return;
        session.Edit("Set keyframe interpolation", () => { foreach (var target in targets) target.Key.Interpolation = interpolation; });
    }

    public static void EaseKeys(this EditorSession session, EaseDirection direction)
    {
        var targets = EditingTargets(session);
        if (targets.Length == 0) return;
        session.Edit("Easy Ease " + direction, () =>
        {
            foreach (var target in targets)
            {
                var keys = target.Property.Channel.Keys; var i = keys.IndexOf(target.Key);
                if (direction != EaseDirection.In && i < keys.Count - 1)
                { ToBezier(target.Key); target.Key.X1 = 1d / 3; target.Key.Y1 = 0; }
                if (direction != EaseDirection.Out && i > 0)
                { var previous = keys[i - 1]; ToBezier(previous); previous.X2 = 2d / 3; previous.Y2 = 1; }
            }
        });
    }
    private static void ToBezier(Keyframe key)
    {
        if (key.Interpolation != Interpolation.Bezier)
        { key.X1 = key.Y1 = 1d / 3; key.X2 = key.Y2 = 2d / 3; key.Interpolation = Interpolation.Bezier; }
    }
    private static KeyframeTarget[] EditingTargets(EditorSession session)
    {
        if (session.SelectedKeyIds.Count > 0) return session.SelectedKeys().Where(t => !t.Layer.Locked).ToArray();
        return session.Selection.Where(l => !l.Locked).SelectMany(l =>
        {
            var property = LayerChannels.Find(l, session.Property);
            return property is null ? [] : property.Channel.Keys.Select(k => new KeyframeTarget(l, property, k));
        }).ToArray();
    }
    public static void NavigateKey(this EditorSession session, int direction)
    {
        var times = session.Selection.SelectMany(l => LayerChannels.Find(l, session.Property)?.Channel.Keys ?? [])
            .Select(k => k.Time).Where(t => t >= 0 && t <= session.Composition.LastFrameTime).Distinct();
        var target = direction < 0 ? times.Where(t => t < session.Time - 1e-8).OrderDescending().FirstOrDefault(double.NaN)
            : times.Where(t => t > session.Time + 1e-8).Order().FirstOrDefault(double.NaN);
        if (double.IsFinite(target)) session.SetTime(target);
    }
    public static void NudgeKeys(this EditorSession session, int frames)
    {
        using var edit = new KeyframeMove(session);
        if (!edit.TryPreview(session.Composition.FrameRate.Seconds(frames), 0))
            throw new InvalidOperationException("A keyframe already occupies the destination time.");
        edit.Commit();
    }
}
