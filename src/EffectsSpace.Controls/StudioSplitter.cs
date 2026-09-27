using EffectsSpace.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace EffectsSpace.Controls;

public sealed class StudioSplitter : Border
{
    private Windows.Foundation.Point? _start;
    public event Action<Vec2>? Dragged;
    public StudioSplitter(bool vertical)
    {
        Width = vertical ? 5 : double.NaN; Height = vertical ? double.NaN : 5;
        Background = Studio.Brush("#141414");
        PointerPressed += (_, e) => { _start = e.GetCurrentPoint(null).Position; CapturePointer(e.Pointer); e.Handled = true; };
        PointerMoved += (_, e) => { if (_start is not { } p) return; var q = e.GetCurrentPoint(null).Position; Dragged?.Invoke(new(q.X - p.X, q.Y - p.Y)); _start = q; e.Handled = true; };
        PointerReleased += (_, e) => { _start = null; ReleasePointerCapture(e.Pointer); };
        PointerCaptureLost += (_, _) => _start = null;
        PointerEntered += (_, _) => Background = Studio.Brush("#3B536D");
        PointerExited += (_, _) => Background = Studio.Brush("#141414");
    }
}
