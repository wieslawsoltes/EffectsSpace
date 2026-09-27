using System.Numerics;
using EffectsSpace.Core;

namespace EffectsSpace.Rendering;

public static class RenderPlanner
{
    public static RenderPlan Build(Composition comp, double time)
    {
        var frame = new CompositionFrame(comp, time, includeGuides: true);
        return new(comp, time, frame.Layers);
    }
    public static Layer? HitTest(Composition comp, double time, Vec2 point)
    {
        foreach (var item in Build(comp, time).Layers.Reverse())
        {
            var l = item.Layer; if (l.Locked || l.Kind == LayerKind.Adjustment || item.Opacity <= 0) continue;
            if (!Matrix3x2.Invert(item.World, out var inverse)) continue;
            var local = Vector2.Transform(new((float)point.X, (float)point.Y), inverse);
            var p = new Vec2(local.X, local.Y);
            if (!new RectD(0, 0, l.Width, l.Height).Contains(p)) continue;
            if (l.Kind == LayerKind.Ellipse)
            {
                var x = (p.X - l.Width / 2) / (l.Width / 2); var y = (p.Y - l.Height / 2) / (l.Height / 2);
                if (x * x + y * y > 1) continue;
            }
            return l;
        }
        return null;
    }
    public static IEnumerable<double> ExportTimes(Composition comp, bool workArea = true, int maximumFrames = 3600)
    {
        var start = workArea ? comp.FrameRate.Frame(comp.WorkStart) : 0;
        var end = workArea ? comp.FrameRate.Frame(comp.WorkEnd) : comp.FrameCount;
        var count = end - start;
        if (count <= 0 || count > maximumFrames) throw new InvalidOperationException($"Export must contain 1–{maximumFrames} frames.");
        for (var frame = start; frame < end; frame++) yield return comp.FrameRate.Seconds(frame);
    }
}
