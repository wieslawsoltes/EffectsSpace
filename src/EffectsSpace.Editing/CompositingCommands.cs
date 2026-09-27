using EffectsSpace.Animation;
using EffectsSpace.Core;

namespace EffectsSpace.Editing;

public static class CompositingCommands
{
    public static void ToggleGuide(this EditorSession session) => session.Edit("Toggle guide layer", () =>
    {
        foreach (var layer in session.Selection.Where(l => !l.Locked)) layer.Guide = !layer.Guide;
    });

    public static void EnableTimeRemap(this EditorSession session)
    {
        var layers = session.Selection.Where(l => !l.Locked).ToArray();
        if (layers.Length == 0 || layers.Any(l => l.Kind is not (LayerKind.Composition or LayerKind.Video or LayerKind.Audio)))
            throw new InvalidOperationException("Select unlocked time-based source layers to enable Time Remap.");
        session.Edit("Enable Time Remap", () =>
        {
            foreach (var layer in layers)
            {
                if (layer.TimeRemapEnabled) continue;
                var first = layer.InPoint;
                var last = Math.Max(first, layer.OutPoint - session.Composition.FrameRate.Seconds(1));
                layer.TimeRemap = new Channel(layer.SourceTime(session.Time));
                layer.TimeRemap.SetKey(first, layer.SourceTime(first), Interpolation.Linear);
                if (last > first) layer.TimeRemap.SetKey(last, layer.SourceTime(last), Interpolation.Linear);
                layer.TimeRemapEnabled = true;
            }
        });
        session.Property = "TimeRemap";
    }

    public static void FreezeFrame(this EditorSession session)
    {
        var layer = session.Primary ?? throw new InvalidOperationException("Select a time-based layer.");
        if (layer.Locked || layer.Kind is not (LayerKind.Composition or LayerKind.Video or LayerKind.Audio))
            throw new InvalidOperationException("Freeze Frame requires an unlocked time-based source.");
        var sourceTime = LayerTime.Evaluate(layer, session.Time, session.Composition.Layers.IndexOf(layer) + 1);
        session.Edit("Freeze Frame", () => { layer.TimeRemapEnabled = true; layer.TimeRemap = new Channel(sourceTime); });
        session.Property = "TimeRemap";
    }

    public static void ReverseTime(this EditorSession session)
    {
        var layers = session.Selection.Where(l => !l.Locked).ToArray();
        if (layers.Length == 0 || layers.Any(l => l.Kind is not (LayerKind.Composition or LayerKind.Video or LayerKind.Audio)))
            throw new InvalidOperationException("Time Reverse requires unlocked time-based source layers.");
        session.Edit("Time Reverse", () =>
        {
            foreach (var layer in session.Selection.Where(l => !l.Locked))
            {
                if (!layer.TimeRemapEnabled)
                {
                    var last = Math.Max(layer.InPoint, layer.OutPoint - session.Composition.FrameRate.Seconds(1));
                    layer.TimeRemap = new Channel(layer.SourceTime(session.Time));
                    layer.TimeRemap.SetKey(layer.InPoint, layer.SourceTime(layer.InPoint), Interpolation.Linear);
                    if (last > layer.InPoint) layer.TimeRemap.SetKey(last, layer.SourceTime(last), Interpolation.Linear);
                    layer.TimeRemapEnabled = true;
                }
                if (layer.TimeRemap.Expression.Length > 0) throw new InvalidOperationException("Bake or remove the Time Remap expression before reversing.");
                var keys = layer.TimeRemap.Keys;
                if (keys.Count < 2) continue;
                var original = keys.Select(k => (k.Time, k.Interpolation, k.X1, k.Y1, k.X2, k.Y2)).ToArray();
                if (original.Take(original.Length - 1).Any(k => k.Interpolation == Interpolation.Hold))
                    throw new InvalidOperationException("Reverse of discontinuous Hold segments requires baking; no keys have been changed.");
                var end = keys[0].Time + keys[^1].Time;
                foreach (var key in keys) key.Time = end - key.Time;
                keys.Reverse();
                for (var i = 0; i + 1 < keys.Count; i++)
                {
                    var segment = original[original.Length - 2 - i];
                    keys[i].Interpolation = segment.Interpolation;
                    keys[i].X1 = 1 - segment.X2; keys[i].Y1 = 1 - segment.Y2;
                    keys[i].X2 = 1 - segment.X1; keys[i].Y2 = 1 - segment.Y1;
                }
            }
        });
        session.Property = "TimeRemap";
    }
}
