using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Documents;

namespace EffectsSpace.Editing;

public static class EditorCommands
{
    public static Layer NewLayer(Composition comp, LayerKind kind)
    {
        var layer = new Layer { Name = $"{kind} {comp.Layers.Count(l => l.Kind == kind) + 1}", Kind = kind, OutPoint = comp.Duration };
        if (kind is LayerKind.Solid or LayerKind.Adjustment) { layer.Width = comp.Width; layer.Height = comp.Height; layer.Fill = "#283244"; }
        if (kind == LayerKind.Text) { layer.Width = 720; layer.Height = 140; layer.Fill = "#FFFFFF"; layer.Label = "#D78383"; }
        if (kind == LayerKind.Ellipse) layer.Height = layer.Width;
        if (kind == LayerKind.Null) { layer.Width = 100; layer.Height = 100; layer.Label = "#DC6B67"; }
        if (kind == LayerKind.Path) layer.Path = ShapePath.Rectangle(layer.Width, layer.Height);
        layer.Transform.X.Value = comp.Width / 2d; layer.Transform.Y.Value = comp.Height / 2d;
        layer.Transform.AnchorX.Value = layer.Width / 2; layer.Transform.AnchorY.Value = layer.Height / 2;
        return layer;
    }
    public static Layer AddLayer(this EditorSession session, LayerKind kind)
    {
        if (kind is LayerKind.Composition or LayerKind.Image or LayerKind.Video or LayerKind.Audio) throw new ArgumentException("Use the source-aware import command.");
        var layer = NewLayer(session.Composition, kind);
        session.Edit("New " + kind, () => session.Composition.Layers.Insert(0, layer)); session.Select(layer.Id); return layer;
    }
    public static void DeleteSelection(this EditorSession session)
    {
        var ids = session.Selection.Where(l => !l.Locked).Select(l => l.Id).ToHashSet();
        if (ids.Count == 0) return;
        session.Edit("Delete layers", () =>
        {
            session.Composition.Layers.RemoveAll(l => ids.Contains(l.Id));
            foreach (var l in session.Composition.Layers) { if (l.ParentId is { } p && ids.Contains(p)) l.ParentId = null; if (l.MatteId is { } m && ids.Contains(m)) l.MatteId = null; }
        });
    }
    public static void DuplicateSelection(this EditorSession session)
    {
        var originals = session.Selection.Where(l => !l.Locked).ToArray(); if (originals.Length == 0) return;
        var copies = originals.Select(CloneForInsert).ToArray(); var map = originals.Zip(copies).ToDictionary(p => p.First.Id, p => p.Second.Id);
        session.Edit("Duplicate layers", () =>
        {
            for (var i = 0; i < originals.Length; i++)
            {
                var copy = copies[i]; if (copy.ParentId is { } p && map.TryGetValue(p, out var cp)) copy.ParentId = cp;
                if (copy.MatteId is { } m && map.TryGetValue(m, out var cm)) copy.MatteId = cm;
                session.Composition.Layers.Insert(session.Composition.Layers.IndexOf(originals[i]), copy);
            }
            session.Select(null); foreach (var copy in copies) session.SelectedIds.Add(copy.Id);
        });
    }
    public static Layer CloneForInsert(Layer source)
    {
        var copy = ProjectJson.CloneLayer(source); copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " copy";
        foreach (var (_, channel) in copy.Transform.Channels()) foreach (var key in channel.Keys) key.Id = Guid.NewGuid().ToString("N");
        foreach (var effect in copy.Effects) { effect.Id = Guid.NewGuid().ToString("N"); foreach (var c in effect.Parameters.Values) foreach (var k in c.Keys) k.Id = Guid.NewGuid().ToString("N"); }
        foreach (var key in copy.TimeRemap.Keys) key.Id = Guid.NewGuid().ToString("N");
        foreach (var mask in copy.Masks) mask.Id = Guid.NewGuid().ToString("N");
        return copy;
    }
    public static void SetProperty(this EditorSession session, string property, double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        session.Edit("Set " + property, () =>
        {
            foreach (var layer in session.Selection.Where(l => !l.Locked))
                if (LayerChannels.Find(layer, property) is { } descriptor)
                    SetChannel(descriptor.Channel, session.Time, Math.Clamp(value, descriptor.Minimum, descriptor.Maximum), session.AutoKey);
        });
    }
    public static void SetChannel(Channel channel, double time, double value, bool autoKey)
    { if (autoKey || channel.Keys.Count > 0) channel.SetKey(time, value); else channel.Value = value; }
    public static void ToggleAnimation(this EditorSession session, string property)
    {
        session.Edit("Toggle " + property + " animation", () =>
        {
            foreach (var layer in session.Selection.Where(l => !l.Locked))
            {
                if (LayerChannels.Find(layer, property) is not { } descriptor) continue;
                var channel = descriptor.Channel; var value = CurveEvaluator.Evaluate(channel, session.Time, session.Composition.Layers.IndexOf(layer) + 1);
                if (channel.Keys.Count == 0) channel.SetKey(session.Time, value); else { channel.Value = value; channel.Keys.Clear(); }
            }
        });
    }
    public static void AddKey(this EditorSession session, string property)
    {
        session.Edit("Add keyframe", () =>
        {
            var keys = new List<string>();
            foreach (var layer in session.Selection.Where(l => !l.Locked))
            {
                if (LayerChannels.Find(layer, property) is not { } descriptor) continue;
                var channel = descriptor.Channel;
                channel.SetKey(session.Time, CurveEvaluator.Evaluate(channel, session.Time, session.Composition.Layers.IndexOf(layer) + 1));
                keys.Add(channel.Keys.First(k => Math.Abs(k.Time - session.Time) < 1e-8).Id);
            }
            session.SelectKeys(keys);
        });
    }
    public static void SetInterpolation(this EditorSession session, Interpolation interpolation) => session.InterpolateKeys(interpolation);
    public static void DeleteSelectedKey(this EditorSession session) => session.DeleteKeys();
    public static void SplitSelection(this EditorSession session)
    {
        var layers = session.Selection.Where(l => !l.Locked && session.Time > l.InPoint && session.Time < l.OutPoint).ToArray();
        session.Edit("Split layers", () =>
        {
            foreach (var layer in layers) { var next = CloneForInsert(layer); next.Name = layer.Name + " / 2"; next.InPoint = session.Time; layer.OutPoint = session.Time; session.Composition.Layers.Insert(session.Composition.Layers.IndexOf(layer), next); }
        });
    }
    public static void TrimSelection(this EditorSession session, bool start)
    {
        session.Edit(start ? "Trim in point" : "Trim out point", () =>
        {
            var frame = session.Composition.FrameRate.Seconds(1);
            foreach (var layer in session.Selection.Where(l => !l.Locked))
                if (start) layer.InPoint = Math.Clamp(session.Time, 0, layer.OutPoint - frame);
                else layer.OutPoint = Math.Clamp(session.Time + frame, layer.InPoint + frame, session.Composition.Duration);
        });
    }
    public static void Reorder(this EditorSession session, int direction)
    {
        session.Edit("Reorder layers", () =>
        {
            var list = session.Composition.Layers;
            var selected = session.Selection.Where(l => !l.Locked).ToArray(); if (direction > 0) Array.Reverse(selected);
            foreach (var layer in selected) { var i = list.IndexOf(layer); var next = i + Math.Sign(direction); if (next < 0 || next >= list.Count || session.SelectedIds.Contains(list[next].Id)) continue; list.RemoveAt(i); list.Insert(next, layer); }
        });
    }
    public static void AddEffect(this EditorSession session, EffectKind kind)
    { session.Edit("Add " + EffectCatalog.Get(kind).Name, () => { foreach (var l in session.Selection.Where(l => !l.Locked)) l.Effects.Add(EffectCatalog.Create(kind)); }); }
    public static void AddMask(this EditorSession session)
    {
        session.Edit("Add rectangular mask", () =>
        {
            foreach (var layer in session.Selection.Where(l => !l.Locked))
            {
                var path = ShapePath.Rectangle(layer.Width * 0.8, layer.Height * 0.8);
                foreach (var n in path.Nodes) n.Point += new Vec2(layer.Width * 0.1, layer.Height * 0.1);
                layer.Masks.Add(new LayerMask { Name = "Mask " + (layer.Masks.Count + 1), Path = path });
            }
        });
    }
    public static string Precompose(this EditorSession session)
    {
        var selected = session.Selection.Where(l => !l.Locked).ToList(); if (selected.Count == 0) throw new InvalidOperationException("Select at least one unlocked layer.");
        var ids = selected.Select(l => l.Id).ToHashSet();
        if (session.Composition.Layers.Any(l => (l.ParentId is { } p && ids.Contains(l.Id) != ids.Contains(p)) || (l.MatteId is { } m && ids.Contains(l.Id) != ids.Contains(m))))
            throw new InvalidOperationException("Include linked parents and mattes before precomposing.");
        var parent = session.Composition;
        var nested = new Composition { Name = "Pre-comp " + session.Project.Compositions.Count, Width = parent.Width, Height = parent.Height, FrameRate = parent.FrameRate, Duration = parent.Duration, WorkStart = parent.WorkStart, WorkEnd = parent.WorkEnd, Background = parent.Background, Layers = selected };
        var wrapper = new Layer { Name = nested.Name, Kind = LayerKind.Composition, SourceId = nested.Id, Width = nested.Width, Height = nested.Height, OutPoint = parent.Duration, Label = "#92B3E8" };
        session.Edit("Pre-compose", () => { var index = parent.Layers.IndexOf(selected[0]); parent.Layers.RemoveAll(l => ids.Contains(l.Id)); parent.Layers.Insert(index, wrapper); session.Project.Compositions.Add(nested); });
        session.Select(wrapper.Id); return nested.Id;
    }
    public static void SetParent(this EditorSession session, string? parentId)
    { session.Edit("Parent layers", () => { foreach (var layer in session.Selection.Where(l => !l.Locked)) layer.ParentId = parentId; }); }
    public static void SetWorkArea(this EditorSession session, bool start)
    {
        session.Edit("Set work area", () =>
        {
            var c = session.Composition; var frame = c.FrameRate.Seconds(1);
            if (start) c.WorkStart = Math.Min(session.Time, c.WorkEnd - frame); else c.WorkEnd = Math.Max(session.Time + frame, c.WorkStart + frame);
        });
    }
    public static void AddMarker(this EditorSession session, string name = "Marker")
    { session.Edit("Add marker", () => session.Composition.Markers.Add(new CompositionMarker { Name = name, Time = session.Time })); }
}
