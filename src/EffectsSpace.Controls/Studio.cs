using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.UI;

namespace EffectsSpace.Controls;

public static class Studio
{
    public const string Background = "#1D1D1D", Panel = "#242424", Raised = "#2D2D2D", BorderColor = "#3C3C3C", TextColor = "#CBCBCB", Muted = "#858585", Accent = "#63A9F2";
    public static FontFamily Font { get; set; } = new("Arial");
    public static SKTypeface Typeface { get; set; } = SKTypeface.Default;
    public static SolidColorBrush Brush(string color)
    { var c = SKColor.Parse(color); return new(Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue)); }
    public static TextBlock Text(string text, double size = 11, string color = TextColor) => new()
    { Text = text, FontFamily = Font, FontSize = size, Foreground = Brush(color), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    public static TextBox Input(string value, string name, double width = double.NaN)
    {
        var box = new TextBox { Text = value, FontFamily = Font, FontSize = 11, Foreground = Brush(TextColor), Background = Brush(Background), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1), Padding = new Thickness(6, 3, 6, 3), MinHeight = 26, Height = 26, CornerRadius = new CornerRadius(2), Width = width };
        AutomationProperties.SetName(box, name); AutomationProperties.SetAutomationId(box, name.Replace(" ", "")); return box;
    }
    public static StackPanel Row(params UIElement[] children)
    { var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center }; foreach (var child in children) row.Children.Add(child); return row; }
    public static Border Separator(bool vertical = true) => new() { Background = Brush(BorderColor), Width = vertical ? 1 : double.NaN, Height = vertical ? 19 : 1, Margin = new Thickness(vertical ? 5 : 0, vertical ? 0 : 5, vertical ? 5 : 0, vertical ? 0 : 5) };
    public static void DrawText(SKCanvas canvas, string text, float x, float y, float size = 11, string color = TextColor)
    { using var font = new SKFont(Typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true }; using var paint = new SKPaint { Color = SKColor.Parse(color), IsAntialias = true }; canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint); }
    public static void Rect(SKCanvas canvas, float x, float y, float w, float h, string color, bool stroke = false)
    { using var p = new SKPaint { Color = SKColor.Parse(color), Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill, StrokeWidth = 1, IsAntialias = false }; canvas.DrawRect(x, y, w, h, p); }
}
