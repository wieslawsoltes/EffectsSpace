using System.Globalization;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using SkiaSharp;
using Windows.Foundation;

namespace EffectsSpace.Timeline;

public sealed partial class TimelineView
{
    private void Draw(SKCanvas canvas, Size area)
    {
        Layout(); var width = (float)area.Width; var height = (float)area.Height; var axis = Axis; var comp = Session.Composition;
        Studio.Rect(canvas, 0, 0, width, height, "#202020");
        var save = canvas.Save(); canvas.ClipRect(new(0, (float)RulerHeight, width, Math.Max((float)RulerHeight, height - 12)));
        foreach (var row in _rows)
        {
            if (row.Y + row.Height < ScrollY + RulerHeight || row.Y - ScrollY > height) continue;
            var y = (float)(row.Y - ScrollY); var selected = Session.SelectedIds.Contains(row.Layer.Id);
            Studio.Rect(canvas, 0, y, (float)HeaderWidth, (float)RowHeight, selected ? "#36404D" : row.IsProperty ? "#242424" : "#292929");
            Studio.Rect(canvas, (float)HeaderWidth, y, width - (float)HeaderWidth, (float)RowHeight, selected ? "#252B32" : "#212121");
            Studio.Rect(canvas, 0, y + (float)RowHeight - 1, width, 1, "#191919");
            if (!row.IsProperty)
            {
                if (row.Layer.Enabled) StudioIcon.Draw(canvas, IconKind.Eye, 7, y + 5, 13, Studio.Muted);
                if (row.Layer.Solo) { using var dot = new SKPaint { Color = SKColor.Parse("#EAC679"), IsAntialias = true }; canvas.DrawCircle(34, y + 11, 3, dot); }
                if (row.Layer.Locked) StudioIcon.Draw(canvas, IconKind.Lock, 46, y + 5, 12, Studio.Muted);
                Studio.DrawText(canvas, _expanded.Contains(row.Layer.Id) ? "▾" : "▸", 66, y + 16, 12, Studio.Muted);
                Studio.Rect(canvas, 84, y + 5, 6, 13, row.Layer.Label);
                Studio.DrawText(canvas, (comp.Layers.IndexOf(row.Layer) + 1).ToString(), 98, y + 16, 10, Studio.Muted);
                var icon = row.Layer.Kind switch { LayerKind.Text => IconKind.Text, LayerKind.Composition => IconKind.Composition, LayerKind.Ellipse => IconKind.Ellipse, LayerKind.Audio => IconKind.Audio, _ => IconKind.Rectangle };
                StudioIcon.Draw(canvas, icon, 119, y + 5, 12, row.Layer.Label);
                var textSave = canvas.Save(); canvas.ClipRect(new(136, y, (float)HeaderWidth - 83, y + 23)); Studio.DrawText(canvas, row.Layer.Name, 138, y + 16); canvas.RestoreToCount(textSave);
                Studio.DrawText(canvas, row.Layer.Blend.ToString(), (float)HeaderWidth - 76, y + 16, 10, Studio.Muted);
                if (!GraphMode)
                {
                    var x = (float)Math.Max(HeaderWidth, axis.ToX(row.Layer.InPoint)); var right = (float)Math.Min(width, axis.ToX(row.Layer.OutPoint));
                    if (right > x)
                    {
                        Studio.Rect(canvas, x, y + 4, right - x, 15, row.Layer.Enabled ? row.Layer.Label : "#555555");
                        if (selected) Studio.Rect(canvas, x, y + 4, right - x, 15, "#DAE8F8", true);
                        if (right - x > 100) { var clip = canvas.Save(); canvas.ClipRect(new(x + 4, y, right - 4, y + 23)); Studio.DrawText(canvas, row.Layer.Name, x + 6, y + 15, 9, "#222027"); canvas.RestoreToCount(clip); }
                    }
                }
            }
            else if (row.Descriptor is { } descriptor)
            {
                var channel = descriptor.Channel;
                StudioIcon.Draw(canvas, IconKind.Stopwatch, 85, y + 4, 14, channel.Keys.Count > 0 ? Studio.Accent : Studio.Muted);
                var clip = canvas.Save(); canvas.ClipRect(new(113, y, (float)HeaderWidth - 82, y + 23));
                Studio.DrawText(canvas, descriptor.Name, 116, y + 16, 10, selected && Session.Property == row.Property ? "#FFFFFF" : descriptor.EffectKind is null ? Studio.TextColor : "#C4A9EA"); canvas.RestoreToCount(clip);
                Studio.DrawText(canvas, CurveEvaluator.Evaluate(channel, Session.Time, comp.Layers.IndexOf(row.Layer) + 1).ToString("0.##", CultureInfo.InvariantCulture), (float)HeaderWidth - 76, y + 16, 10, Studio.Accent);
                if (!GraphMode) foreach (var key in channel.Keys) DrawKey(canvas, key, (float)axis.ToX(key.Time), y + 11);
            }
        }
        canvas.RestoreToCount(save);
        if (GraphMode) DrawGraph(canvas, width, height);
        DrawRuler(canvas, width, height);
        if (_drag == "marquee")
        {
            var box = MarqueeRectangle();
            using var fill = new SKPaint { Color = new SKColor(110,175,242,32), IsAntialias = false }; canvas.DrawRect(box, fill);
            fill.Style = SKPaintStyle.Stroke; fill.StrokeWidth = 1; fill.Color = SKColor.Parse(Studio.Accent); canvas.DrawRect(box, fill);
        }
        Studio.Rect(canvas, 0, height - 12, width, 12, "#1C1C1C");
        Studio.DrawText(canvas, $"{comp.Layers.Count} layers   {Session.SelectedKeyIds.Count} keys selected", 8, height - 3, 9, Studio.Muted);
        var total = Math.Max(comp.Duration, (width - HeaderWidth) / PixelsPerSecond); var visible = (width - HeaderWidth) / PixelsPerSecond;
        var track = width - (float)HeaderWidth; var thumb = Math.Max(20, (float)(visible / total * track)); var left = (float)HeaderWidth + (float)(ScrollSeconds / total * track);
        Studio.Rect(canvas, left, height - 9, Math.Max(0, Math.Min(thumb, width - left)), 5, "#5A5A5A");
    }
    private void DrawRuler(SKCanvas canvas, float width, float height)
    {
        var axis = Axis; var comp = Session.Composition;
        Studio.Rect(canvas, 0, 0, width, (float)RulerHeight, "#282828");
        Studio.DrawText(canvas, comp.FrameRate.Timecode(Session.Time), 11, 25, 21, Studio.Accent);
        Studio.DrawText(canvas, $"{comp.FrameRate.Frame(Session.Time):00000}  ({comp.FrameRate})", 13, 42, 10, Studio.Muted);
        Studio.DrawText(canvas, "Source Name / Property", 139, 55, 9, Studio.Muted); Studio.DrawText(canvas, "Mode / Value", (float)HeaderWidth - 76, 55, 9, Studio.Muted);
        var save = canvas.Save(); canvas.ClipRect(new((float)HeaderWidth, 0, width, height));
        var candidates = new[] { comp.FrameRate.Seconds(1), .1, .25, .5, 1, 2, 5, 10, 30, 60, 120, 300, 600 };
        var step = candidates.FirstOrDefault(s => s * PixelsPerSecond >= 62); if (step == 0) step = 600;
        var first = Math.Floor(ScrollSeconds / step) * step;
        using var line = new SKPaint { Color = SKColor.Parse("#333333"), StrokeWidth = 1 };
        for (var time = first; time <= ScrollSeconds + (width - HeaderWidth) / PixelsPerSecond + step; time += step)
        {
            var x = (float)axis.ToX(time); canvas.DrawLine(x, 34, x, height - 12, line);
            if (time >= 0) Studio.DrawText(canvas, step < 1 ? $"{time:0.##}s" : $"{time:0}s", x + 4, 31, 10, Studio.Muted);
            for (var j = 1; j < 4; j++) { var tick = (float)axis.ToX(time + step * j / 4); canvas.DrawLine(tick, 39, tick, 45, line); }
        }
        var start = (float)axis.ToX(comp.WorkStart); var end = (float)axis.ToX(comp.WorkEnd);
        Studio.Rect(canvas, start, 5, end - start, 8, "#737E8C"); Studio.Rect(canvas, start, 4, 5, 11, "#CFD4DA"); Studio.Rect(canvas, end - 5, 4, 5, 11, "#CFD4DA");
        foreach (var marker in comp.Markers)
        { var x = (float)axis.ToX(marker.Time); StudioIcon.Draw(canvas, IconKind.Keyframe, x - 4, 44, 8, marker.Color); Studio.DrawText(canvas, marker.Name, x + 7, 53, 8, marker.Color); }
        var play = (float)axis.ToX(Session.Time); line.Color = SKColor.Parse(Studio.Accent); canvas.DrawLine(play, 18, play, height - 12, line);
        using var head = new SKPath(); head.MoveTo(play - 5, 17); head.LineTo(play + 5, 17); head.LineTo(play + 5, 24); head.LineTo(play, 29); head.LineTo(play - 5, 24); head.Close();
        using var fill = new SKPaint { Color = SKColor.Parse(Studio.Accent), IsAntialias = true }; canvas.DrawPath(head, fill);
        canvas.RestoreToCount(save); Studio.Rect(canvas, (float)HeaderWidth - 1, 0, 1, height, "#101010");
    }
    private void DrawKey(SKCanvas canvas, Keyframe key, float x, float y)
    {
        if (x < HeaderWidth + 2 || x > ActualWidth) return;
        var color = Session.SelectedKeyIds.Contains(key.Id) ? "#FFD374" : Studio.Accent;
        if (key.Interpolation == Interpolation.Hold) Studio.Rect(canvas, x - 4, y - 4, 8, 8, color);
        else StudioIcon.Draw(canvas, key.Interpolation == Interpolation.Bezier ? IconKind.Diamond : IconKind.Keyframe, x - 5, y - 5, 10, color);
    }
    private SKRect MarqueeRectangle() => new((float)Math.Max(HeaderWidth, Math.Min(_down.X, _pointer.X)),
        (float)Math.Max(RulerHeight, Math.Min(_down.Y, _pointer.Y)), (float)Math.Min(ActualWidth, Math.Max(_down.X, _pointer.X)),
        (float)Math.Min(ActualHeight - 12, Math.Max(_down.Y, _pointer.Y)));
}
