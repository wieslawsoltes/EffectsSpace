using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace EffectsSpace.Controls;

/// <summary>A compact numeric editor with typed entry and a transactional horizontal scrub grip.</summary>
public sealed class NumericField : UserControl
{
    private readonly TextBox _box;
    private double _value, _startValue, _startX;
    private bool _dragging;
    public double Minimum { get; set; } = -100000;
    public double Maximum { get; set; } = 100000;
    public double Step { get; set; } = 1;
    public event Action? EditStarted;
    public event Action<double>? ValueChanging;
    public event Action? EditCompleted;
    public event Action? EditCancelled;
    public double Value { get => _value; set { _value = value; if (_box.FocusState != FocusState.Keyboard) _box.Text = Format(value); } }
    public NumericField(string name, double value)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var grip = new Border { Background = Studio.Brush("#292929"), Child = Studio.Text("↔", 10, Studio.Muted) };
        _box = Studio.Input(Format(value), name); _box.Foreground = Studio.Brush(Studio.Accent); _box.Background = Studio.Brush("#242424"); _box.BorderThickness = new Thickness(0, 0, 0, 1); _box.Height = 24; _box.MinHeight = 24; _value = value;
        root.Children.Add(grip); Grid.SetColumn(_box, 1); root.Children.Add(_box); Content = root;
        void CommitText()
        {
            if (!IsEnabled) return;
            if (double.TryParse(_box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
            {
                parsed = Math.Clamp(parsed, Minimum, Maximum);
                if (Math.Abs(parsed - _value) > 1e-9) { EditStarted?.Invoke(); _value = parsed; ValueChanging?.Invoke(parsed); EditCompleted?.Invoke(); }
            }
            _box.Text = Format(_value);
        }
        _box.LostFocus += (_, _) => CommitText();
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter) { CommitText(); e.Handled = true; }
            if (e.Key == VirtualKey.Escape) { _box.Text = Format(_value); e.Handled = true; }
        };
        grip.PointerPressed += (_, e) => { if (!IsEnabled) return; _startX = e.GetCurrentPoint(null).Position.X; _startValue = _value; _dragging = true; EditStarted?.Invoke(); grip.CapturePointer(e.Pointer); e.Handled = true; };
        grip.PointerMoved += (_, e) =>
        {
            if (!_dragging) return; var scale = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) ? .1 : 1;
            _value = Math.Clamp(_startValue + (e.GetCurrentPoint(null).Position.X - _startX) * Step * scale, Minimum, Maximum); _box.Text = Format(_value); ValueChanging?.Invoke(_value); e.Handled = true;
        };
        grip.PointerReleased += (_, e) => { if (!_dragging) return; _dragging = false; grip.ReleasePointerCapture(e.Pointer); EditCompleted?.Invoke(); e.Handled = true; };
        grip.PointerCaptureLost += (_, _) => { if (!_dragging) return; _dragging = false; _value = _startValue; _box.Text = Format(_value); EditCancelled?.Invoke(); };
    }
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
