using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Controls;

public sealed class StudioPanel : Grid
{
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 0 };
    private readonly ScrollViewer _tabScroll = new() { HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border _body = new();
    private readonly Dictionary<string, (StudioButton Button, UIElement Content)> _items = [];
    public string SelectedTab { get; private set; } = "";
    public event Action<string>? TabChanged;
    public StudioPanel()
    {
        Background = Studio.Brush(Studio.Panel); BorderBrush = Studio.Brush("#101010"); BorderThickness = new Thickness(1);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) }); RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _tabScroll.Content = _tabs;
        var header = new Border { BorderBrush = Studio.Brush(Studio.BorderColor), BorderThickness = new Thickness(0, 0, 0, 1), Child = _tabScroll, Background = Studio.Brush(Studio.Panel) };
        Children.Add(header); Grid.SetRow(_body, 1); Children.Add(_body);
    }
    public void AddTab(string name, UIElement content)
    {
        var button = new StudioButton(name, () => Select(name)) { Height = 28, Padding = new Thickness(10, 2, 10, 2) };
        _tabs.Children.Add(button); _items[name] = (button, content); if (_items.Count == 1) Select(name);
    }
    public void Select(string name)
    {
        if (!_items.TryGetValue(name, out var item)) return;
        foreach (var entry in _items) { entry.Value.Button.Active = entry.Key == name; entry.Value.Button.Background = Studio.Brush(Studio.Panel); }
        _body.Child = item.Content; SelectedTab = name; TabChanged?.Invoke(name);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (SelectedTab != name || !item.Button.IsLoaded) return;
            var x = item.Button.TransformToVisual(_tabs).TransformPoint(new Windows.Foundation.Point()).X;
            if (x < _tabScroll.HorizontalOffset) _tabScroll.ChangeView(x, null, null, true);
            else if (x + item.Button.ActualWidth > _tabScroll.HorizontalOffset + _tabScroll.ViewportWidth)
                _tabScroll.ChangeView(Math.Max(0, x + item.Button.ActualWidth - _tabScroll.ViewportWidth), null, null, true);
        });
    }
}
