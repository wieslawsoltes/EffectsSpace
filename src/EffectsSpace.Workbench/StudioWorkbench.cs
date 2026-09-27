using System.Diagnostics;
using System.Globalization;
using System.Text;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Skia;
using EffectsSpace.Timeline;
using EffectsSpace.Viewer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

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
    public TimelineView Timeline { get; }
    public bool IsPlaying => _playTimer.IsEnabled;
    public bool IsRendering => _renderCancellation is not null;
    public string Status => _status.Text;
    public event Action? DiagnosticsChanged;

    public StudioWorkbench(EditorSession session, IWorkspaceStorage storage)
    {
        Session = session; _storage = storage; Viewer = new(session); Timeline = new(session); IsTabStop = true;
        Background = Studio.Brush(Studio.Background); FontFamily = Studio.Font; Foreground = Studio.Brush(Studio.TextColor);
        _play = new StudioButton("Play / Pause", TogglePlayback, IconKind.Play, false);
        _root.RowDefinitions.Add(new() { Height = new GridLength(28) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(34) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 160 });
        _root.RowDefinitions.Add(new() { Height = new GridLength(5) });
        _root.RowDefinitions.Add(new() { Height = new GridLength(302), MinHeight = 130 });
        _root.RowDefinitions.Add(new() { Height = new GridLength(22) });
        Place(_root, BuildMenu(), 0); Place(_root, BuildToolbar(), 1);
        BuildMiddle(); Place(_root, _middle, 2);
        var horizontal = new StudioSplitter(false); horizontal.Dragged += delta => _root.RowDefinitions[4].Height = new GridLength(Math.Clamp(_root.RowDefinitions[4].ActualHeight - delta.Y, 130, Math.Max(160, ActualHeight - 240))); Place(_root, horizontal, 3);
        _bottomPanel.AddTab("Timeline", BuildTimeline()); _bottomPanel.AddTab("Render Queue", Scroll(_queue)); Place(_root, _bottomPanel, 4);
        var footer = new Grid { Background = Studio.Brush("#202020"), Padding = new Thickness(10, 0, 10, 0) }; footer.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Children.Add(_status); var backend = Studio.Text("Uno · Skia canvas  /  sRGB 8-bit", 10, Studio.Muted); Grid.SetColumn(backend, 1); footer.Children.Add(backend); Place(_root, footer, 5);
        Content = _root;
        Viewer.Error += message => ShowStatus(message, true); Timeline.Error += message => ShowStatus(message, true);
        Viewer.ViewChanged += () => { UpdateTransport(); DiagnosticsChanged?.Invoke(); }; Timeline.ViewChanged += () => DiagnosticsChanged?.Invoke();
        Session.Changed += Changed;
        _playTimer.Tick += (_, _) => Tick();
        _recoveryTimer.Tick += async (_, _) => { _recoveryTimer.Stop(); await SaveRecoveryAsync(); };
        KeyDown += Shortcut; KeyUp += (_, e) => { if (e.Key == VirtualKey.Space) Viewer.SpaceDown = false; };
        Loaded += (_, _) => { Refresh(); Viewer.Fit(); Timeline.Fit(); Focus(FocusState.Programmatic); DiagnosticsChanged?.Invoke(); };
        SizeChanged += (_, _) => AdaptSize();
        Refresh();
    }
    private static ScrollViewer Scroll(UIElement child) => new() { Content = child, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(8, 7, 8, 7) };
    private static void Place(Grid grid, UIElement child, int row = 0, int column = 0) { Grid.SetRow(child, row); Grid.SetColumn(child, column); grid.Children.Add(child); }
    private StudioButton Button(string name, Action action, IconKind? icon = null, bool text = true) => new(name, () => Run(action), icon, text);
    private void Run(Action action)
    {
        try { action(); } catch (Exception ex) { Session.CancelEdit(); ShowStatus(ex.Message, true); }
    }
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); } catch (OperationCanceledException) { ShowStatus("Operation cancelled."); } catch (Exception ex) { ShowStatus(ex.Message, true); }
    }
    public void ShowStatus(string message, bool error = false) { _status.Text = message; _status.Foreground = Studio.Brush(error ? "#E49A90" : Studio.Muted); DiagnosticsChanged?.Invoke(); }
    private FrameworkElement BuildMenu()
    {
        var grid = new Grid { Background = Studio.Brush("#202020"), Padding = new Thickness(7, 0, 8, 0) }; grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var menus = Studio.Row();
        StudioButton Menu(string name, params (string Name, string Shortcut, Action Action)[] items)
        {
            StudioButton? button = null;
            button = new StudioButton(name, () =>
            {
                var list = new StackPanel { Spacing = 2, MinWidth = 260, Background = Studio.Brush(Studio.Panel) }; var flyout = new Flyout { Content = list };
                foreach (var item in items)
                {
                    if (item.Name == "-") { list.Children.Add(Studio.Separator(false)); continue; }
                    var entry = new StudioButton(item.Name + (item.Shortcut.Length > 0 ? "     " + item.Shortcut : ""), () => { flyout.Hide(); Run(item.Action); }) { HorizontalAlignment = HorizontalAlignment.Stretch };
                    list.Children.Add(entry);
                }
                flyout.ShowAt(button!);
            }) { Background = Studio.Brush("#202020"), Height = 26, Padding = new Thickness(6, 2, 6, 2) }; return button;
        }
        menus.Children.Add(Menu("File", ("New Project", "Ctrl+Alt+N", () => Session.Replace(MotionProject.Empty())), ("Open Project…", "Ctrl+O", () => _ = RunAsync(OpenAsync)), ("Save Project…", "Ctrl+S", () => _ = RunAsync(SaveAsync)), ("Import Footage…", "Ctrl+I", () => _ = RunAsync(ImportAsync)), ("-", "", () => { }), ("Export Current Frame…", "", () => _ = RunAsync(ExportFrameAsync)), ("Load Orbital Sample", "", () => { Session.Replace(SampleProject.Create()); Session.SetTime(2.4); Viewer.Fit(); Timeline.Fit(); })));
        menus.Children.Add(Menu("Edit", ("Undo", "Ctrl+Z", Session.Undo), ("Redo", "Ctrl+Shift+Z", Session.Redo), ("Duplicate", "Ctrl+D", () => Session.DuplicateSelection()), ("Delete", "Delete", Delete), ("Select All", "Ctrl+A", Session.SelectAll)));
        menus.Children.Add(Menu("Composition", ("New Composition…", "Ctrl+N", () => _ = RunAsync(NewCompositionAsync)), ("Composition Settings…", "Ctrl+K", () => _ = RunAsync(CompositionSettingsAsync)), ("Add to Render Queue", "Ctrl+M", () => _ = RunAsync(ExportSequenceAsync)), ("Set Work Area Start", "B", () => Session.SetWorkArea(true)), ("Set Work Area End", "N", () => Session.SetWorkArea(false)), ("Add Marker", "*", () => Session.AddMarker("Marker " + (Session.Composition.Markers.Count + 1)))));
        menus.Children.Add(Menu("Layer", ("New Solid", "", () => Session.AddLayer(LayerKind.Solid)), ("New Text", "", () => Session.AddLayer(LayerKind.Text)), ("New Null Object", "", () => Session.AddLayer(LayerKind.Null)), ("New Shape", "", () => Session.AddLayer(LayerKind.Rectangle)), ("-", "", () => { }), ("Pre-compose", "Ctrl+Shift+C", () => Session.Precompose()), ("Split Layer", "Ctrl+Shift+D", () => Session.SplitSelection()), ("Trim In Point", "Alt+[", () => Session.TrimSelection(true)), ("Trim Out Point", "Alt+]", () => Session.TrimSelection(false)), ("Move Forward", "", () => Session.Reorder(-1)), ("Move Backward", "", () => Session.Reorder(1)), ("Add Rectangular Mask", "", () => Session.AddMask())));
        menus.Children.Add(Menu("Effect", EffectCatalog.All.Select(d => (d.Name, "", (Action)(() => ApplyEffect(d.Kind)))).ToArray()));
        menus.Children.Add(Menu("Animation", ("Add Keyframe", "", () => Session.AddKey(Session.Property)), ("Easy Ease", "F9", () => Session.SetInterpolation(Interpolation.Bezier)), ("Linear", "", () => Session.SetInterpolation(Interpolation.Linear)), ("Hold", "", () => Session.SetInterpolation(Interpolation.Hold)), ("Toggle Graph Editor", "Shift+F3", Timeline.ToggleGraph), ("Animate Position", "", () => AnimatePreset("position")), ("Fade In / Out", "", () => AnimatePreset("fade")), ("Spin", "", () => AnimatePreset("spin"))));
        menus.Children.Add(Menu("View", ("Fit Composition", "Shift+/", Viewer.Fit), ("100%", "", () => Viewer.SetZoom(1)), ("Transparency Grid", "", () => { Viewer.ShowTransparency = !Viewer.ShowTransparency; Viewer.Invalidate(); }), ("Title / Action Safe", "", () => { Viewer.ShowGuides = !Viewer.ShowGuides; Viewer.Invalidate(); }), ("Grid", "", () => { Viewer.ShowGrid = !Viewer.ShowGrid; Viewer.Invalidate(); })));
        menus.Children.Add(Menu("Window", ("Default Workspace", "", () => SetWorkspace("Default")), ("Animation Workspace", "", () => SetWorkspace("Animation")), ("Composition Only", "", () => SetWorkspace("Composition")), ("Project", "", () => _projectPanel.Select("Project")), ("Effect Controls", "", () => _projectPanel.Select("Effect Controls")), ("Properties", "", () => _rightPanel.Select("Properties")), ("Effects & Presets", "", () => _rightPanel.Select("Effects & Presets")), ("Render Queue", "", () => _bottomPanel.Select("Render Queue"))));
        menus.Children.Add(Menu("Help", ("EffectsSpace User Guide", "", () => _ = RunAsync(ShowHelpAsync)), ("Keyboard Shortcuts", "", () => _ = RunAsync(ShowHelpAsync)), ("About EffectsSpace", "", () => _ = RunAsync(ShowAboutAsync))));
        grid.Children.Add(menus); _title.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(_title, 1); grid.Children.Add(_title);
        var brand = Studio.Text("EffectsSpace", 11, "#B8A5EF"); Grid.SetColumn(brand, 2); grid.Children.Add(brand); return grid;
    }
    private FrameworkElement BuildToolbar()
    {
        var grid = new Grid { Background = Studio.Brush("#292929"), Padding = new Thickness(9, 2, 9, 2), BorderBrush = Studio.Brush("#111111"), BorderThickness = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var tools = Studio.Row();
        foreach (var (tool, icon, shortcut) in new[] { (ViewerTool.Select, IconKind.Select, "V"), (ViewerTool.Hand, IconKind.Hand, "H"), (ViewerTool.Zoom, IconKind.Zoom, "Z"), (ViewerTool.Rotate, IconKind.Rotate, "W"), (ViewerTool.Anchor, IconKind.Anchor, "Y"), (ViewerTool.Rectangle, IconKind.Rectangle, "Q"), (ViewerTool.Ellipse, IconKind.Ellipse, ""), (ViewerTool.Star, IconKind.Star, ""), (ViewerTool.Pen, IconKind.Pen, "G"), (ViewerTool.Text, IconKind.Text, "T") })
        {
            var button = Button(tool + (shortcut.Length > 0 ? " (" + shortcut + ")" : ""), () => SelectTool(tool), icon, false); _tools[tool] = button; tools.Children.Add(button);
        }
        tools.Children.Add(Studio.Separator()); tools.Children.Add(Button("Undo", Session.Undo, IconKind.Undo, false)); tools.Children.Add(Button("Redo", Session.Redo, IconKind.Redo, false)); tools.Children.Add(Studio.Separator());
        var snap = Button("Snapping", () => { Viewer.Snapping = !Viewer.Snapping; RefreshToolButtons(); }, IconKind.Anchor); snap.Active = true; tools.Children.Add(snap);
        tools.Children.Add(Button("Import", () => _ = RunAsync(ImportAsync), IconKind.Import)); tools.Children.Add(Button("Save", () => _ = RunAsync(SaveAsync), IconKind.Save));
        grid.Children.Add(new ScrollViewer { Content = tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var workspace = new StudioChoice("Workspace", new[] { ("Default", "Default"), ("Animation", "Animation"), ("Composition", "Composition") }, "Default") { Width = 125 }; workspace.ValueChanged += SetWorkspace; Grid.SetColumn(workspace, 1); grid.Children.Add(workspace); return grid;
    }
    private void BuildMiddle()
    {
        _middle.ColumnDefinitions.Add(new() { Width = new GridLength(265), MinWidth = 170 }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(286), MinWidth = 220 });
        var project = new Grid(); project.RowDefinitions.Add(new() { Height = GridLength.Auto }); project.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); project.RowDefinitions.Add(new() { Height = new GridLength(30) });
        var filter = Studio.Input("", "Search project"); filter.PlaceholderText = "Search project"; filter.Margin = new Thickness(8); filter.TextChanged += (_, _) => { _projectFilter = filter.Text; RefreshProject(); }; Place(project, filter);
        Place(project, Scroll(_projectItems), 1); Place(project, Studio.Row(Button("New composition", () => _ = RunAsync(NewCompositionAsync), IconKind.Composition, false), Button("Import footage", () => _ = RunAsync(ImportAsync), IconKind.Import, false), Studio.Text("32-bit project time", 10, Studio.Muted)), 2);
        _projectPanel.AddTab("Project", project); _projectPanel.AddTab("Effect Controls", Scroll(_effects)); Place(_middle, _projectPanel, 0, 0);
        var left = new StudioSplitter(true); left.Dragged += d => _middle.ColumnDefinitions[0].Width = new GridLength(Math.Clamp(_middle.ColumnDefinitions[0].ActualWidth + d.X, 170, 520)); Place(_middle, left, 0, 1);
        var viewer = new Grid { Background = Studio.Brush("#171717") }; viewer.RowDefinitions.Add(new() { Height = new GridLength(29) }); viewer.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); viewer.RowDefinitions.Add(new() { Height = new GridLength(31) });
        var viewerHeader = new Grid { Background = Studio.Brush(Studio.Panel), Padding = new Thickness(10, 0, 8, 0), BorderBrush = Studio.Brush(Studio.BorderColor), BorderThickness = new Thickness(0, 0, 0, 1) }; viewerHeader.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); viewerHeader.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); viewerHeader.Children.Add(_viewerTitle); var fit = Button("Fit", Viewer.Fit); Grid.SetColumn(fit, 1); viewerHeader.Children.Add(fit); Place(viewer, viewerHeader);
        Place(viewer, Viewer, 1);
        var viewerTools = Studio.Row(Button("Fit viewer", Viewer.Fit, IconKind.Zoom, false), _zoomLabel, Studio.Separator(), Button("Transparency grid", () => { Viewer.ShowTransparency = !Viewer.ShowTransparency; Viewer.Invalidate(); }, IconKind.Checker, false), Button("Title / Action safe", () => { Viewer.ShowGuides = !Viewer.ShowGuides; Viewer.Invalidate(); }, IconKind.Guides, false), Button("Composition grid", () => { Viewer.ShowGrid = !Viewer.ShowGrid; Viewer.Invalidate(); }, IconKind.Grid, false), Studio.Separator(), _timeLabel, Studio.Separator(), Button("Motion blur (4 samples)", () => { Viewer.Renderer.MotionBlurSamples = Viewer.Renderer.MotionBlurSamples == 1 ? 4 : 1; Viewer.Invalidate(); ShowStatus("Motion blur samples: " + Viewer.Renderer.MotionBlurSamples); }, IconKind.Ellipse, false), Button("Snapshot PNG", () => _ = RunAsync(ExportFrameAsync), IconKind.Render, false));
        Place(viewer, new ScrollViewer { Content = viewerTools, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Studio.Brush(Studio.Panel) }, 2); Place(_middle, viewer, 0, 2);
        var right = new StudioSplitter(true); right.Dragged += d => _middle.ColumnDefinitions[4].Width = new GridLength(Math.Clamp(_middle.ColumnDefinitions[4].ActualWidth - d.X, 220, 500)); Place(_middle, right, 0, 3);
        _rightPanel.AddTab("Properties", Scroll(_properties));
        var effectPanel = new Grid(); effectPanel.RowDefinitions.Add(new() { Height = GridLength.Auto }); effectPanel.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var search = Studio.Input("", "Search effects"); search.PlaceholderText = "Search effects & presets"; search.Margin = new Thickness(8); search.TextChanged += (_, _) => { _effectFilter = search.Text; RefreshCatalog(); }; Place(effectPanel, search); Place(effectPanel, Scroll(_catalog), 1); _rightPanel.AddTab("Effects & Presets", effectPanel); Place(_middle, _rightPanel, 0, 4);
    }
    private UIElement BuildTimeline()
    {
        var grid = new Grid(); grid.RowDefinitions.Add(new() { Height = new GridLength(30) }); grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var row = Studio.Row(Button("First frame", () => { Pause(); Session.SetTime(Session.Composition.WorkStart); }, IconKind.First, false), Button("Previous frame", () => Step(-1), IconKind.Previous, false), _play, Button("Next frame", () => Step(1), IconKind.Next, false), Button("Last frame", () => { Pause(); Session.SetTime(Session.Composition.WorkEnd - Session.Composition.FrameRate.Seconds(1)); }, IconKind.Last, false), Studio.Separator(), Button("Graph Editor", Timeline.ToggleGraph, IconKind.Graph), Button("Add Keyframe", () => Session.AddKey(Session.Property), IconKind.Keyframe, false), Button("Easy Ease", () => Session.SetInterpolation(Interpolation.Bezier), IconKind.Graph, false), Button("Split Layer", () => Session.SplitSelection(), IconKind.Split, false), Button("Duplicate Layer", () => Session.DuplicateSelection(), IconKind.Add, false), Button("Delete Selection", Delete, IconKind.Delete, false), Studio.Separator(), Button("Fit timeline", Timeline.Fit, IconKind.Zoom, false), Button("Zoom timeline in", () => Timeline.ZoomBy(1.4), IconKind.Add, false), Button("Zoom timeline out", () => Timeline.ZoomBy(1 / 1.4), IconKind.Zoom, false));
        var auto = Button("Auto-key", () => { Session.AutoKey = !Session.AutoKey; ShowStatus(Session.AutoKey ? "Auto-key enabled" : "Auto-key disabled"); }, IconKind.Stopwatch); row.Children.Add(auto);
        Place(grid, new ScrollViewer { Content = row, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Studio.Brush("#282828") }); Place(grid, Timeline, 1); return grid;
    }
    private void SetWorkspace(string name)
    {
        var composition = name == "Composition"; _projectPanel.Visibility = _rightPanel.Visibility = composition ? Visibility.Collapsed : Visibility.Visible;
        _middle.ColumnDefinitions[0].MinWidth = _middle.ColumnDefinitions[4].MinWidth = 0;
        _middle.ColumnDefinitions[0].Width = new GridLength(composition ? 0 : 265); _middle.ColumnDefinitions[4].Width = new GridLength(composition ? 0 : 286);
        _root.RowDefinitions[4].Height = new GridLength(name == "Animation" ? Math.Max(280, ActualHeight * .45) : composition ? 150 : 302);
        Viewer.Fit(); Timeline.Fit(); ShowStatus(name + " workspace");
    }
    private void AdaptSize()
    {
        if (ActualWidth is > 0 and < 950) { _middle.ColumnDefinitions[0].MinWidth = 0; _middle.ColumnDefinitions[0].Width = new GridLength(0); _projectPanel.Visibility = Visibility.Collapsed; }
        if (ActualHeight is > 0 and < 650) _root.RowDefinitions[4].Height = new GridLength(190);
    }
    private void SelectTool(ViewerTool tool) { Pause(); Viewer.Tool = tool; RefreshToolButtons(); Focus(FocusState.Programmatic); ShowStatus(tool + " tool"); }
    private void RefreshToolButtons() { foreach (var (tool, button) in _tools) button.Active = Viewer.Tool == tool; }
    private void Changed(ChangeKind kind)
    {
        UpdateTransport(); DiagnosticsChanged?.Invoke();
        if (kind is ChangeKind.Time or ChangeKind.Preview) { UpdateValues(); return; }
        if (kind == ChangeKind.Document) { _recoveryTimer.Stop(); _recoveryTimer.Start(); }
        if (_refreshQueued) return; _refreshQueued = true;
        DispatcherQueue.TryEnqueue(() => { _refreshQueued = false; if (!_disposed) Refresh(); });
    }
    private void Refresh()
    {
        if (_refreshing || _disposed) return; _refreshing = true;
        try { RefreshProject(); RefreshProperties(); RefreshEffects(); RefreshCatalog(); RefreshQueue(); RefreshToolButtons(); UpdateTransport(); }
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
    private void Delete() { if (Session.SelectedKeyId is not null) Session.DeleteSelectedKey(); else Session.DeleteSelection(); }
    private void ApplyEffect(EffectKind kind) { if (Session.Primary is null) throw new InvalidOperationException("Select a layer before adding an effect."); Session.AddEffect(kind); _projectPanel.Select("Effect Controls"); ShowStatus("Added " + EffectCatalog.Get(kind).Name); }
    private void AnimatePreset(string preset)
    {
        Session.Edit("Apply " + preset + " animation", () =>
        {
            foreach (var l in Session.Selection.Where(l => !l.Locked))
            {
                var start = l.InPoint; var end = Math.Min(l.OutPoint - Session.Composition.FrameRate.Seconds(1), start + 1);
                if (end <= start) continue;
                if (preset == "fade") { l.Transform.Opacity.SetKey(start, 0); l.Transform.Opacity.SetKey(end, 100); l.Transform.Opacity.SetKey(Math.Max(end, l.OutPoint - 1), 100); l.Transform.Opacity.SetKey(l.OutPoint - Session.Composition.FrameRate.Seconds(1), 0); }
                else if (preset == "spin") { l.Transform.Rotation.SetKey(start, 0, Interpolation.Linear); l.Transform.Rotation.SetKey(l.OutPoint - Session.Composition.FrameRate.Seconds(1), 360, Interpolation.Linear); }
                else { var value = CurveEvaluator.Evaluate(l.Transform.Y, Session.Time); l.Transform.Y.SetKey(start, value + 160); l.Transform.Y.SetKey(end, value); }
            }
        });
    }
    private void Shortcut(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        var focused = FocusManager.GetFocusedElement(XamlRoot);
        if (focused is TextBox or PasswordBox) return;
        bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        var ctrl = Down(VirtualKey.Control), shift = Down(VirtualKey.Shift), alt = Down(VirtualKey.Menu);
        var handled = true;
        Run(() =>
        {
            if (ctrl)
            {
                switch (e.Key)
                {
                    case VirtualKey.Z: if (shift) Session.Redo(); else Session.Undo(); break;
                    case VirtualKey.Y: Session.Redo(); break;
                    case VirtualKey.S: _ = RunAsync(SaveAsync); break;
                    case VirtualKey.O: _ = RunAsync(OpenAsync); break;
                    case VirtualKey.I: _ = RunAsync(ImportAsync); break;
                    case VirtualKey.N: _ = RunAsync(NewCompositionAsync); break;
                    case VirtualKey.K: _ = RunAsync(CompositionSettingsAsync); break;
                    case VirtualKey.D: if (shift) Session.SplitSelection(); else Session.DuplicateSelection(); break;
                    case VirtualKey.C: if (shift) Session.Precompose(); else handled = false; break;
                    case VirtualKey.A: Session.SelectAll(); break;
                    case VirtualKey.M: _ = RunAsync(ExportSequenceAsync); break;
                    default: handled = false; break;
                }
            }
            else switch (e.Key)
            {
                case VirtualKey.Space: TogglePlayback(); break;
                case VirtualKey.Home: Pause(); Session.SetTime(0); break;
                case VirtualKey.End: Pause(); Session.SetTime(Session.Composition.LastFrameTime); break;
                case VirtualKey.PageUp: Step(shift ? -10 : -1); break;
                case VirtualKey.PageDown: Step(shift ? 10 : 1); break;
                case VirtualKey.Delete: case VirtualKey.Back: Delete(); break;
                case VirtualKey.F9: Session.SetInterpolation(Interpolation.Bezier); break;
                case VirtualKey.F3: if (shift) Timeline.ToggleGraph(); else handled = false; break;
                case VirtualKey.B: Session.SetWorkArea(true); break;
                case VirtualKey.N: Session.SetWorkArea(false); break;
                case VirtualKey.V: SelectTool(ViewerTool.Select); break;
                case VirtualKey.H: SelectTool(ViewerTool.Hand); break;
                case VirtualKey.Z: SelectTool(ViewerTool.Zoom); break;
                case VirtualKey.W: SelectTool(ViewerTool.Rotate); break;
                case VirtualKey.Y: SelectTool(ViewerTool.Anchor); break;
                case VirtualKey.Q: SelectTool(ViewerTool.Rectangle); break;
                case VirtualKey.G: SelectTool(ViewerTool.Pen); break;
                case VirtualKey.T: if (shift || Session.Primary is null) SelectTool(ViewerTool.Text); else { Session.Property = "Opacity"; Timeline.ShowProperties("T"); } break;
                case VirtualKey.P: Session.Property = "X"; Timeline.ShowProperties("P"); break;
                case VirtualKey.S: Session.Property = "ScaleX"; Timeline.ShowProperties("S"); break;
                case VirtualKey.R: Session.Property = "Rotation"; Timeline.ShowProperties("R"); break;
                case VirtualKey.A: Session.Property = "AnchorX"; Timeline.ShowProperties("A"); break;
                case VirtualKey.U: Timeline.ShowProperties("U"); break;
                case VirtualKey.Left: Nudge(shift ? -10 : -1, 0); break;
                case VirtualKey.Right: Nudge(shift ? 10 : 1, 0); break;
                case VirtualKey.Up: Nudge(0, shift ? -10 : -1); break;
                case VirtualKey.Down: Nudge(0, shift ? 10 : 1); break;
                case VirtualKey.Escape: Pause(); Viewer.CancelDrag(); Viewer.EndText(false); Session.CancelEdit(); break;
                default: handled = false; break;
            }
        });
        e.Handled = handled;
    }
    private void Nudge(double x, double y)
    {
        Session.Edit("Nudge layers", () => { foreach (var l in Session.Selection.Where(l => !l.Locked)) { EditorCommands.SetChannel(l.Transform.X, Session.Time, CurveEvaluator.Evaluate(l.Transform.X, Session.Time) + x, Session.AutoKey); EditorCommands.SetChannel(l.Transform.Y, Session.Time, CurveEvaluator.Evaluate(l.Transform.Y, Session.Time) + y, Session.AutoKey); } });
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _playTimer.Stop(); _recoveryTimer.Stop(); _renderCancellation?.Cancel(); Session.Changed -= Changed; Viewer.Dispose(); Timeline.Dispose();
    }
}
