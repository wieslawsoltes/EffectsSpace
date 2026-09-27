using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace EffectsSpace.Viewer;

public readonly record struct MaskEditPoint(int Node, string Part, double X, double Y);

/// <summary>Reusable screen-space mask path editor. Each captured gesture owns one cancellable document transaction.</summary>
public sealed class MaskEditView : UserControl, IDisposable
{
    private sealed class Surface(MaskEditView owner) : SKCanvasElement
    { protected override void RenderOverride(SKCanvas canvas, Size size) => owner.Draw(canvas); }
    private readonly CompositionView _viewer;
    private readonly Surface _surface;
    private string? _layerId, _maskId;
    private int _node;
    private string _part = "";
    private Vec2 _startPoint, _startIn, _startOut, _down;
    private bool _dragging, _disposed;
    private EditorSession Session => _viewer.Session;
    public string? MaskId => _maskId;
    public event Action<string>? Error;

    public MaskEditView(CompositionView viewer)
    {
        _viewer = viewer; _surface = new(this); Content = _surface; IsTabStop = true;
        Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(this, "Mask path editor");
        _surface.PointerPressed += Pressed; _surface.PointerMoved += Moved; _surface.PointerReleased += Released;
        _surface.PointerCaptureLost += (_, _) => Cancel();
        _surface.PointerWheelChanged += (_, e) =>
        {
            if (_dragging) return;
            var p = e.GetCurrentPoint(_surface);
            _viewer.ZoomAt(new(p.Position.X, p.Position.Y), _viewer.Zoom * Math.Pow(1.12, p.Properties.MouseWheelDelta / 120d));
            e.Handled = true;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape) { if (_dragging) Cancel(); else Stop(); e.Handled = true; }
            if (e.Key == VirtualKey.Enter && !_dragging) { Stop(); e.Handled = true; }
        };
        Session.Changed += Changed; viewer.ViewChanged += Invalidate;
    }
    public void Start(string layerId, string maskId)
    {
        Stop(); _layerId = layerId; _maskId = maskId;
        if (Resolve() is not { } pair || pair.Layer.Locked) { Stop(); throw new InvalidOperationException("Select an unlocked layer and an existing mask."); }
        Visibility = Visibility.Visible; Invalidate(); Focus(FocusState.Programmatic);
    }
    public void Stop() { Cancel(); _layerId = _maskId = null; Visibility = Visibility.Collapsed; }
    public void Cancel()
    {
        if (!_dragging) return;
        _dragging = false; Session.CancelEdit(); Invalidate();
    }
    private (Layer Layer, LayerMask Mask)? Resolve()
    {
        var layer = Session.Composition.Layers.FirstOrDefault(l => l.Id == _layerId);
        var mask = layer?.Masks.FirstOrDefault(m => m.Id == _maskId);
        return layer is not null && mask is not null ? (layer, mask) : null;
    }
    private void Changed(ChangeKind kind)
    {
        if (_maskId is null) return;
        if (Resolve() is null || !Session.SelectedIds.Contains(_layerId!)) { Stop(); return; }
        Invalidate();
    }
    private void Invalidate() => _surface.Invalidate();
    private Vec2 Screen(Layer layer, Vec2 point) => _viewer.ToScreen(TransformEvaluator.ToWorld(Session.Composition, layer, Session.Time, point));
    public IReadOnlyList<MaskEditPoint> VisiblePoints()
    {
        if (Resolve() is not { } pair || Visibility != Visibility.Visible) return [];
        var result = new List<MaskEditPoint>();
        for (var i = 0; i < pair.Mask.Path.Nodes.Count; i++)
        {
            var node = pair.Mask.Path.Nodes[i]; var p = Screen(pair.Layer, node.Point);
            result.Add(new(i, "Point", p.X, p.Y));
            foreach (var (part, handle) in new[] { ("In", node.InHandle), ("Out", node.OutHandle) })
                if (handle.Length > .001) { p = Screen(pair.Layer, node.Point + handle); result.Add(new(i, part, p.X, p.Y)); }
        }
        return result;
    }
    private void Draw(SKCanvas canvas)
    {
        if (Resolve() is not { } pair) return;
        var saved = canvas.Save();
        try
        {
            canvas.Translate((float)_viewer.Pan.X, (float)_viewer.Pan.Y); canvas.Scale((float)_viewer.Zoom);
            var matrix = EffectsSpace.Skia.SkiaCompositor.Matrix(TransformEvaluator.World(Session.Composition, pair.Layer, Session.Time));
            canvas.Concat(in matrix);
            using var path = EffectsSpace.Skia.PathGeometry.Build(pair.Mask.Path);
            using var line = new SKPaint { Color = SKColor.Parse("#F4D27D"), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)(1.2 / _viewer.Zoom) };
            canvas.DrawPath(path, line);
            canvas.RestoreToCount(saved);
            using var stroke = new SKPaint { Color = SKColor.Parse("#F4D27D"), IsAntialias = true, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            using var fill = new SKPaint { Color = SKColor.Parse("#F4D27D"), IsAntialias = true };
            foreach (var point in VisiblePoints())
            {
                if (point.Part == "Point") canvas.DrawRect((float)point.X - 4, (float)point.Y - 4, 8, 8, fill);
                else
                {
                    var center = Screen(pair.Layer, pair.Mask.Path.Nodes[point.Node].Point);
                    canvas.DrawLine((float)center.X, (float)center.Y, (float)point.X, (float)point.Y, stroke);
                    canvas.DrawCircle((float)point.X, (float)point.Y, 3.5f, fill);
                }
            }
            Studio.DrawText(canvas, "MASK PATH  ·  Drag nodes / handles  ·  Alt-drag node: tangents  ·  Shift: mirror  ·  Enter: finish", 12, 20, 10, "#F4D27D");
        }
        finally { canvas.RestoreToCount(saved); }
    }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (Resolve() is not { } pair || pair.Layer.Locked || Session.IsEditing) return;
        var current = e.GetCurrentPoint(_surface); var screen = new Vec2(current.Position.X, current.Position.Y);
        var hit = VisiblePoints().Where(p => (new Vec2(p.X,p.Y) - screen).Length <= 8)
            .OrderBy(p => (new Vec2(p.X,p.Y) - screen).Length).ToArray();
        e.Handled = true; Focus(FocusState.Pointer);
        if (hit.Length == 0) return;
        var local = TransformEvaluator.ToLocal(Session.Composition, pair.Layer, Session.Time, _viewer.ToComposition(screen));
        if (local is null) return;
        _node = hit[0].Node; _part = hit[0].Part; _down = local.Value;
        var node = pair.Mask.Path.Nodes[_node]; _startPoint = node.Point; _startIn = node.InHandle; _startOut = node.OutHandle;
        Session.BeginEdit("Edit mask path"); _dragging = true; _surface.CapturePointer(e.Pointer);
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || Resolve() is not { } pair) return;
        try
        {
            var p = e.GetCurrentPoint(_surface).Position;
            var local = TransformEvaluator.ToLocal(Session.Composition, pair.Layer, Session.Time, _viewer.ToComposition(new(p.X,p.Y)));
            if (local is null) return;
            var delta = local.Value - _down; var node = pair.Mask.Path.Nodes[_node];
            if (_part == "Point" && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu)) node.Point = _startPoint + delta;
            else if (_part == "In") { node.InHandle = _startIn + delta; if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) node.OutHandle = node.InHandle * -1; }
            else { node.OutHandle = _part == "Point" ? delta : _startOut + delta; if (_part == "Point" || e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)) node.InHandle = node.OutHandle * -1; }
            Session.PreviewChanged(); e.Handled = true;
        }
        catch (Exception ex) { Cancel(); Error?.Invoke(ex.Message); }
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false; _surface.ReleasePointerCapture(e.Pointer);
        try { Session.CommitEdit(); } catch (Exception ex) { Error?.Invoke(ex.Message); }
        Invalidate(); e.Handled = true;
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; Stop();
        Session.Changed -= Changed; _viewer.ViewChanged -= Invalidate; base.Dispose();
    }
}
