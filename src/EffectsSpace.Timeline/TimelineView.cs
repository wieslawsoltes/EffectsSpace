using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;
using Windows.System;

namespace EffectsSpace.Timeline;

public enum GraphKind { Value, Velocity }

/// <summary>A reusable timeline with model-only transactions, cached row layout and custom Skia interaction surfaces.</summary>
public sealed partial class TimelineView : UserControl, IDisposable
{
    private sealed class Surface(TimelineView owner) : SKCanvasElement
    { protected override void RenderOverride(SKCanvas canvas, Size area) => owner.Draw(canvas, area); }
    private readonly Surface _surface;
    private readonly HashSet<string> _expanded = [];
    private readonly Dictionary<string, Layer> _starts = [];
    private List<TimelineRow> _rows = [];
    private bool _fit = true, _layoutDirty = true, _graphDirty = true, _graphMode, _hideShy, _disposed;
    private string _filter = "", _drag = "";
    private Vec2 _down, _pointer;
    private double _startWork, _endWork, _scrollAtPress;
    private KeyframeMove? _keyMove;
    private CurveHandle? _handle;
    private double _graphMin, _graphMax = 1;
    private GraphKind _graphKind;
    private string[] _marqueeStart = [];
    private bool _marqueeAdditive;
    public EditorSession Session { get; }
    public double HeaderWidth { get; private set; } = 430;
    public double PixelsPerSecond { get; private set; } = 100;
    public double ScrollSeconds { get; private set; }
    public double ScrollY { get; private set; }
    public bool GraphMode { get => _graphMode; set { _graphMode = value; _graphDirty = true; Invalidate(); ViewChanged?.Invoke(); } }
    public GraphKind GraphKind { get => _graphKind; set { _graphKind = value; _graphDirty = true; Invalidate(); ViewChanged?.Invoke(); } }
    public bool HideShy { get => _hideShy; set { _hideShy = value; _layoutDirty = true; Invalidate(); } }
    public double GraphMinimum => _graphMin;
    public double GraphMaximum => _graphMax;
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
        foreach (var layer in session.Composition.Layers.Where(l => LayerChannels.Enumerate(l).Any(p => p.Channel.Keys.Count > 0))) _expanded.Add(layer.Id);
        _surface.PointerPressed += Pressed; _surface.PointerMoved += Moved; _surface.PointerReleased += Released; _surface.PointerWheelChanged += Wheel;
        _surface.PointerCaptureLost += (_, _) => { if (_drag.Length > 0) CancelInteraction(); };
        _surface.DoubleTapped += (_, e) =>
        {
            var p = e.GetPosition(_surface); var row = RowAt(p.Y);
            if (p.X < HeaderWidth && row is { IsProperty: false, Layer.Kind: LayerKind.Composition } && row.Layer.SourceId is { } id)
            { Session.Activate(id); Fit(); e.Handled = true; }
        };
        SizeChanged += (_, _) => { _layoutDirty = true; _graphDirty = true; Layout(); Invalidate(); ViewChanged?.Invoke(); };
        KeyDown += (_, e) => { if (e.Key == VirtualKey.Escape && _drag.Length > 0) { CancelInteraction(); e.Handled = true; } };
        Session.Changed += Changed;
    }
    private void Changed(ChangeKind kind)
    {
        if (kind is ChangeKind.Document or ChangeKind.Selection) _layoutDirty = true;
        if (kind != ChangeKind.Time && _drag.Length == 0) _graphDirty = true;
        Layout(); Invalidate();
    }
    public void Invalidate() => _surface.Invalidate();
    public void Fit() { _fit = true; ScrollSeconds = 0; ScrollY = 0; _layoutDirty = true; _graphDirty = true; Layout(); Invalidate(); ViewChanged?.Invoke(); }
    public void ToggleGraph() => GraphMode = !GraphMode;
    public void ToggleGraphKind() { GraphMode = true; GraphKind = GraphKind == GraphKind.Value ? GraphKind.Velocity : GraphKind.Value; }
    public void ShowProperties(string filter)
    {
        _filter = filter; foreach (var layer in Session.Selection) _expanded.Add(layer.Id);
        _layoutDirty = true; _graphDirty = true; Layout();
        var row = _rows.FirstOrDefault(r => r.Layer == Session.Primary && r.Property == Session.Property);
        if (row is not null)
        {
            var available = Math.Max(RowHeight * 2, ActualHeight - RulerHeight - 12);
            if (row.Y - ScrollY < RulerHeight) ScrollY = row.Y - RulerHeight;
            else if (row.Y + row.Height - ScrollY > ActualHeight - 12) ScrollY = row.Y + row.Height - RulerHeight - available;
            ClampScroll();
        }
        Invalidate(); ViewChanged?.Invoke();
    }
    public void RevealProperty(string path)
    { Session.Property = path; ShowProperties(path.StartsWith("fx/", StringComparison.Ordinal) ? "E" : "All"); }
    public void ZoomBy(double factor, double? anchorX = null)
    {
        var x = anchorX ?? HeaderWidth + (ActualWidth - HeaderWidth) / 2; var time = Axis.ToTime(x);
        _fit = false; PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, 4, 6000);
        ScrollSeconds = Math.Max(0, time - (x - HeaderWidth) / PixelsPerSecond); _graphDirty = true; Invalidate(); ViewChanged?.Invoke();
    }
    private void Layout()
    {
        if (!_layoutDirty) return;
        HeaderWidth = Math.Clamp(ActualWidth * .36, 280, 450);
        if (_fit) PixelsPerSecond = Math.Max(4, (ActualWidth - HeaderWidth - 24) / Session.Composition.Duration);
        var rows = new List<TimelineRow>(); var y = RulerHeight;
        foreach (var layer in Session.Composition.Layers.Where(l => !HideShy || !l.Shy))
        {
            rows.Add(new(layer, null, y, RowHeight)); y += RowHeight;
            if (!_expanded.Contains(layer.Id)) continue;
            foreach (var property in LayerChannels.Enumerate(layer))
            {
                var include = _filter switch
                {
                    "P" => property.Path is "X" or "Y", "S" => property.Path is "ScaleX" or "ScaleY",
                    "R" => property.Path == "Rotation", "T" => property.Path == "Opacity", "A" => property.Path is "AnchorX" or "AnchorY",
                    "E" => property.EffectKind is not null, "All" => true,
                    _ => property.Channel.Keys.Count > 0 || property.Channel.Expression.Length > 0
                };
                if (!include) continue;
                rows.Add(new(layer, property.Path, y, RowHeight, property)); y += RowHeight;
            }
        }
        _rows = rows; _layoutDirty = false; ClampScroll();
    }
    private void ClampScroll() => ScrollY = Math.Clamp(ScrollY, 0, Math.Max(0, (_rows.LastOrDefault()?.Y ?? RulerHeight) + RowHeight - ActualHeight + 12));
    private TimelineRow? RowAt(double y) => y < RulerHeight ? null : _rows.FirstOrDefault(r => y + ScrollY >= r.Y && y + ScrollY < r.Y + r.Height);
    private Layer? GraphLayer => Session.Selection.FirstOrDefault(l => LayerChannels.Find(l, Session.Property) is not null);
    private AnimatedProperty? GraphProperty => GraphLayer is { } layer ? LayerChannels.Find(layer, Session.Property) : null;
    public static string PropertyName(string property) => LayerChannels.DisplayName(property);
    public void CancelInteraction()
    {
        var edited = _drag is not ("" or "scrub" or "scroll" or "marquee");
        _drag = ""; _handle = null; _starts.Clear();
        if (_keyMove is { } move) { _keyMove = null; move.Dispose(); }
        else if (edited) Session.CancelEdit();
        _graphDirty = true; Invalidate();
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; CancelInteraction(); Session.Changed -= Changed; base.Dispose();
    }
}
