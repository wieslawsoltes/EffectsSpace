using System.Diagnostics;
using System.Numerics;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Rendering;
using EffectsSpace.Skia;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace EffectsSpace.Viewer;

public sealed class CompositionView : UserControl, IDisposable
{
    private sealed class Surface(CompositionView owner) : SKCanvasElement
    { protected override void RenderOverride(SKCanvas canvas, Size area) => owner.Draw(canvas, area); }
    private readonly Surface _surface;
    private readonly Grid _root = new();
    private readonly Canvas _overlay = new();
    private readonly Dictionary<string, Layer> _starts = [];
    private Vec2 _down, _worldDown, _panStart;
    private string _drag = "";
    private int _handle = -1, _node = -1;
    private Layer? _shape, _pen;
    private TextBox? _text;
    private bool _fit = true, _disposed;
    private ViewerTool _tool;
    public EditorSession Session { get; }
    public SkiaCompositor Renderer { get; } = new();
    public ViewerTool Tool { get => _tool; set { if (_tool == value) return; FinishPen(); EndText(true); _tool = value; Invalidate(); ViewChanged?.Invoke(); } }
    public bool SpaceDown { get; set; }
    public bool ShowGrid { get; set; }
    public bool ShowGuides { get; set; }
    public bool ShowTransparency { get; set; }
    public bool ShowMotionPaths { get; set; } = true;
    public bool Snapping { get; set; } = true;
    public double Zoom { get; private set; } = 1;
    public Vec2 Pan { get; private set; }
    public double LastRenderMilliseconds { get; private set; }
    public string? LastRenderError { get; private set; }
    public event Action? ViewChanged;
    public event Action<string>? Error;

    public CompositionView(EditorSession session)
    {
        Session = session; IsTabStop = true; Background = Studio.Brush("#171717");
        _surface = new Surface(this); _overlay.IsHitTestVisible = false;
        _root.Children.Add(_surface); _root.Children.Add(_overlay); Content = _root;
        AutomationProperties.SetName(this, "Composition viewer"); AutomationProperties.SetAutomationId(this, "CompositionViewer");
        _surface.PointerPressed += Pressed; _surface.PointerMoved += Moved; _surface.PointerReleased += Released;
        _surface.PointerCaptureLost += (_, _) => { if (_drag.Length > 0) CancelDrag(); };
        _surface.PointerWheelChanged += Wheel;
        _surface.DoubleTapped += (_, e) =>
        {
            var point = e.GetPosition(_surface); var layer = Hit(new(point.X, point.Y));
            if (layer?.Kind == LayerKind.Text && !layer.Locked) { Session.Select(layer.Id); BeginText(layer); e.Handled = true; }
            else if (layer?.Kind == LayerKind.Composition && layer.SourceId is { } source) { Session.Activate(source); Fit(); e.Handled = true; }
            else if (_pen is not null) { FinishPen(); e.Handled = true; }
        };
        SizeChanged += (_, _) => { UpdateFit(); Invalidate(); ViewChanged?.Invoke(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape) { CancelDrag(); if (_pen is not null) { Session.CancelEdit(); _pen = null; } EndText(false); e.Handled = true; }
            if (e.Key == VirtualKey.Enter && _pen is not null) { FinishPen(); e.Handled = true; }
        };
        Session.Changed += OnChanged;
    }
    private void OnChanged(ChangeKind kind) { if (_disposed) return; Invalidate(); }
    public void Invalidate() => _surface.Invalidate();
    public void Fit() { _fit = true; UpdateFit(); Invalidate(); ViewChanged?.Invoke(); }
    private void UpdateFit()
    {
        if (!_fit || ActualWidth <= 0 || ActualHeight <= 0) return;
        var c = Session.Composition;
        Zoom = Math.Clamp(Math.Min(Math.Max(1, ActualWidth - 64) / c.Width, Math.Max(1, ActualHeight - 52) / c.Height), .01, 32);
        Pan = new((ActualWidth - c.Width * Zoom) / 2, (ActualHeight - c.Height * Zoom) / 2);
    }
    public Vec2 ToComposition(Vec2 point) => (point - Pan) * (1 / Zoom);
    public Vec2 ToScreen(Vec2 point) => point * Zoom + Pan;
    public void SetZoom(double zoom) => ZoomAt(new(ActualWidth / 2, ActualHeight / 2), zoom);
    public void ZoomAt(Vec2 point, double zoom)
    {
        var world = ToComposition(point); _fit = false; Zoom = Math.Clamp(zoom, .01, 32); Pan = point - world * Zoom;
        EndText(true); Invalidate(); ViewChanged?.Invoke();
    }
    private void Draw(SKCanvas canvas, Size area)
    {
        var watch = Stopwatch.StartNew(); var saved = canvas.Save();
        try
        {
            UpdateFit(); LastRenderError = null;
            Studio.Rect(canvas, 0, 0, (float)area.Width, (float)area.Height, "#171717");
            canvas.Translate((float)Pan.X, (float)Pan.Y); canvas.Scale((float)Zoom);
            var c = Session.Composition;
            if (ShowTransparency)
            {
                var cell = (float)(12 / Zoom); using var p = new SKPaint();
                for (var y = 0f; y < c.Height; y += cell)
                for (var x = 0f; x < c.Width; x += cell)
                { p.Color = ((int)(x / cell) + (int)(y / cell)) % 2 == 0 ? SKColor.Parse("#353535") : SKColor.Parse("#292929"); canvas.DrawRect(x, y, Math.Min(cell, c.Width - x), Math.Min(cell, c.Height - y), p); }
            }
            else Studio.Rect(canvas, 0, 0, c.Width, c.Height, c.Background);
            Renderer.Render(canvas, Session.Project, c, Session.Time);
            using var guide = new SKPaint { Color = SKColor.Parse("#5D8294"), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)(1 / Zoom) };
            if (ShowGrid)
            {
                guide.Color = new SKColor(100, 150, 190, 65);
                for (var x = 0; x <= c.Width; x += 100) canvas.DrawLine(x, 0, x, c.Height, guide);
                for (var y = 0; y <= c.Height; y += 100) canvas.DrawLine(0, y, c.Width, y, guide);
            }
            if (ShowGuides)
            {
                guide.Color = new SKColor(170, 205, 210, 170);
                canvas.DrawRect(c.Width * .05f, c.Height * .05f, c.Width * .9f, c.Height * .9f, guide);
                canvas.DrawRect(c.Width * .1f, c.Height * .1f, c.Width * .8f, c.Height * .8f, guide);
                canvas.DrawLine(c.Width / 2f - 16, c.Height / 2f, c.Width / 2f + 16, c.Height / 2f, guide);
                canvas.DrawLine(c.Width / 2f, c.Height / 2f - 16, c.Width / 2f, c.Height / 2f + 16, guide);
            }
            DrawSelection(canvas);
            canvas.RestoreToCount(saved);
            Studio.DrawText(canvas, $"{c.Width} × {c.Height}   ·   {c.FrameRate}   ·   {Session.Composition.FrameRate.Timecode(Session.Time)}", 12, (float)area.Height - 10, 10, Studio.Muted);
        }
        catch (Exception ex)
        {
            canvas.RestoreToCount(saved); LastRenderError = ex.Message;
            Studio.DrawText(canvas, "Preview could not render: " + ex.Message, 20, 40, 12, "#E49A90");
        }
        finally { canvas.RestoreToCount(saved); LastRenderMilliseconds = watch.Elapsed.TotalMilliseconds; }
    }
    private void DrawSelection(SKCanvas canvas)
    {
        var c = Session.Composition; var radius = (float)(3.4 / Zoom);
        using var line = new SKPaint { Color = SKColor.Parse(Studio.Accent), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)(1 / Zoom) };
        using var handle = new SKPaint { Color = SKColor.Parse("#CADDF6"), IsAntialias = true };
        foreach (var l in Session.Selection)
        {
            var m = SkiaCompositor.Matrix(TransformEvaluator.World(c, l, Session.Time)); var save = canvas.Save(); canvas.Concat(ref m);
            canvas.DrawRect(0, 0, (float)l.Width, (float)l.Height, line);
            foreach (var p in Handles(l)) canvas.DrawRect((float)p.X - radius, (float)p.Y - radius, radius * 2, radius * 2, handle);
            var ax = (float)CurveEvaluator.Evaluate(l.Transform.AnchorX, Session.Time); var ay = (float)CurveEvaluator.Evaluate(l.Transform.AnchorY, Session.Time);
            canvas.DrawCircle(ax, ay, radius * 1.4f, line);
            if (l.Kind == LayerKind.Path)
            {
                using var path = PathGeometry.Build(l.Path); canvas.DrawPath(path, line);
                foreach (var n in l.Path.Nodes)
                {
                    canvas.DrawCircle((float)n.Point.X, (float)n.Point.Y, radius, handle);
                    foreach (var v in new[] { n.InHandle, n.OutHandle }.Where(v => v.Length > .01))
                    { canvas.DrawLine((float)n.Point.X, (float)n.Point.Y, (float)(n.Point.X + v.X), (float)(n.Point.Y + v.Y), line); canvas.DrawCircle((float)(n.Point.X + v.X), (float)(n.Point.Y + v.Y), radius * .7f, handle); }
                }
            }
            foreach (var mask in l.Masks.Where(m => m.Enabled)) { using var path = PathGeometry.Build(mask.Path); line.Color = SKColor.Parse("#E8C467"); canvas.DrawPath(path, line); line.Color = SKColor.Parse(Studio.Accent); }
            canvas.RestoreToCount(save);
            if (ShowMotionPaths && (l.Transform.X.Keys.Count > 0 || l.Transform.Y.Keys.Count > 0))
            {
                using var motion = new SKPath(); var first = true;
                for (var i = 0; i <= 80; i++)
                {
                    var t = c.Duration * i / 80; var p = TransformEvaluator.ToWorld(c, l, t, new(CurveEvaluator.Evaluate(l.Transform.AnchorX, t), CurveEvaluator.Evaluate(l.Transform.AnchorY, t)));
                    if (first) { motion.MoveTo((float)p.X, (float)p.Y); first = false; } else motion.LineTo((float)p.X, (float)p.Y);
                }
                line.Color = new SKColor(100, 170, 242, 120); canvas.DrawPath(motion, line); line.Color = SKColor.Parse(Studio.Accent);
            }
        }
    }
    private static Vec2[] Handles(Layer l) => [new(0,0), new(l.Width/2,0), new(l.Width,0), new(l.Width,l.Height/2), new(l.Width,l.Height), new(l.Width/2,l.Height), new(0,l.Height), new(0,l.Height/2)];
    private Layer? Hit(Vec2 point)
    {
        var world = ToComposition(point);
        foreach (var l in Session.Composition.Layers.Where(l => l.ActiveAt(Session.Time) && !l.Locked))
        {
            if (l.Kind != LayerKind.Path) continue;
            var p = TransformEvaluator.ToLocal(Session.Composition, l, Session.Time, world); if (p is null) continue;
            using var path = PathGeometry.Build(l.Path);
            if (path.Contains((float)p.Value.X, (float)p.Value.Y)) return l;
        }
        return RenderPlanner.HitTest(Session.Composition, Session.Time, world);
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            EndText(true); Focus(FocusState.Pointer);
            var p = e.GetCurrentPoint(_surface); _down = new(p.Position.X, p.Position.Y); _worldDown = ToComposition(_down); _panStart = Pan; _starts.Clear();
            if (p.Properties.IsMiddleButtonPressed || Tool == ViewerTool.Hand || SpaceDown) _drag = "pan";
            else if (Tool == ViewerTool.Zoom) { ZoomAt(_down, Zoom * (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu) ? .8 : 1.25)); e.Handled = true; return; }
            else if (Tool == ViewerTool.Pen)
            {
                if (_pen is not null && _pen.Path.Nodes.Count > 2 && (_worldDown - _pen.Path.Nodes[0].Point).Length * Zoom < 8) { _pen.Path.Closed = true; FinishPen(); e.Handled = true; return; }
                if (_pen is null)
                {
                    Session.BeginEdit("Draw Bezier path"); _pen = new Layer { Name = "Shape path", Kind = LayerKind.Path, OutPoint = Session.Composition.Duration, Width = Session.Composition.Width, Height = Session.Composition.Height, Path = new ShapePath { Closed = false }, StrokeWidth = 4, FillEnabled = false, Stroke = "#A7F2D0" };
                    Session.Composition.Layers.Insert(0, _pen); Session.Select(_pen.Id);
                }
                _pen.Path.Nodes.Add(new PathNode { Point = _worldDown }); _drag = "pen";
            }
            else if (Tool is ViewerTool.Rectangle or ViewerTool.Ellipse or ViewerTool.Star)
            {
                Session.BeginEdit("Draw " + Tool); var kind = Enum.Parse<LayerKind>(Tool.ToString());
                _shape = EditorCommands.NewLayer(Session.Composition, kind); _shape.Width = _shape.Height = 1; _shape.Transform.AnchorX.Value = _shape.Transform.AnchorY.Value = 0;
                _shape.Transform.X.Value = _worldDown.X; _shape.Transform.Y.Value = _worldDown.Y;
                Session.Composition.Layers.Insert(0, _shape); Session.Select(_shape.Id); _drag = "shape";
            }
            else if (Tool == ViewerTool.Text)
            {
                var hit = Hit(_down);
                if (hit?.Kind != LayerKind.Text) { hit = Session.AddLayer(LayerKind.Text); Session.Edit("Position text", () => { hit.Transform.X.Value = _worldDown.X; hit.Transform.Y.Value = _worldDown.Y; hit.Transform.AnchorX.Value = hit.Transform.AnchorY.Value = 0; }); }
                Session.Select(hit.Id); BeginText(hit); e.Handled = true; return;
            }
            else
            {
                var primary = Session.Primary; _handle = -1; _node = -1;
                if (primary is not null && !primary.Locked)
                {
                    var handles = Handles(primary);
                    for (var i = 0; i < handles.Length; i++) if ((ToScreen(TransformEvaluator.ToWorld(Session.Composition, primary, Session.Time, handles[i])) - _down).Length < 7) _handle = i;
                    if (primary.Kind == LayerKind.Path) for (var i = 0; i < primary.Path.Nodes.Count; i++) if ((ToScreen(TransformEvaluator.ToWorld(Session.Composition, primary, Session.Time, primary.Path.Nodes[i].Point)) - _down).Length < 8) _node = i;
                }
                var hit = _handle >= 0 || _node >= 0 ? primary : Hit(_down);
                if (hit is null) { Session.Select(null); e.Handled = true; return; }
                if (!Session.SelectedIds.Contains(hit.Id) || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) Session.Select(hit.Id, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift));
                if (Session.Primary is null || Session.Primary.Locked) return;
                foreach (var l in Session.Selection.Where(l => !l.Locked)) _starts[l.Id] = ProjectJson.CloneLayer(l);
                _drag = _node >= 0 ? "node" : Tool == ViewerTool.Rotate ? "rotate" : Tool == ViewerTool.Anchor ? "anchor" : _handle >= 0 ? "scale" : "move";
                Session.BeginEdit(_drag switch { "scale" => "Scale layers", "rotate" => "Rotate layers", "anchor" => "Move anchor", "node" => "Edit path", _ => "Move layers" });
            }
            _surface.CapturePointer(e.Pointer); Session.PreviewChanged(); e.Handled = true;
        }
        catch (Exception ex) { CancelDrag(); Error?.Invoke(ex.Message); }
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length == 0) return;
        try
        {
            var p = e.GetCurrentPoint(_surface).Position; var screen = new Vec2(p.X, p.Y); var world = ToComposition(screen);
            if (_drag == "pan") { _fit = false; Pan = _panStart + (screen - _down); Invalidate(); ViewChanged?.Invoke(); e.Handled = true; return; }
            if (_drag == "pen" && _pen is not null)
            { var node = _pen.Path.Nodes[^1]; node.OutHandle = world - node.Point; node.InHandle = node.OutHandle * -1; }
            else if (_drag == "shape" && _shape is not null)
            {
                var delta = world - _worldDown;
                if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) { var size = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)); delta = new(Math.CopySign(size, delta.X), Math.CopySign(size, delta.Y)); }
                _shape.Width = Math.Clamp(Math.Abs(delta.X), 1, 32768); _shape.Height = Math.Clamp(Math.Abs(delta.Y), 1, 32768);
                _shape.Transform.X.Value = Math.Min(_worldDown.X, _worldDown.X + delta.X); _shape.Transform.Y.Value = Math.Min(_worldDown.Y, _worldDown.Y + delta.Y);
            }
            else foreach (var layer in Session.Selection.Where(l => _starts.ContainsKey(l.Id)))
            {
                var start = _starts[layer.Id]; var t = Session.Time;
                double E(Channel c) => CurveEvaluator.Evaluate(c, t);
                void Set(Channel c, double value) => EditorCommands.SetChannel(c, t, value, Session.AutoKey);
                var delta = ParentDelta(layer, world - _worldDown);
                if (_drag == "move")
                {
                    var x = E(start.Transform.X) + delta.X; var y = E(start.Transform.Y) + delta.Y;
                    if (Snapping && layer.ParentId is null)
                    {
                        var threshold = 6 / Zoom; if (Math.Abs(x - Session.Composition.Width / 2d) < threshold) x = Session.Composition.Width / 2d;
                        if (Math.Abs(y - Session.Composition.Height / 2d) < threshold) y = Session.Composition.Height / 2d;
                        if (ShowGrid) { x = Math.Round(x / 10) * 10; y = Math.Round(y / 10) * 10; }
                    }
                    Set(layer.Transform.X, x); Set(layer.Transform.Y, y);
                }
                else if (_drag == "rotate")
                {
                    var origin = TransformEvaluator.ToWorld(Session.Composition, start, t, new(E(start.Transform.AnchorX), E(start.Transform.AnchorY)));
                    var a = Math.Atan2(world.Y - origin.Y, world.X - origin.X) - Math.Atan2(_worldDown.Y - origin.Y, _worldDown.X - origin.X);
                    var angle = E(start.Transform.Rotation) + a * 180 / Math.PI; if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) angle = Math.Round(angle / 15) * 15;
                    Set(layer.Transform.Rotation, angle);
                }
                else if (_drag == "anchor")
                {
                    var matrix = TransformEvaluator.Local(start, t); matrix.M31 = matrix.M32 = 0;
                    if (!Matrix3x2.Invert(matrix, out var inverse)) continue;
                    var local = Vector2.TransformNormal(new((float)delta.X, (float)delta.Y), inverse);
                    Set(layer.Transform.AnchorX, E(start.Transform.AnchorX) + local.X); Set(layer.Transform.AnchorY, E(start.Transform.AnchorY) + local.Y);
                    Set(layer.Transform.X, E(start.Transform.X) + delta.X); Set(layer.Transform.Y, E(start.Transform.Y) + delta.Y);
                }
                else if (_drag == "node")
                {
                    var local = TransformEvaluator.ToLocal(Session.Composition, layer, t, world);
                    if (local is { } q && _node < layer.Path.Nodes.Count) layer.Path.Nodes[_node].Point = q;
                }
                else if (_drag == "scale")
                {
                    // Drag in the original local frame and keep the opposite handle fixed in parent coordinates.
                    var local = TransformEvaluator.ToLocal(Session.Composition, start, t, world); if (local is not { } q) continue;
                    var handles = Handles(start); var opposite = handles[(_handle + 4) % 8]; var active = handles[_handle];
                    var fx = Math.Abs(active.X - opposite.X) > 1e-9 ? (q.X - opposite.X) / (active.X - opposite.X) : 1;
                    var fy = Math.Abs(active.Y - opposite.Y) > 1e-9 ? (q.Y - opposite.Y) / (active.Y - opposite.Y) : 1;
                    if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) fx = fy = Math.Abs(fx - 1) > Math.Abs(fy - 1) ? fx : fy;
                    fx = Math.Clamp(fx, .01, 100); fy = Math.Clamp(fy, .01, 100);
                    var sx = E(start.Transform.ScaleX) * fx; var sy = E(start.Transform.ScaleY) * fy;
                    var anchor = new Vec2(E(start.Transform.AnchorX), E(start.Transform.AnchorY)); var relative = opposite - anchor;
                    var rotation = E(start.Transform.Rotation) * Math.PI / 180;
                    var dx = relative.X * (E(start.Transform.ScaleX) - sx) / 100; var dy = relative.Y * (E(start.Transform.ScaleY) - sy) / 100;
                    Set(layer.Transform.ScaleX, sx); Set(layer.Transform.ScaleY, sy);
                    Set(layer.Transform.X, E(start.Transform.X) + dx * Math.Cos(rotation) - dy * Math.Sin(rotation));
                    Set(layer.Transform.Y, E(start.Transform.Y) + dx * Math.Sin(rotation) + dy * Math.Cos(rotation));
                }
            }
            Session.PreviewChanged(); e.Handled = true;
        }
        catch (Exception ex) { CancelDrag(); Error?.Invoke(ex.Message); }
    }
    private Vec2 ParentDelta(Layer layer, Vec2 delta)
    {
        if (layer.ParentId is null) return delta;
        var parent = Session.Composition.Layers.First(l => l.Id == layer.ParentId);
        if (!Matrix3x2.Invert(TransformEvaluator.World(Session.Composition, parent, Session.Time), out var inverse)) return new(0, 0);
        var v = Vector2.TransformNormal(new((float)delta.X, (float)delta.Y), inverse); return new(v.X, v.Y);
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length == 0) return;
        var drag = _drag; _drag = ""; _surface.ReleasePointerCapture(e.Pointer);
        try
        {
            if (drag == "shape" && _shape is { Width: < 3, Height: < 3 }) { _shape.Width = 320; _shape.Height = Tool == ViewerTool.Ellipse ? 320 : 200; }
            if (drag != "pan" && drag != "pen") Session.CommitEdit();
        }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        _shape = null; _starts.Clear(); Invalidate(); e.Handled = true;
    }
    public void CancelDrag()
    {
        var wasPan = _drag == "pan"; _drag = ""; _starts.Clear(); _shape = null;
        if (!wasPan && _pen is null && _text is null) Session.CancelEdit(); Invalidate();
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    { var p = e.GetCurrentPoint(_surface); ZoomAt(new(p.Position.X, p.Position.Y), Zoom * Math.Pow(1.12, p.Properties.MouseWheelDelta / 120d)); e.Handled = true; }
    private void FinishPen()
    {
        if (_pen is null) return;
        try { if (_pen.Path.Nodes.Count < 2) Session.CancelEdit(); else Session.CommitEdit(); }
        catch (Exception ex) { Error?.Invoke(ex.Message); }
        _pen = null; _drag = ""; Invalidate();
    }
    private void BeginText(Layer layer)
    {
        EndText(true); Session.BeginEdit("Edit text");
        var point = ToScreen(TransformEvaluator.ToWorld(Session.Composition, layer, Session.Time, new(0, 0)));
        _text = new TextBox { Text = layer.Text, FontFamily = Studio.Font, FontSize = Math.Clamp(layer.FontSize * Zoom, 12, 240), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Width = Math.Max(220, layer.Width * Zoom + 24), MinHeight = Math.Max(48, layer.Height * Zoom + 12), Foreground = Studio.Brush(layer.Fill), Background = Studio.Brush("#242424"), BorderBrush = Studio.Brush(Studio.Accent), BorderThickness = new Thickness(1), Padding = new Thickness(4) };
        Canvas.SetLeft(_text, Math.Clamp(point.X, 0, Math.Max(0, ActualWidth - 220))); Canvas.SetTop(_text, Math.Clamp(point.Y, 0, Math.Max(0, ActualHeight - 60)));
        AutomationProperties.SetName(_text, "Inline composition text");
        _overlay.IsHitTestVisible = true; _overlay.Children.Add(_text);
        _text.TextChanged += (_, _) => { if (_text is null) return; layer.Text = _text.Text; Session.PreviewChanged(); };
        _text.KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape) { EndText(false); e.Handled = true; } };
        _text.LostFocus += (_, _) => { if (_text is not null) DispatcherQueue.TryEnqueue(() => EndText(true)); };
        _text.Focus(FocusState.Programmatic); _text.SelectAll();
    }
    public void EndText(bool commit)
    {
        if (_text is null) return;
        _text = null; _overlay.Children.Clear(); _overlay.IsHitTestVisible = false;
        try { if (commit) Session.CommitEdit(); else Session.CancelEdit(); } catch (Exception ex) { Error?.Invoke(ex.Message); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Session.Changed -= OnChanged; Renderer.Dispose(); }
}
