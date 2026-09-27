using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Controls;

/// <summary>Compact, keyboard-focusable studio choice control with original item chrome.</summary>
public sealed class StudioChoice : UserControl
{
    private readonly StudioButton _button;
    private readonly List<(string Label, string Value)> _items;
    public string Value { get; private set; }
    public event Action<string>? ValueChanged;
    public StudioChoice(string name, IEnumerable<(string Label, string Value)> items, string value)
    {
        _items = items.ToList(); Value = value;
        _button = new StudioButton(Label(value) + "  ▾", Open) { HorizontalAlignment = HorizontalAlignment.Stretch, Height = 25 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_button, name); Content = _button;
    }
    private string Label(string value) => _items.FirstOrDefault(i => i.Value == value).Label ?? value;
    private void Open()
    {
        var list = new StackPanel { Spacing = 2, MinWidth = Math.Max(130, ActualWidth), Background = Studio.Brush(Studio.Panel) };
        var flyout = new Flyout { Content = new ScrollViewer { Content = list, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        foreach (var item in _items)
        {
            var button = new StudioButton(item.Label, () => { Value = item.Value; _button.SetText(Label(Value) + "  ▾"); flyout.Hide(); ValueChanged?.Invoke(Value); }) { HorizontalAlignment = HorizontalAlignment.Stretch, Active = item.Value == Value };
            list.Children.Add(button);
        }
        flyout.ShowAt(_button);
    }
}
