using System.Diagnostics;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Timeline;
using EffectsSpace.Viewer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace EffectsSpace.Workbench;

/// <summary>A reusable, platform-independent motion-design shell. Platform file access is injected.</summary>
public sealed partial class StudioWorkbench : UserControl, IDisposable
{
    private readonly IWorkspaceStorage _storage;
    private readonly Grid _root = new(), _middle = new();
    private readonly StudioPanel _projectPanel = new(), _rightPanel = new(), _bottomPanel = new();
    private readonly StackPanel _projectItems = new() { Spacing = 1 }, _properties = new() { Spacing = 3 }, _effects = new() { Spacing = 4 }, _catalog = new() { Spacing = 2 }, _queue = new() { Spacing = 5 };
    private readonly Dictionary<string, NumericField> _transformFields = [];
    private readonly Dictionary<ViewerTool, StudioButton> _tools = [];
    private readonly TextBlock _status = Studio.Text("Ready", 10, Studio.Muted), _timeLabel = Studio.Text("00:00:00:00", 12, Studio.Accent), _title = Studio.Text("EffectsSpace", 11), _viewerTitle = Studio.Text("Composition", 11), _zoomLabel = Studio.Text("Fit", 10, Studio.Muted);
    private readonly StudioButton _play;
    private readonly DispatcherTimer _playTimer = new() { Interval = TimeSpan.FromMilliseconds(16) }, _recoveryTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly Stopwatch _playWatch = new();
    private double _playStart;
    private bool _refreshQueued, _refreshing, _disposed, _savingRecovery;
    private string _projectFilter = "", _effectFilter = "";
    private CancellationTokenSource? _renderCancellation;
    private readonly List<RenderQueueItem> _renderItems = [];
    public EditorSession Session { get; }
    public CompositionView Viewer { get; }
    public MaskEditView MaskEditor { get; }
    public TimelineView Timeline { get; }
    public bool IsPlaying => _playTimer.IsEnabled;
    public bool IsRendering => _renderCancellation is not null;
    public string Status => _status.Text;
    public event Action? DiagnosticsChanged;

    public StudioWorkbench(EditorSession session, IWorkspaceStorage storage)
    {
        Session = session; _storage = storage; Viewer = new(session); MaskEditor = new(Viewer); Timeline = new(session); IsTabStop = true;
        Background = Studio.Brush(Studio.Background); FontFamily = Studio.Font; Foreground = Studio.Brush(Studio.TextColor);
        _play = new StudioButton("Play / Pause", TogglePlayback, IconKind.Play, false);
        _root.RowDefinitions.Add(new() { Height = new GridLength(28) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(34) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 160 });
        _root.RowDefinitions.Add(new() { Height = new GridLength(5) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(302), MinHeight = 130 });
        _root.RowDefinitions.Add(new() { Height = new GridLength(22) });
        Place(_root, BuildMenu(), 0); Place(_root, BuildToolbar(), 1); BuildMiddle(); InitializeCompositing(); Place(_root, _middle, 2);
        var horizontal = new StudioSplitter(false); horizontal.Dragged += delta => _root.RowDefinitions[4].Height = new GridLength(Math.Clamp(_root.RowDefinitions[4].ActualHeight - delta.Y, 130, Math.Max(160, ActualHeight - 240))); Place(_root, horizontal, 3);
        _bottomPanel.AddTab("Timeline", BuildTimeline()); _bottomPanel.AddTab("Render Queue", Scroll(_queue)); Place(_root, _bottomPanel, 4);
        var footer = new Grid { Background = Studio.Brush("#202020"), Padding = new Thickness(10, 0, 10, 0) };
        footer.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Children.Add(_status);
        var backend = Studio.Text("Uno · Skia canvas  /  sRGB 8-bit", 10, Studio.Muted); Grid.SetColumn(backend, 1); footer.Children.Add(backend); Place(_root, footer, 5);
        Content = _root;
        Viewer.Error += message => ShowStatus(message, true); MaskEditor.Error += message => ShowStatus(message, true); Timeline.Error += message => ShowStatus(message, true);
        Viewer.ViewChanged += () => { UpdateTransport(); DiagnosticsChanged?.Invoke(); };
        Timeline.ViewChanged += () => { UpdateAnimationToolbar(); DiagnosticsChanged?.Invoke(); };
        Session.Changed += Changed;
        _playTimer.Tick += (_, _) => Tick();
        _recoveryTimer.Tick += async (_, _) => { _recoveryTimer.Stop(); await SaveRecoveryAsync(); };
        KeyDown += Shortcut; KeyUp += (_, e) => { if (e.Key == VirtualKey.Space) Viewer.SpaceDown = false; };
        Loaded += (_, _) => { Refresh(); Viewer.Fit(); Timeline.Fit(); Focus(FocusState.Programmatic); DiagnosticsChanged?.Invoke(); };
        SizeChanged += (_, _) => AdaptSize(); Refresh();
    }
    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(8, 7, 8, 7) };
    private static void Place(Grid grid, UIElement child, int row = 0, int column = 0) { Grid.SetRow(child, row); Grid.SetColumn(child, column); grid.Children.Add(child); }
    private StudioButton Button(string name, Action action, IconKind? icon = null, bool text = true) => new(name, () => Run(action), icon, text);
    private void Run(Action action) { try { action(); } catch (Exception ex) { Timeline.CancelInteraction(); Session.CancelEdit(); ShowStatus(ex.Message, true); } }
    private async Task RunAsync(Func<Task> action)
    { try { await action(); } catch (OperationCanceledException) { ShowStatus("Operation cancelled."); } catch (Exception ex) { ShowStatus(ex.Message, true); } }
    public void ShowStatus(string message, bool error = false) { _status.Text = message; _status.Foreground = Studio.Brush(error ? "#E49A90" : Studio.Muted); DiagnosticsChanged?.Invoke(); }
    private void SelectTool(ViewerTool tool) { Pause(); MaskEditor.Stop(); Viewer.Tool = tool; RefreshToolButtons(); Focus(FocusState.Programmatic); ShowStatus(tool + " tool"); }
    private void RefreshToolButtons() { foreach (var (tool, button) in _tools) button.Active = Viewer.Tool == tool; }
    private void Changed(ChangeKind kind)
    {
        UpdateTransport(); UpdateAnimationToolbar(); DiagnosticsChanged?.Invoke();
        if (kind is ChangeKind.Time or ChangeKind.Preview or ChangeKind.KeySelection) { UpdateValues(); UpdateSourceTime(); return; }
        if (kind == ChangeKind.Document) { _recoveryTimer.Stop(); _recoveryTimer.Start(); }
        if (_refreshQueued) return; _refreshQueued = true;
        DispatcherQueue.TryEnqueue(() => { _refreshQueued = false; if (!_disposed) Refresh(); });
    }
    private void Refresh()
    {
        if (_refreshing || _disposed) return; _refreshing = true;
        try { RefreshProject(); RefreshProperties(); RefreshEffects(); RefreshCatalog(); RefreshCompositing(); RefreshQueue(); RefreshToolButtons(); UpdateTransport(); UpdateAnimationToolbar(); }
        finally { _refreshing = false; }
    }
    private void UpdateTransport()
    {
        _timeLabel.Text = Session.Composition.FrameRate.Timecode(Session.Time); _title.Text = Session.Project.Name;
        _viewerTitle.Text = "Composition  ·  " + Session.Composition.Name; _zoomLabel.Text = $"{Viewer.Zoom * 100:0.#}%";
        _play.SetIcon(IsPlaying ? IconKind.Pause : IconKind.Play); _play.Active = IsPlaying;
    }
    public void TogglePlayback()
    {
        Viewer.EndText(true); if (IsPlaying) { Pause(); return; }
        if (Session.IsEditing || IsRendering) return;
        var c = Session.Composition; if (Session.Time < c.WorkStart || Session.Time >= c.WorkEnd - c.FrameRate.Seconds(1)) Session.SetTime(c.WorkStart);
        _playStart = Session.Time; _playWatch.Restart(); _playTimer.Start(); UpdateTransport();
    }
    public void Pause() { _playTimer.Stop(); _playWatch.Stop(); UpdateTransport(); }
    private void Tick()
    {
        if (Session.IsEditing) { Pause(); return; }
        var c = Session.Composition; var span = c.WorkEnd - c.WorkStart;
        Session.SetTime(c.WorkStart + ((_playStart - c.WorkStart + _playWatch.Elapsed.TotalSeconds) % span));
    }
    private void Step(int direction) { Pause(); Session.SetTime(Session.Time + Session.Composition.FrameRate.Seconds(direction)); }
    private void Delete() { if (Session.SelectedKeyIds.Count > 0) Session.DeleteKeys(); else Session.DeleteSelection(); }
    private void ApplyEffect(EffectKind kind)
    {
        if (Session.Primary is null) throw new InvalidOperationException("Select a layer before adding an effect.");
        Session.AddEffect(kind); _projectPanel.Select("Effect Controls"); ShowStatus("Added " + EffectCatalog.Get(kind).Name);
    }
    private void AnimatePreset(string preset)
    {
        Session.Edit("Apply " + preset + " animation", () =>
        {
            foreach (var l in Session.Selection.Where(l => !l.Locked))
            {
                var start = l.InPoint; var end = Math.Min(l.OutPoint - Session.Composition.FrameRate.Seconds(1), start + 1); if (end <= start) continue;
                if (preset == "fade") { l.Transform.Opacity.SetKey(start, 0); l.Transform.Opacity.SetKey(end, 100); l.Transform.Opacity.SetKey(Math.Max(end, l.OutPoint - 1), 100); l.Transform.Opacity.SetKey(l.OutPoint - Session.Composition.FrameRate.Seconds(1), 0); }
                else if (preset == "spin") { l.Transform.Rotation.SetKey(start, 0, Interpolation.Linear); l.Transform.Rotation.SetKey(l.OutPoint - Session.Composition.FrameRate.Seconds(1), 360, Interpolation.Linear); }
                else { var value = CurveEvaluator.Evaluate(l.Transform.Y, Session.Time); l.Transform.Y.SetKey(start, value + 160); l.Transform.Y.SetKey(end, value); }
            }
        });
    }
    private void Nudge(double x, double y)
    {
        Session.Edit("Nudge layers", () => { foreach (var l in Session.Selection.Where(l => !l.Locked)) { EditorCommands.SetChannel(l.Transform.X, Session.Time, CurveEvaluator.Evaluate(l.Transform.X, Session.Time) + x, Session.AutoKey); EditorCommands.SetChannel(l.Transform.Y, Session.Time, CurveEvaluator.Evaluate(l.Transform.Y, Session.Time) + y, Session.AutoKey); } });
    }
    public new void Dispose()
    {
        if (_disposed) return; _disposed = true; _playTimer.Stop(); _recoveryTimer.Stop(); _renderCancellation?.Cancel(); Session.Changed -= Changed; MaskEditor.Dispose(); Viewer.Dispose(); Timeline.Dispose(); base.Dispose();
    }
}
