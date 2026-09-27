using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Viewer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
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
                    list.Children.Add(new StudioButton(item.Name + (item.Shortcut.Length > 0 ? "     " + item.Shortcut : ""), () => { flyout.Hide(); Run(item.Action); }) { HorizontalAlignment = HorizontalAlignment.Stretch });
                }
                flyout.ShowAt(button!);
            }) { Background = Studio.Brush("#202020"), Height = 26, Padding = new Thickness(6, 2, 6, 2) }; return button;
        }
        menus.Children.Add(Menu("File", ("New Project", "", () => Session.Replace(MotionProject.Empty())), ("Open Project…", "Ctrl+O", () => _ = RunAsync(OpenAsync)), ("Save Project…", "Ctrl+S", () => _ = RunAsync(SaveAsync)), ("Import Footage…", "Ctrl+I", () => _ = RunAsync(ImportAsync)), ("-", "", () => { }), ("Export Current Frame…", "", () => _ = RunAsync(ExportFrameAsync)), ("Load Orbital Sample", "", () => { Session.Replace(SampleProject.Create()); Session.SetTime(2.4); Viewer.Fit(); Timeline.Fit(); })));
        menus.Children.Add(Menu("Edit", ("Undo", "Ctrl+Z", Session.Undo), ("Redo", "Ctrl+Shift+Z", Session.Redo), ("Duplicate", "Ctrl+D", () => Session.DuplicateSelection()), ("Delete", "Delete", Delete), ("Select All", "Ctrl+A", Session.SelectAll)));
        menus.Children.Add(Menu("Composition", ("New Composition…", "Ctrl+N", () => _ = RunAsync(NewCompositionAsync)), ("Composition Settings…", "Ctrl+K", () => _ = RunAsync(CompositionSettingsAsync)), ("Add to Render Queue", "Ctrl+M", () => _ = RunAsync(ExportSequenceAsync)), ("Set Work Area Start", "B", () => Session.SetWorkArea(true)), ("Set Work Area End", "N", () => Session.SetWorkArea(false)), ("Add Marker", "", () => Session.AddMarker("Marker " + (Session.Composition.Markers.Count + 1)))));
        menus.Children.Add(Menu("Layer", ("New Solid", "", () => Session.AddLayer(LayerKind.Solid)), ("New Text", "", () => Session.AddLayer(LayerKind.Text)), ("New Null Object", "", () => Session.AddLayer(LayerKind.Null)), ("New Shape", "", () => Session.AddLayer(LayerKind.Rectangle)), ("-", "", () => { }), ("Pre-compose", "Ctrl+Shift+C", () => Session.Precompose()), ("Split Layer", "Ctrl+Shift+D", () => Session.SplitSelection()), ("Trim In Point", "", () => Session.TrimSelection(true)), ("Trim Out Point", "", () => Session.TrimSelection(false)), ("Move Forward", "", () => Session.Reorder(-1)), ("Move Backward", "", () => Session.Reorder(1)), ("Add Rectangular Mask", "", () => Session.AddMask())));
        menus.Children.Add(Menu("Effect", EffectCatalog.All.Select(d => (d.Name, "", (Action)(() => ApplyEffect(d.Kind)))).ToArray()));
        menus.Children.Add(Menu("Animation", ("Add Keyframe", "", () => Session.AddKey(Session.Property)), ("Easy Ease", "F9", () => Session.SetInterpolation(Interpolation.Bezier)), ("Linear", "", () => Session.SetInterpolation(Interpolation.Linear)), ("Hold", "", () => Session.SetInterpolation(Interpolation.Hold)), ("Toggle Graph Editor", "Shift+F3", Timeline.ToggleGraph), ("Animate Position", "", () => AnimatePreset("position")), ("Fade In / Out", "", () => AnimatePreset("fade")), ("Spin", "", () => AnimatePreset("spin"))));
        menus.Children.Add(Menu("View", ("Fit Composition", "", Viewer.Fit), ("100%", "", () => Viewer.SetZoom(1)), ("Transparency Grid", "", () => { Viewer.ShowTransparency = !Viewer.ShowTransparency; Viewer.Invalidate(); }), ("Title / Action Safe", "", () => { Viewer.ShowGuides = !Viewer.ShowGuides; Viewer.Invalidate(); }), ("Grid", "", () => { Viewer.ShowGrid = !Viewer.ShowGrid; Viewer.Invalidate(); })));
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
        foreach (var (tool, icon, shortcut) in new[] { (ViewerTool.Select, IconKind.Select, "V"), (ViewerTool.Hand, IconKind.Hand, "H"), (ViewerTool.Zoom, IconKind.Zoom, "Z"), (ViewerTool.Rotate, IconKind.Rotate, "W"), (ViewerTool.Anchor, IconKind.Anchor, "Y"), (ViewerTool.Rectangle, IconKind.Rectangle, "Q"), (ViewerTool.Ellipse, IconKind.Ellipse, ""), (ViewerTool.Star, IconKind.Star, ""), (ViewerTool.Pen, IconKind.Pen, "G"), (ViewerTool.Text, IconKind.Text, "Shift+T") })
        { var button = Button(tool + (shortcut.Length > 0 ? " (" + shortcut + ")" : ""), () => SelectTool(tool), icon, false); _tools[tool] = button; tools.Children.Add(button); }
        tools.Children.Add(Studio.Separator()); tools.Children.Add(Button("Undo", Session.Undo, IconKind.Undo, false)); tools.Children.Add(Button("Redo", Session.Redo, IconKind.Redo, false)); tools.Children.Add(Studio.Separator());
        StudioButton? snap = null; snap = Button("Snapping", () => { Viewer.Snapping = !Viewer.Snapping; snap!.Active = Viewer.Snapping; }, IconKind.Anchor); snap.Active = true; tools.Children.Add(snap);
        tools.Children.Add(Button("Import", () => _ = RunAsync(ImportAsync), IconKind.Import)); tools.Children.Add(Button("Save", () => _ = RunAsync(SaveAsync), IconKind.Save));
        grid.Children.Add(new ScrollViewer { Content = tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var workspace = new StudioChoice("Workspace", new[] { ("Default", "Default"), ("Animation", "Animation"), ("Composition", "Composition") }, "Default") { Width = 125 }; workspace.ValueChanged += SetWorkspace; Grid.SetColumn(workspace, 1); grid.Children.Add(workspace); return grid;
    }
    private void BuildMiddle()
    {
        _middle.ColumnDefinitions.Add(new() { Width = new GridLength(265), MinWidth = 170 }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 250 }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); _middle.ColumnDefinitions.Add(new() { Width = new GridLength(286), MinWidth = 220 });
        var project = new Grid(); project.RowDefinitions.Add(new() { Height = GridLength.Auto }); project.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); project.RowDefinitions.Add(new() { Height = new GridLength(30) });
        var filter = Studio.Input("", "Search project"); filter.PlaceholderText = "Search project"; filter.Margin = new Thickness(8); filter.TextChanged += (_, _) => { _projectFilter = filter.Text; RefreshProject(); }; Place(project, filter);
        Place(project, Scroll(_projectItems), 1); Place(project, Studio.Row(Button("New composition", () => _ = RunAsync(NewCompositionAsync), IconKind.Composition, false), Button("Import footage", () => _ = RunAsync(ImportAsync), IconKind.Import, false), Studio.Text("Rational frame timing", 10, Studio.Muted)), 2);
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
        StudioButton? auto = null; auto = Button("Auto-key", () => { Session.AutoKey = !Session.AutoKey; auto!.Active = Session.AutoKey; ShowStatus(Session.AutoKey ? "Auto-key enabled" : "Auto-key disabled"); }, IconKind.Stopwatch); row.Children.Add(auto);
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
}
