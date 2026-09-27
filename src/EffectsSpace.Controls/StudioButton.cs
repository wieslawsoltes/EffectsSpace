using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace EffectsSpace.Controls;

public sealed class StudioButton : Button
{
    private readonly TextBlock? _text;
    private readonly StudioIcon? _icon;
    private bool _active;
    public bool Active
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value; Background = Studio.Brush(value ? "#314960" : "#282828"); Foreground = Studio.Brush(value ? Studio.Accent : Studio.TextColor);
            if (_text is not null) _text.Foreground = Foreground;
            if (_icon is not null) { _icon.Color = value ? Studio.Accent : Studio.TextColor; _icon.Invalidate(); }
        }
    }
    public StudioButton(string name, Action? action = null, IconKind? icon = null, bool showText = true)
    {
        FontFamily = Studio.Font; FontSize = 11; MinWidth = 0; MinHeight = 0; Height = 27;
        Padding = new Thickness(showText ? 8 : 6, 3, showText ? 8 : 6, 3);
        Foreground = Studio.Brush(Studio.TextColor); Background = Studio.Brush("#282828"); BorderThickness = new Thickness(0);
        HorizontalAlignment = HorizontalAlignment.Left; VerticalAlignment = VerticalAlignment.Center;
        Template = (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='Root' Background='{TemplateBinding Background}' CornerRadius='2'><VisualStateManager.VisualStateGroups><VisualStateGroup x:Name='CommonStates'><VisualState x:Name='Normal'/><VisualState x:Name='PointerOver'><VisualState.Setters><Setter Target='Root.Opacity' Value='0.75'/></VisualState.Setters></VisualState><VisualState x:Name='Pressed'><VisualState.Setters><Setter Target='Root.Opacity' Value='0.55'/></VisualState.Setters></VisualState><VisualState x:Name='Disabled'><VisualState.Setters><Setter Target='Root.Opacity' Value='0.35'/></VisualState.Setters></VisualState></VisualStateGroup></VisualStateManager.VisualStateGroups><ContentPresenter Content='{TemplateBinding Content}' Padding='{TemplateBinding Padding}' HorizontalContentAlignment='Center' VerticalContentAlignment='Center'/></Border></ControlTemplate>");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (icon is { } kind) { _icon = new StudioIcon { Kind = kind, Width = 16, Height = 16 }; row.Children.Add(_icon); }
        if (showText) { _text = Studio.Text(name); row.Children.Add(_text); }
        Content = row;
        AutomationProperties.SetName(this, name); AutomationProperties.SetAutomationId(this, name.Replace(" ", "")); ToolTipService.SetToolTip(this, name);
        if (action is not null) Click += (_, _) => action();
    }
    public void SetText(string text) { if (_text is not null) _text.Text = text; }
    public void SetIcon(IconKind kind) { if (_icon is not null && _icon.Kind != kind) { _icon.Kind = kind; _icon.Invalidate(); } }
}
