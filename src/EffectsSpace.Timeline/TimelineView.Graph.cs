using System.Globalization;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using SkiaSharp;

namespace EffectsSpace.Timeline;

public sealed partial class TimelineView
{
    private sealed record CurveHandle(Channel Channel, Keyframe Left, Keyframe Right, bool Outgoing)
    {
        public double Time => Left.Time + (Right.Time - Left.Time) * (Outgoing ? Left.X1 : Left.X2);
        public double Value => Left.Value + (Right.Value - Left.Value) * (Outgoing ? Left.Y1 : Left.Y2);
        public Keyframe Endpoint => Outgoing ? Left : Right;
    }
    private IEnumerable<CurveHandle> CurveHandles()
    {
        if (GraphKind != GraphKind.Value || GraphProperty is not { } property || GraphLayer?.Locked == true || property.Channel.Expression.Length > 0) yield break;
        var keys = property.Channel.Keys;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var left = keys[i]; var right = keys[i + 1];
            if (left.Interpolation != Interpolation.Bezier || Math.Abs(right.Value - left.Value) < 1e-9) continue;
            if (Session.SelectedKeyIds.Contains(left.Id)) yield return new(property.Channel, left, right, true);
            if (Session.SelectedKeyIds.Contains(right.Id)) yield return new(property.Channel, left, right, false);
        }
    }
    private bool GraphValueAt(Channel channel, double time, out double value)
    {
        var index = GraphLayer is { } layer ? Session.Composition.Layers.IndexOf(layer) + 1 : 1;
        if (GraphKind == GraphKind.Velocity) return CurveVelocity.TryEvaluate(channel, time, out value, index);
        value = CurveEvaluator.Evaluate(channel, time, index); return double.IsFinite(value);
    }
    private void EnsureGraphRange()
    {
        if (!_graphDirty || _drag is "key" or "handle") return;
        _graphDirty = false;
        var channel = GraphProperty?.Channel;
        if (channel is null) { _graphMin = 0; _graphMax = 1; return; }
        var values = new List<double>();
        var start = Math.Max(0, ScrollSeconds); var end = Math.Min(Session.Composition.Duration, Axis.ToTime(Math.Max(HeaderWidth + 1, ActualWidth)));
        for (var i = 0; i <= 192; i++)
            if (GraphValueAt(channel, start + (end - start) * i / 192, out var value)) values.Add(value);
        if (GraphKind == GraphKind.Value)
        {
            values.AddRange(channel.Keys.Select(k => k.Value)); values.AddRange(CurveHandles().Select(h => h.Value));
        }
        else values.Add(0);
        if (values.Count == 0) values.Add(0);
        _graphMin = values.Min(); _graphMax = values.Max();
        if (_graphMax - _graphMin < 1) { _graphMin -= 1; _graphMax += 1; }
        var pad = (_graphMax - _graphMin) * .15; _graphMin -= pad; _graphMax += pad;
    }
    private void DrawGraph(SKCanvas canvas, float width, float height)
    {
        EnsureGraphRange();
        var left = (float)HeaderWidth; var top = (float)RulerHeight; var bottom = height - 20;
        var saved = canvas.Save(); canvas.ClipRect(new(left, top, width, Math.Max(top, bottom)));
        Studio.Rect(canvas, left, top, width - left, bottom - top, "#1D2024");
        var property = GraphProperty;
        if (property is null)
        { Studio.DrawText(canvas, "Select a layer and property to edit its graph.", left + 22, top + 35, 11, Studio.Muted); canvas.RestoreToCount(saved); return; }
        using var grid = new SKPaint { Color = SKColor.Parse("#32363C"), StrokeWidth = 1 };
        for (var i = 0; i < 5; i++)
        {
            var value = _graphMin + (_graphMax - _graphMin) * i / 4; var y = (float)GraphY(value);
            canvas.DrawLine(left, y, width, y, grid); Studio.DrawText(canvas, value.ToString("0.##", CultureInfo.InvariantCulture), left + 5, y - 3, 9, Studio.Muted);
        }
        using var path = new SKPath(); var first = true;
        for (var x = left; x <= width; x += 2)
        {
            if (!GraphValueAt(property.Channel, Axis.ToTime(x), out var value)) { first = true; continue; }
            var y = (float)GraphY(value);
            if (first) { path.MoveTo(x, y); first = false; } else path.LineTo(x, y);
        }
        var color = GraphKind == GraphKind.Value ? "#81D9AE" : "#E7B16F";
        using var curve = new SKPaint { Color = SKColor.Parse(color), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f }; canvas.DrawPath(path, curve);
        using var handlePaint = new SKPaint { Color = SKColor.Parse("#FFD374"), IsAntialias = true, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
        foreach (var handle in CurveHandles())
        {
            var x = (float)Axis.ToX(handle.Time); var y = (float)GraphY(handle.Value);
            canvas.DrawLine((float)Axis.ToX(handle.Endpoint.Time), (float)GraphY(handle.Endpoint.Value), x, y, handlePaint);
            canvas.DrawCircle(x, y, 4, handlePaint);
        }
        foreach (var key in property.Channel.Keys)
        {
            var value = key.Value;
            if (GraphKind == GraphKind.Velocity && !GraphValueAt(property.Channel, key.Time, out value)) continue;
            DrawKey(canvas, key, (float)Axis.ToX(key.Time), (float)GraphY(value));
        }
        var mode = GraphKind == GraphKind.Value ? "Value" : property.Channel.Expression.Length > 0 ? "Velocity estimate · units/s" : "Velocity · units/s";
        Studio.DrawText(canvas, property.Name + "  /  " + mode, left + 80, top + 17, 10, color);
        Studio.DrawText(canvas, GraphKind == GraphKind.Value ? "Shift-click: multi-select · Drag empty area: marquee · Ctrl-click: add key · Drag circles: easing" : "Drag keys horizontally to retime · Discontinuities have undefined velocity", left + 14, height - 26, 9, Studio.Muted);
        canvas.RestoreToCount(saved);
    }
    private double GraphY(double value) => RulerHeight + 29 + (1 - (value - _graphMin) / (_graphMax - _graphMin)) * Math.Max(1, ActualHeight - RulerHeight - 76);
    private double GraphValue(double y) => _graphMin + (1 - (y - RulerHeight - 29) / Math.Max(1, ActualHeight - RulerHeight - 76)) * (_graphMax - _graphMin);
    /// <summary>Read-only geometry for automation and embedding. Does not mutate the editor.</summary>
    public IReadOnlyList<(string Id, double X, double Y)> VisibleKeyPositions()
    {
        EnsureGraphRange();
        if (GraphMode)
        {
            var channel = GraphProperty?.Channel; if (channel is null) return [];
            return channel.Keys.Select(key =>
            {
                var valid = GraphValueAt(channel, key.Time, out var value);
                return (Id: key.Id, X: Axis.ToX(key.Time), Y: valid ? GraphY(GraphKind == GraphKind.Value ? key.Value : value) : double.NaN);
            }).Where(k => double.IsFinite(k.Y) && k.X >= HeaderWidth && k.X <= ActualWidth && k.Y >= RulerHeight && k.Y < ActualHeight - 12).ToArray();
        }
        return _rows.Where(r => r.Descriptor is not null && r.Y - ScrollY >= RulerHeight && r.Y - ScrollY < ActualHeight - 12)
            .SelectMany(r => r.Descriptor!.Channel.Keys.Select(k => (Id: k.Id, X: Axis.ToX(k.Time), Y: r.Y - ScrollY + 11)))
            .Where(k => k.X >= HeaderWidth && k.X <= ActualWidth).ToArray();
    }
    public IReadOnlyList<(string LeftId, string RightId, bool Outgoing, double X, double Y)> VisibleHandlePositions()
    {
        EnsureGraphRange();
        if (!GraphMode) return [];
        return CurveHandles().Select(h => (LeftId: h.Left.Id, RightId: h.Right.Id, h.Outgoing, X: Axis.ToX(h.Time), Y: GraphY(h.Value)))
            .Where(h => h.X >= HeaderWidth && h.X <= ActualWidth && h.Y >= RulerHeight && h.Y < ActualHeight - 12).ToArray();
    }
}
