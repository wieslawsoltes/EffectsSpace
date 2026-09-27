using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace EffectsSpace.Timeline;

public sealed partial class TimelineView
{
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (Session.IsEditing || !e.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed) return;
        try
        {
            Focus(FocusState.Pointer); Layout(); EnsureGraphRange();
            var point = e.GetCurrentPoint(_surface).Position; _down = _pointer = new(point.X, point.Y);
            var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
            var ctrl = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
            if (_down.X < HeaderWidth)
            {
                var row = RowAt(_down.Y); if (row is null) return;
                if (!row.IsProperty && _down.X < 62)
                    Session.Edit("Layer switch", () =>
                    {
                        if (_down.X < 25) row.Layer.Enabled = !row.Layer.Enabled;
                        else if (_down.X < 43) row.Layer.Solo = !row.Layer.Solo;
                        else row.Layer.Locked = !row.Layer.Locked;
                    });
                else if (!row.IsProperty && _down.X < 82)
                { if (!_expanded.Add(row.Layer.Id)) _expanded.Remove(row.Layer.Id); _layoutDirty = true; Layout(); Invalidate(); }
                else
                {
                    Session.Select(row.Layer.Id, shift && !row.IsProperty);
                    if (row.Property is { } property)
                    {
                        Session.Property = property;
                        if (_down.X is >= 82 and <= 108) Session.ToggleAnimation(property);
                        else if (ctrl) Session.SelectPropertyKeys();
                        else Session.PreviewChanged();
                        _graphDirty = true;
                    }
                }
                ViewChanged?.Invoke(); e.Handled = true; return;
            }
            if (_down.Y < 17)
            {
                _startWork = Session.Composition.WorkStart; _endWork = Session.Composition.WorkEnd;
                _drag = Math.Abs(_down.X - Axis.ToX(_startWork)) < 8 ? "workStart"
                    : Math.Abs(_down.X - Axis.ToX(_endWork)) < 8 ? "workEnd" : "workMove";
                Session.BeginEdit("Adjust work area");
            }
            else if (_down.Y < RulerHeight)
            { _drag = "scrub"; Session.SetTime(Axis.ToTime(_down.X)); }
            else if (_down.Y >= ActualHeight - 12)
            { _drag = "scroll"; _scrollAtPress = ScrollSeconds; }
            else
            {
                var row = RowAt(_down.Y);
                var layer = GraphMode ? GraphLayer : row?.Layer;
                var property = GraphMode ? GraphProperty : row?.Descriptor;
                if (property is not null && layer is not null)
                {
                    var key = property.Channel.Keys.FirstOrDefault(k =>
                    {
                        if (Math.Abs(Axis.ToX(k.Time) - _down.X) > 8) return false;
                        if (!GraphMode) return true;
                        if (!GraphValueAt(property.Channel, k.Time, out var value)) return false;
                        return Math.Abs(GraphY(GraphKind == GraphKind.Value ? k.Value : value) - _down.Y) < 9;
                    });
                    if (key is not null)
                    {
                        if (shift) { Session.ToggleKey(key.Id); e.Handled = true; return; }
                        if (!Session.SelectedKeyIds.Contains(key.Id)) Session.SelectKeys([key.Id]);
                        Session.Property = property.Path;
                        if (layer.Locked) { e.Handled = true; return; }
                        EnsureGraphRange(); _keyMove = new KeyframeMove(Session); _drag = "key";
                    }
                    else if (GraphMode && CurveHandles().FirstOrDefault(h =>
                        Math.Abs(Axis.ToX(h.Time) - _down.X) < 8 && Math.Abs(GraphY(h.Value) - _down.Y) < 8) is { } handle)
                    {
                        _handle = handle; Session.BeginEdit("Edit temporal Bezier handles"); _drag = "handle";
                    }
                    else if (ctrl && !layer.Locked && (!GraphMode || GraphKind == GraphKind.Value))
                    {
                        var time = Math.Clamp(Session.Composition.FrameRate.Snap(Axis.ToTime(_down.X)), 0, Session.Composition.LastFrameTime);
                        var value = GraphMode ? GraphValue(_down.Y) : CurveEvaluator.Evaluate(property.Channel, time, Session.Composition.Layers.IndexOf(layer) + 1);
                        Session.Edit("Add keyframe", () =>
                        {
                            property.Channel.SetKey(time, Math.Clamp(value, property.Minimum, property.Maximum));
                            Session.SelectKeys([property.Channel.Keys.First(k => Math.Abs(k.Time - time) < 1e-8).Id]);
                        });
                        _graphDirty = true; Invalidate(); e.Handled = true; return;
                    }
                    else BeginMarquee(shift);
                }
                else if (!GraphMode && layer is not null && !layer.Locked &&
                    Axis.ToTime(_down.X) >= layer.InPoint && Axis.ToTime(_down.X) <= layer.OutPoint && !shift)
                {
                    if (!Session.SelectedIds.Contains(layer.Id)) Session.Select(layer.Id);
                    else Session.ClearKeySelection();
                    _starts.Clear(); foreach (var selected in Session.Selection.Where(l => !l.Locked)) _starts[selected.Id] = ProjectJson.CloneLayer(selected);
                    _drag = Math.Abs(_down.X - Axis.ToX(layer.InPoint)) < 7 ? "trimStart"
                        : Math.Abs(_down.X - Axis.ToX(layer.OutPoint)) < 7 ? "trimEnd"
                        : e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu) ? "slip" : "move";
                    Session.BeginEdit("Timeline " + _drag);
                }
                else BeginMarquee(shift);
            }
            _surface.CapturePointer(e.Pointer); Invalidate(); e.Handled = true;
        }
        catch (Exception ex) { CancelInteraction(); Error?.Invoke(ex.Message); }
    }
    private void BeginMarquee(bool additive)
    {
        _drag = "marquee"; _marqueeAdditive = additive; _marqueeStart = Session.SelectedKeyIds.ToArray();
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length == 0) return;
        try
        {
            var point = e.GetCurrentPoint(_surface).Position; _pointer = new(point.X, point.Y);
            var comp = Session.Composition; var frame = comp.FrameRate.Seconds(1);
            var delta = comp.FrameRate.Snap((point.X - _down.X) / PixelsPerSecond);
            if (_drag == "marquee") { Invalidate(); e.Handled = true; return; }
            if (_drag == "scrub") { Session.SetTime(Axis.ToTime(point.X)); e.Handled = true; return; }
            if (_drag == "scroll")
            {
                ScrollSeconds = Math.Clamp(_scrollAtPress + (point.X - _down.X) / Math.Max(1, ActualWidth - HeaderWidth) * comp.Duration,
                    0, Math.Max(0, comp.Duration - (ActualWidth - HeaderWidth) / PixelsPerSecond));
                _graphDirty = true; Invalidate(); ViewChanged?.Invoke(); e.Handled = true; return;
            }
            if (_drag == "key" && _keyMove is not null)
            {
                var valueDelta = GraphMode && GraphKind == GraphKind.Value ? GraphValue(point.Y) - GraphValue(_down.Y) : 0;
                _keyMove.TryPreview(delta, valueDelta); e.Handled = true; return;
            }
            if (_drag == "handle" && _handle is { } handle)
            {
                var x = Math.Clamp((Axis.ToTime(point.X) - handle.Left.Time) / (handle.Right.Time - handle.Left.Time), 0, 1);
                var y = Math.Clamp((GraphValue(point.Y) - handle.Left.Value) / (handle.Right.Value - handle.Left.Value), -100, 100);
                if (handle.Outgoing) { handle.Left.X1 = x; handle.Left.Y1 = y; }
                else { handle.Left.X2 = x; handle.Left.Y2 = y; }
            }
            else if (_drag == "workStart") comp.WorkStart = Math.Clamp(_startWork + delta, 0, _endWork - frame);
            else if (_drag == "workEnd") comp.WorkEnd = Math.Clamp(_endWork + delta, _startWork + frame, comp.Duration);
            else if (_drag == "workMove")
            { delta = Math.Clamp(delta, -_startWork, comp.Duration - _endWork); comp.WorkStart = _startWork + delta; comp.WorkEnd = _endWork + delta; }
            else MoveLayers(delta, frame);
            Session.PreviewChanged(); e.Handled = true;
        }
        catch (Exception ex) { CancelInteraction(); Error?.Invoke(ex.Message); }
    }
    private void MoveLayers(double delta, double frame)
    {
        if (_drag == "move" && _starts.Count > 0)
        {
            var first = _starts.Values.Min(l => Math.Min(l.InPoint,
                LayerChannels.Enumerate(l).SelectMany(p => p.Channel.Keys).Select(k => k.Time).DefaultIfEmpty(l.InPoint).Min()));
            delta = Math.Clamp(delta, -first, Session.Composition.Duration - _starts.Values.Max(l => l.OutPoint));
        }
        foreach (var layer in Session.Selection.Where(l => _starts.ContainsKey(l.Id)))
        {
            var start = _starts[layer.Id];
            if (_drag == "trimStart") layer.InPoint = Math.Clamp(start.InPoint + delta, 0, start.OutPoint - frame);
            else if (_drag == "trimEnd") layer.OutPoint = Math.Clamp(start.OutPoint + delta, start.InPoint + frame, Session.Composition.Duration);
            else if (_drag == "slip") layer.StartTime = start.StartTime + delta;
            else if (_drag == "move")
            {
                layer.InPoint = start.InPoint + delta; layer.OutPoint = start.OutPoint + delta; layer.StartTime = start.StartTime + delta;
                foreach (var property in LayerChannels.Enumerate(layer))
                {
                    var original = LayerChannels.Get(start, property.Path);
                    for (var i = 0; i < property.Channel.Keys.Count; i++) property.Channel.Keys[i].Time = original.Keys[i].Time + delta;
                }
            }
        }
    }
    private void Released(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length == 0) return;
        var drag = _drag; _drag = "";
        _surface.ReleasePointerCapture(e.Pointer);
        try
        {
            if (drag == "key")
            {
                var move = _keyMove; _keyMove = null;
                using (move) move?.Commit();
            }
            else if (drag == "marquee")
            {
                if ((_pointer - _down).Length < 3)
                { if (!_marqueeAdditive) Session.ClearKeySelection(); Session.SetTime(Axis.ToTime(_down.X)); }
                else
                {
                    var rect = MarqueeRectangle(); var ids = VisibleKeyPositions().Where(k => rect.Contains((float)k.X, (float)k.Y)).Select(k => k.Id);
                    Session.SelectKeys(_marqueeAdditive ? _marqueeStart.Concat(ids) : ids);
                }
            }
            else if (drag is not ("scrub" or "scroll")) Session.CommitEdit();
        }
        catch (Exception ex) { Session.CancelEdit(); Error?.Invoke(ex.Message); }
        finally { _handle = null; _starts.Clear(); _graphDirty = true; Invalidate(); ViewChanged?.Invoke(); }
        e.Handled = true;
    }
    private void Wheel(object sender, PointerRoutedEventArgs e)
    {
        if (_drag.Length > 0) { e.Handled = true; return; }
        var point = e.GetCurrentPoint(_surface); var delta = point.Properties.MouseWheelDelta;
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) ZoomBy(Math.Pow(1.2, delta / 120d), point.Position.X);
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            ScrollSeconds = Math.Clamp(ScrollSeconds - delta / PixelsPerSecond, 0,
                Math.Max(0, Session.Composition.Duration - (ActualWidth - HeaderWidth) / PixelsPerSecond)); _graphDirty = true;
        }
        else { ScrollY -= delta / 3d; ClampScroll(); }
        Invalidate(); ViewChanged?.Invoke(); e.Handled = true;
    }
}
