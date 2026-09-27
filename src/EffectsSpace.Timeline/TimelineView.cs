using System.Globalization;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace EffectsSpace.Timeline;

public sealed class TimelineView : UserControl, IDisposable
{
    private sealed class Surface(TimelineView owner) : SKCanvasElement
    { protected override void RenderOverride(SKCanvas canvas, Size area) => owner.Draw(canvas, area); }
    private readonly Surface _surface;
    private readonly HashSet<string> _expanded = [];
    private readonly Dictionary<string, Layer> _starts = [];
    private string _drag = "", _filter = "";
    private Vec2 _down;
    private Keyframe? _key;
    private Channel? _keyChannel;
    private double _keyTime, _keyValue, _startWork, _endWork;
    private bool _fit = true;
    private double _graphMin, _graphMax;
    private List<TimelineRow> _rows = [];
    public EditorSession Session { get; }
    public double HeaderWidth { get; private set; } = 430;
    public double PixelsPerSecond { get; private set; } = 100;
    public double ScrollSeconds { get; private set; }
    public double ScrollY { get; private set; }
    public bool GraphMode { get; set; }
    public bool HideShy { get; set; }
    public const double RulerHeight = 58;
    public const double RowHeight = 23;
    public IReadOnlyList<TimelineRow> Rows => _rows;
    public TimelineAxis Axis => new(HeaderWidth, PixelsPerSecond, ScrollSeconds);
    public event Action<string>? Error;
    public event Action? ViewChanged;
    public TimelineView(EditorSession session)
    {
        Session = session; IsTabStop = true; Background = Studio.Brush(Studio.Panel); _surface = new(this); Content = _surface;
        AutomationProperties.SetName(this, "Composition timeline"); AutomationProperties.SetAutomationId(this, "CompositionTimeline");
        foreach (var layer in session.Composition.Layers.Where(l => l.Transform.Channels().Any(p => p.Channel.Keys.Count > 0))) _expanded.Add(layer.Id);
        _surface.PointerPressed += Pressed; _surface.PointerMoved += Moved; _surface.PointerReleased += Released; _surface.PointerWheelChanged += Wheel;
        _surface.PointerCaptureLost += (_, _) => { if (_drag.Length > 0) { _drag = ""; Session.CancelEdit(); } };
        _surface.DoubleTapped += (_, e) => { var p = e.GetPosition(_surface); var row = RowAt(p.Y); if (p.X < HeaderWidth && row?.Layer is { Kind: LayerKind.Composition, SourceId: { } id }) { Session.Activate(id); Fit(); e.Handled = true; } };
        SizeChanged += (_, _) => { Layout(); Invalidate(); ViewChanged?.Invoke(); };
        KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape && _drag.Length > 0) { _drag = ""; Session.CancelEdit(); e.Handled = true; } };
        Session.Changed += Changed;
    }
    private void Changed(ChangeKind kind) { Layout(); Invalidate(); }
    public void Invalidate() => _surface.Invalidate();
    public void Fit() { _fit = true; ScrollSeconds = 0; ScrollY = 0; Layout(); Invalidate(); ViewChanged?.Invoke(); }
    public void ToggleGraph() { GraphMode = !GraphMode; Invalidate(); ViewChanged?.Invoke(); }
    public void ShowProperties(string filter)
    { _filter = filter; foreach (var l in Session.Selection) _expanded.Add(l.Id); Layout(); Invalidate(); }
    public void ZoomBy(double factor, double? anchorX = null)
    {
        var x = anchorX ?? HeaderWidth + (ActualWidth - HeaderWidth) / 2; var time = Axis.ToTime(x);
        _fit = false; PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, 4, 6000);
        ScrollSeconds = Math.Max(0, time - (x - HeaderWidth) / PixelsPerSecond); Invalidate(); ViewChanged?.Invoke();
    }
    private void Layout()
    {
        HeaderWidth = Math.Clamp(ActualWidth * .36, 280, 450);
        if (_fit) PixelsPerSecond = Math.Max(4, (ActualWidth - HeaderWidth - 24) / Session.Composition.Duration);
        var rows = new List<TimelineRow>(); var y = RulerHeight;
        foreach (var l in Session.Composition.Layers.Where(l => !HideShy || !l.Shy))
        {
            rows.Add(new(l, null, y, RowHeight)); y += RowHeight;
            if (!_expanded.Contains(l.Id)) continue;
            var names = _filter switch
            {
                "P" => new[] { "X", "Y" }, "S" => ["ScaleX", "ScaleY"], "R" => ["Rotation"], "T" => ["Opacity"], "A" => ["AnchorX", "AnchorY"],
                "All" => l.Transform.Channels().Select(p => p.Name).ToArray(),
                _ => l.Transform.Channels().Where(p => p.Channel.Keys.Count > 0 || p.Channel.Expression.Length > 0).Select(p => p.Name).ToArray()
            };
            foreach (var name in names) { rows.Add(new(l, name, y, RowHeight)); y += RowHeight; }
        }
        _rows = rows; ScrollY = Math.Clamp(ScrollY, 0, Math.Max(0, y - ActualHeight + 16));
    }
    private TimelineRow? RowAt(double y) => _rows.FirstOrDefault(r => y + ScrollY >= r.Y && y + ScrollY < r.Y + r.Height);
    private void Draw(SKCanvas canvas, Size area)
    {
        Layout(); var width = (float)area.Width; var height = (float)area.Height; var axis = Axis; var comp = Session.Composition;
        Studio.Rect(canvas, 0, 0, width, height, "#202020");
        var save = canvas.Save(); canvas.ClipRect(new(0, (float)RulerHeight, width, height - 12));
        foreach (var row in _rows.Where(r => r.Y + r.Height >= ScrollY + RulerHeight && r.Y - ScrollY <= height))
        {
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
                        if (right - x > 100) { var clipSave = canvas.Save(); canvas.ClipRect(new(x + 4, y, right - 4, y + 23)); Studio.DrawText(canvas, row.Layer.Name, x + 6, y + 15, 9, "#222027"); canvas.RestoreToCount(clipSave); }
                    }
                }
            }
            else
            {
                var channel = row.Layer.Transform.Get(row.Property!); StudioIcon.Draw(canvas, IconKind.Stopwatch, 85, y + 4, 14, channel.Keys.Count > 0 ? Studio.Accent : Studio.Muted);
                var active = selected && Session.Property == row.Property;
                Studio.DrawText(canvas, PropertyName(row.Property!), 116, y + 16, 10, active ? "#FFFFFF" : Studio.TextColor);
                Studio.DrawText(canvas, CurveEvaluator.Evaluate(channel, Session.Time).ToString("0.##", CultureInfo.InvariantCulture), (float)HeaderWidth - 76, y + 16, 10, Studio.Accent);
                if (!GraphMode) foreach (var key in channel.Keys) DrawKey(canvas, key, (float)axis.ToX(key.Time), y + 11, Session.SelectedKeyId == key.Id);
            }
        }
        canvas.RestoreToCount(save);
        if (GraphMode) DrawGraph(canvas, width, height);
        DrawRuler(canvas, width, height);
        Studio.Rect(canvas, 0, height - 12, width, 12, "#1C1C1C");
        Studio.DrawText(canvas, $"{Session.Composition.Layers.Count} layers    {Session.SelectedIds.Count} selected", 8, height - 3, 9, Studio.Muted);
        var total = Math.Max(comp.Duration, (width - HeaderWidth) / PixelsPerSecond); var visible = (width - HeaderWidth) / PixelsPerSecond;
        var track = width - (float)HeaderWidth; var thumb = Math.Max(20, (float)(visible / total * track)); var left = (float)HeaderWidth + (float)(ScrollSeconds / total * track);
        Studio.Rect(canvas, left, height - 9, Math.Min(thumb, width - left), 5, "#5A5A5A");
    }
    private void DrawRuler(SKCanvas canvas, float width, float height)
    {
        var axis = Axis; var c = Session.Composition;
        Studio.Rect(canvas, 0, 0, width, (float)RulerHeight, "#282828");
        Studio.DrawText(canvas, c.FrameRate.Timecode(Session.Time), 11, 25, 21, Studio.Accent);
        Studio.DrawText(canvas, $"{c.FrameRate.Frame(Session.Time):00000}  ({c.FrameRate})", 13, 42, 10, Studio.Muted);
        Studio.DrawText(canvas, "Source Name", 139, 55, 9, Studio.Muted); Studio.DrawText(canvas, "Mode", (float)HeaderWidth - 76, 55, 9, Studio.Muted);
        var save = canvas.Save(); canvas.ClipRect(new((float)HeaderWidth, 0, width, height));
        var candidates = new[] { c.FrameRate.Seconds(1), .1, .25, .5, 1, 2, 5, 10, 30, 60, 120, 300, 600 };
        var step = candidates.FirstOrDefault(s => s * PixelsPerSecond >= 62); if (step == 0) step = 600;
        var first = Math.Floor(ScrollSeconds / step) * step;
        using var line = new SKPaint { Color = SKColor.Parse("#333333"), StrokeWidth = 1 };
        for (var time = first; time <= ScrollSeconds + (width - HeaderWidth) / PixelsPerSecond + step; time += step)
        {
            var x = (float)axis.ToX(time); canvas.DrawLine(x, 34, x, height - 12, line);
            if (time >= 0) Studio.DrawText(canvas, step < 1 ? $"{time:0.##}s" : $"{time:0}s", x + 4, 31, 10, Studio.Muted);
            for (var j = 1; j < 4; j++) { var tick = (float)axis.ToX(time + step * j / 4); canvas.DrawLine(tick, 39, tick, 45, line); }
        }
        var start = (float)axis.ToX(c.WorkStart); var end = (float)axis.ToX(c.WorkEnd);
        Studio.Rect(canvas, start, 5, end - start, 8, "#737E8C"); Studio.Rect(canvas, start, 4, 5, 11, "#CFD4DA"); Studio.Rect(canvas, end - 5, 4, 5, 11, "#CFD4DA");
        foreach (var marker in c.Markers)
        { var x = (float)axis.ToX(marker.Time); StudioIcon.Draw(canvas, IconKind.Keyframe, x - 4, 44, 8, marker.Color); Studio.DrawText(canvas, marker.Name, x + 7, 53, 8, marker.Color); }
        var play = (float)axis.ToX(Session.Time); line.Color = SKColor.Parse(Studio.Accent); canvas.DrawLine(play, 18, play, height - 12, line);
        using var head = new SKPath(); head.MoveTo(play - 5, 17); head.LineTo(play + 5, 17); head.LineTo(play + 5, 24); head.LineTo(play, 29); head.LineTo(play - 5, 24); head.Close();
        using var fill = new SKPaint { Color = SKColor.Parse(Studio.Accent), IsAntialias = true }; canvas.DrawPath(head, fill);
        canvas.RestoreToCount(save); Studio.Rect(canvas, (float)HeaderWidth - 1, 0, 1, height, "#101010");
    }
    private void DrawGraph(SKCanvas canvas, float width, float height)
    {
        var left = (float)HeaderWidth; var top = (float)RulerHeight; var bottom = height - 20;
        var save = canvas.Save(); canvas.ClipRect(new(left, top, width, bottom)); Studio.Rect(canvas, left, top, width - left, bottom - top, "#1D2024");
        var layer = Session.Primary;
        if (layer is null) { Studio.DrawText(canvas, "Select a layer and animated property to edit its value graph.", left + 22, top + 35, 11, Studio.Muted); canvas.RestoreToCount(save); return; }
        var channel = layer.Transform.Get(Session.Property);
        var values = channel.Keys.Select(k => k.Value).Append(channel.Value).ToArray(); _graphMin = values.Min(); _graphMax = values.Max();
        if (_graphMax - _graphMin < 1) { _graphMin -= 50; _graphMax += 50; }
        var padding = (_graphMax - _graphMin) * .12; _graphMin -= padding; _graphMax += padding;
        using var grid = new SKPaint { Color = SKColor.Parse("#32363C"), StrokeWidth = 1 };
        for (var i = 0; i < 5; i++) { var value = _graphMin + (_graphMax - _graphMin) * i / 4; var y = GraphY(value); canvas.DrawLine(left, (float)y, width, (float)y, grid); Studio.DrawText(canvas, value.ToString("0.#", CultureInfo.InvariantCulture), left + 5, (float)y - 3, 9, Studio.Muted); }
        using var path = new SKPath(); var first = true;
        for (var x = left; x <= width; x += 2)
        { var y = (float)GraphY(CurveEvaluator.Evaluate(channel, Axis.ToTime(x), Session.Composition.Layers.IndexOf(layer) + 1)); if (first) { path.MoveTo(x, y); first = false; } else path.LineTo(x, y); }
        using var curve = new SKPaint { Color = SKColor.Parse("#81D9AE"), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.8f }; canvas.DrawPath(path, curve);
        foreach (var key in channel.Keys) DrawKey(canvas, key, (float)Axis.ToX(key.Time), (float)GraphY(key.Value), Session.SelectedKeyId == key.Id);
        Studio.DrawText(canvas, PropertyName(Session.Property) + "  ·  Value graph", left + 80, top + 19, 10, "#81D9AE"); canvas.RestoreToCount(save);
    }
    private double GraphY(double value) => RulerHeight + 12 + (1 - (value - _graphMin) / (_graphMax - _graphMin)) * Math.Max(1, ActualHeight - RulerHeight - 44);
    private double GraphValue(double y) => _graphMin + (1 - (y - RulerHeight - 12) / Math.Max(1, ActualHeight - RulerHeight - 44)) * (_graphMax - _graphMin);
    private void DrawKey(SKCanvas canvas, Keyframe key, float x, float y, bool selected)
    {
        if (x < HeaderWidth + 2 || x > ActualWidth) return;
        var color = selected ? "#FFD374" : Studio.Accent;
        if (key.Interpolation == Interpolation.Hold) Studio.Rect(canvas, x - 4, y - 4, 8, 8, color);
        else StudioIcon.Draw(canvas, key.Interpolation == Interpolation.Bezier ? IconKind.Diamond : IconKind.Keyframe, x - 5, y - 5, 10, color);
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            Focus(FocusState.Pointer); var point = e.GetCurrentPoint(_surface); _down = new(point.Position.X, point.Position.Y); var axis = Axis;
            if (_down.X < HeaderWidth)
            {
                var row = RowAt(_down.Y); if (row is null || _down.Y < RulerHeight) return;
                if (!row.IsProperty && _down.X < 62)
                {
                    Session.Edit("Layer switch", () => { if (_down.X < 25) row.Layer.Enabled = !row.Layer.Enabled; else if (_down.X < 43) row.Layer.Solo = !row.Layer.Solo; else row.Layer.Locked = !row.Layer.Locked; });
                }
                else if (!row.IsProperty && _down.X < 82) { if (!_expanded.Add(row.Layer.Id)) _expanded.Remove(row.Layer.Id); Layout(); Invalidate(); }
                else
                {
                    Session.Select(row.Layer.Id, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift));
                    if (row.Property is { } property) { Session.Property = property; if (_down.X is >= 82 and <= 108) Session.ToggleAnimation(property); else Session.PreviewChanged(); }
                }
                e.Handled = true; return;
            }
            if (_down.Y < 17)
            {
                _startWork = Session.Composition.WorkStart; _endWork = Session.Composition.WorkEnd;
                _drag = Math.Abs(_down.X - axis.ToX(_startWork)) < 8 ? "workStart" : Math.Abs(_down.X - axis.ToX(_endWork)) < 8 ? "workEnd" : "workMove"; Session.BeginEdit("Adjust work area");
            }
            else if (_down.Y < RulerHeight) { _drag = "scrub"; Session.SetTime(axis.ToTime(_down.X)); }
            else if (_down.Y >= ActualHeight - 12) _drag = "scroll";
            else
            {
                var row = RowAt(_down.Y); Layer? layer = GraphMode ? Session.Primary : row?.Layer;
                var property = GraphMode ? Session.Property : row?.Property;
                if (layer is null) return;
                if (!Session.SelectedIds.Contains(layer.Id)) Session.Select(layer.Id);
                if (property is not null)
                {
                    Session.Property = property; var channel = layer.Transform.Get(property);
                    var key = channel.Keys.FirstOrDefault(k => Math.Abs(axis.ToX(k.Time) - _down.X) < 8 && (!GraphMode || Math.Abs(GraphY(k.Value) - _down.Y) < 9));
                    if (key is null)
                    {
                        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) && !layer.Locked)
                        { var t = Math.Clamp(Session.Composition.FrameRate.Snap(axis.ToTime(_down.X)), 0, Session.Composition.LastFrameTime); Session.Edit("Add graph keyframe", () => channel.SetKey(t, GraphMode ? GraphValue(_down.Y) : CurveEvaluator.Evaluate(channel, t))); }
                        else Session.SetTime(axis.ToTime(_down.X));
                        e.Handled = true; return;
                    }
                    Session.SelectedKeyId = key.Id; _key = key; _keyChannel = channel; _keyTime = key.Time; _keyValue = key.Value;
                    if (layer.Locked) return; Session.BeginEdit("Move keyframe"); _drag = "key";
                }
                else
                {
                    if (layer.Locked || axis.ToTime(_down.X) < layer.InPoint || axis.ToTime(_down.X) > layer.OutPoint) { Session.SetTime(axis.ToTime(_down.X)); return; }
                    _starts.Clear(); foreach (var l in Session.Selection.Where(l => !l.Locked)) _starts[l.Id] = ProjectJson.CloneLayer(l);
                    _drag = Math.Abs(_down.X - axis.ToX(layer.InPoint)) < 7 ? "trimStart" : Math.Abs(_down.X - axis.ToX(layer.OutPoint)) < 7 ? "trimEnd" : e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu) ? "slip" : "move";
                    Session.BeginEdit("Timeline " + _drag);
                }
            }
            _surface.CapturePointer(e.Pointer); Invalidate(); e.Handled = true;
        }
        catch (Exception ex) { _drag = ""; Session.CancelEdit(); Error?.Invoke(ex.Message); }
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length == 0) return;
        try
        {
            var p = e.GetCurrentPoint(_surface).Position; var c = Session.Composition; var rate = c.FrameRate; var frame = rate.Seconds(1); var delta = rate.Snap((p.X - _down.X) / PixelsPerSecond);
            if (_drag == "scrub") { Session.SetTime(Axis.ToTime(p.X)); e.Handled = true; return; }
            if (_drag == "scroll") { ScrollSeconds = Math.Clamp(ScrollSeconds + (_down.X - p.X) / PixelsPerSecond, 0, Math.Max(0, c.Duration - (ActualWidth - HeaderWidth) / PixelsPerSecond)); _down = new(p.X, p.Y); Invalidate(); e.Handled = true; return; }
            if (_drag == "workStart") c.WorkStart = Math.Clamp(_startWork + delta, 0, _endWork - frame);
            else if (_drag == "workEnd") c.WorkEnd = Math.Clamp(_endWork + delta, _startWork + frame, c.Duration);
            else if (_drag == "workMove") { delta = Math.Clamp(delta, -_startWork, c.Duration - _endWork); c.WorkStart = _startWork + delta; c.WorkEnd = _endWork + delta; }
            else if (_drag == "key" && _key is not null && _keyChannel is not null)
            {
                var t = Math.Clamp(_keyTime + delta, 0, c.LastFrameTime);
                if (_keyChannel.Keys.All(k => k.Id == _key.Id || Math.Abs(k.Time - t) > frame * .25)) { _key.Time = t; _keyChannel.Keys.Sort((a,b) => a.Time.CompareTo(b.Time)); }
                if (GraphMode) _key.Value = Math.Clamp(_keyValue + GraphValue(p.Y) - GraphValue(_down.Y), -100000, 100000);
            }
            else
            {
                if (_drag == "move" && _starts.Count > 0)
                {
                    var min = _starts.Values.Min(l => Math.Min(l.InPoint, l.Transform.Channels().SelectMany(p => p.Channel.Keys).Select(k => k.Time).DefaultIfEmpty(l.InPoint).Min()));
                    delta = Math.Clamp(delta, -min, c.Duration - _starts.Values.Max(l => l.OutPoint));
                }
                foreach (var l in Session.Selection.Where(l => _starts.ContainsKey(l.Id)))
                {
                    var start = _starts[l.Id];
                    if (_drag == "trimStart") l.InPoint = Math.Clamp(start.InPoint + delta, 0, start.OutPoint - frame);
                    else if (_drag == "trimEnd") l.OutPoint = Math.Clamp(start.OutPoint + delta, start.InPoint + frame, c.Duration);
                    else if (_drag == "slip") l.StartTime = start.StartTime + delta;
                    else if (_drag == "move")
                    {
                        l.InPoint = start.InPoint + delta; l.OutPoint = start.OutPoint + delta; l.StartTime = start.StartTime + delta;
                        foreach (var (name, channel) in l.Transform.Channels())
                        { var original = start.Transform.Get(name); for (var i = 0; i < channel.Keys.Count; i++) channel.Keys[i].Time = original.Keys[i].Time + delta; }
                        for (var i = 0; i < l.Effects.Count; i++) foreach (var entry in l.Effects[i].Parameters) for (var j = 0; j < entry.Value.Keys.Count; j++) entry.Value.Keys[j].Time = start.Effects[i].Parameters[entry.Key].Keys[j].Time + delta;
                    }
                }
            }
            Session.PreviewChanged(); e.Handled = true;
        }
        catch (Exception ex) { _drag = ""; Session.CancelEdit(); Error?.Invoke(ex.Message); }
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        var drag = _drag; _drag = ""; _surface.ReleasePointerCapture(e.Pointer); _starts.Clear(); _key = null; _keyChannel = null;
        if (drag.Length == 0 || drag is "scrub" or "scroll") return;
        try { Session.CommitEdit(); } catch (Exception ex) { Error?.Invoke(ex.Message); } e.Handled = true;
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(_surface); var delta = p.Properties.MouseWheelDelta;
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) ZoomBy(Math.Pow(1.2, delta / 120d), p.Position.X);
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) { ScrollSeconds = Math.Clamp(ScrollSeconds - delta / PixelsPerSecond, 0, Math.Max(0, Session.Composition.Duration - (ActualWidth - HeaderWidth) / PixelsPerSecond)); }
        else { ScrollY -= delta / 3d; Layout(); }
        Invalidate(); e.Handled = true;
    }
    public static string PropertyName(string property) => property switch { "X" => "Position X", "Y" => "Position Y", "ScaleX" => "Scale X", "ScaleY" => "Scale Y", "AnchorX" => "Anchor X", "AnchorY" => "Anchor Y", _ => property };
    public void Dispose() => Session.Changed -= Changed;
}
