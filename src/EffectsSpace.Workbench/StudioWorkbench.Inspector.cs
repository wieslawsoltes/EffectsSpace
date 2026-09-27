using System.Globalization;
using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Timeline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private static TextBlock Heading(string text) => new() { Text = text, FontFamily = Studio.Font, FontSize = 11, Foreground = Studio.Brush("#DDDDDD"), Margin = new Thickness(0, 7, 0, 3) };
    private static Grid FieldRow(string label, UIElement field, double labelWidth = 92)
    {
        var row = new Grid { MinHeight = 26 }; row.ColumnDefinitions.Add(new() { Width = new GridLength(labelWidth) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Studio.Text(label, 10, Studio.Muted)); Grid.SetColumn(field, 1); row.Children.Add(field); return row;
    }
    private TextBox TextField(string value, string name, Action<string> apply, bool multiline = false)
    {
        var box = Studio.Input(value, name); box.IsEnabled = Session.Primary?.Locked != true;
        if (multiline) { box.Height = 74; box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; }
        var original = value;
        void Commit() { if (_refreshing || box.Text == original) return; Run(() => { apply(box.Text); original = box.Text; }); }
        box.LostFocus += (_, _) => Commit(); box.KeyDown += (_, e) => { if (!multiline && e.Key == VirtualKey.Enter) { Commit(); e.Handled = true; } }; return box;
    }
    private NumericField Number(string label, double value, Action<double> set, double minimum = -100000, double maximum = 100000, double step = 1)
    {
        var field = new NumericField(label, value) { Minimum = minimum, Maximum = maximum, Step = step, IsEnabled = Session.Primary?.Locked != true };
        field.EditStarted += () => Run(() => { Pause(); if (!Session.IsEditing) Session.BeginEdit("Set " + label); });
        field.ValueChanging += next => Run(() => { set(next); Session.PreviewChanged(); });
        field.EditCompleted += () => Run(Session.CommitEdit); field.EditCancelled += Session.CancelEdit; return field;
    }
    private StudioChoice Choice(string name, IEnumerable<(string Label, string Value)> values, string current, Action<string> change)
    {
        var choice = new StudioChoice(name, values, current) { IsEnabled = Session.Primary?.Locked != true };
        choice.ValueChanged += value => Run(() => Session.Edit("Set " + name, () => change(value))); return choice;
    }
    private void RefreshProject()
    {
        _projectItems.Children.Clear();
        var info = Studio.Text(Session.Project.Name, 12, "#D6D6D6"); info.Margin = new Thickness(2, 4, 2, 5); _projectItems.Children.Add(info);
        _projectItems.Children.Add(Studio.Text($"{Session.Project.Compositions.Count} compositions  ·  {Session.Project.Assets.Count} footage items", 10, Studio.Muted)); _projectItems.Children.Add(Studio.Separator(false));
        _projectItems.Children.Add(Heading("▾  COMPOSITIONS"));
        foreach (var comp in Session.Project.Compositions.Where(c => c.Name.Contains(_projectFilter, StringComparison.OrdinalIgnoreCase)))
        {
            var button = Button(comp.Name, () => { Pause(); Session.Activate(comp.Id); Viewer.Fit(); Timeline.Fit(); }, IconKind.Composition); button.HorizontalAlignment = HorizontalAlignment.Stretch; button.Active = comp.Id == Session.Project.ActiveCompositionId; _projectItems.Children.Add(button);
            _projectItems.Children.Add(new TextBlock { Text = $"      {comp.Width} × {comp.Height}   {comp.FrameRate}   {comp.Duration:0.##}s", FontFamily = Studio.Font, FontSize = 9, Foreground = Studio.Brush(Studio.Muted), Margin = new Thickness(0, 0, 0, 4) });
        }
        _projectItems.Children.Add(Heading("▾  FOOTAGE"));
        foreach (var asset in Session.Project.Assets.Where(a => a.Name.Contains(_projectFilter, StringComparison.OrdinalIgnoreCase)))
        { var button = Button(asset.Name, () => AddAssetLayer(asset), asset.MimeType.StartsWith("audio/") ? IconKind.Audio : IconKind.Folder); button.HorizontalAlignment = HorizontalAlignment.Stretch; _projectItems.Children.Add(button); }
        if (Session.Project.Assets.Count == 0) _projectItems.Children.Add(new TextBlock { Text = "Import PNG, JPEG or WebP footage.\nClick an item to add it to the composition.", FontFamily = Studio.Font, FontSize = 10, Foreground = Studio.Brush(Studio.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 5, 3, 4) });
    }
    private void RefreshProperties()
    {
        _properties.Children.Clear(); _transformFields.Clear(); var layer = Session.Primary;
        _properties.Children.Add(Heading(layer is null ? "COMPOSITION" : $"{layer.Kind.ToString().ToUpperInvariant()}  /  {Session.SelectedIds.Count} SELECTED"));
        if (layer is null)
        {
            _properties.Children.Add(Studio.Text(Session.Composition.Name, 12)); _properties.Children.Add(Studio.Text($"{Session.Composition.Width} × {Session.Composition.Height}  ·  {Session.Composition.FrameRate}", 10, Studio.Muted));
            _properties.Children.Add(Studio.Text($"Duration  {Session.Composition.Duration:0.###} seconds", 10, Studio.Muted));
            _properties.Children.Add(Button("Composition Settings", () => _ = RunAsync(CompositionSettingsAsync), IconKind.Settings));
            _properties.Children.Add(Studio.Separator(false));
            _properties.Children.Add(Heading("CREATE A LAYER"));
            foreach (var kind in new[] { LayerKind.Rectangle, LayerKind.Ellipse, LayerKind.Text, LayerKind.Solid, LayerKind.Null }) _properties.Children.Add(Button("New " + kind, () => Session.AddLayer(kind), kind == LayerKind.Text ? IconKind.Text : IconKind.Add));
            _properties.Children.Add(Heading("START ANIMATING")); _properties.Children.Add(new TextBlock { Text = "Select a layer in the viewer or timeline. Enable a stopwatch, move the playhead, then change a value to create keyframes.", FontFamily = Studio.Font, FontSize = 11, Foreground = Studio.Brush(Studio.Muted), TextWrapping = TextWrapping.Wrap }); return;
        }
        _properties.Children.Add(TextField(layer.Name, "Layer name", value => Session.Edit("Rename layer", () => layer.Name = value)));
        _properties.Children.Add(Heading("▾  Transform"));
        foreach (var (name, channel) in layer.Transform.Channels())
        {
            var minimum = name == "Opacity" ? 0 : -100000; var maximum = name == "Opacity" ? 100 : 100000;
            var field = Number("Transform " + name, CurveEvaluator.Evaluate(channel, Session.Time), value => { foreach (var l in Session.Selection.Where(l => !l.Locked)) EditorCommands.SetChannel(l.Transform.Get(name), Session.Time, value, Session.AutoKey); }, minimum, maximum, name is "Rotation" or "Opacity" ? .5 : 1);
            _transformFields[name] = field;
            var row = new Grid { MinHeight = 25 }; row.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(91) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var stopwatch = Button("Animate " + name, () => { Session.Property = name; Session.ToggleAnimation(name); }, IconKind.Stopwatch, false); stopwatch.Width = 23; stopwatch.Height = 23; stopwatch.Padding = new Thickness(3); stopwatch.Active = channel.Keys.Count > 0; row.Children.Add(stopwatch);
            var property = Button(TimelineView.PropertyName(name), () => { Session.Property = name; Timeline.ShowProperties("All"); Timeline.Invalidate(); }, null); property.Padding = new Thickness(1); property.Height = 23; Grid.SetColumn(property, 1); row.Children.Add(property); Grid.SetColumn(field, 2); row.Children.Add(field); _properties.Children.Add(row);
        }
        _properties.Children.Add(FieldRow("Blend Mode", Choice("Blend mode", Enum.GetValues<LayerBlend>().Select(v => (v.ToString(), v.ToString())), layer.Blend.ToString(), value => layer.Blend = Enum.Parse<LayerBlend>(value))));
        var parents = new[] { ("None", "") }.Concat(Session.Composition.Layers.Where(l => l.Id != layer.Id).Select(l => (l.Name, l.Id)));
        _properties.Children.Add(FieldRow("Parent", Choice("Parent layer", parents, layer.ParentId ?? "", value => layer.ParentId = value.Length == 0 ? null : value)));
        _properties.Children.Add(FieldRow("Track Matte", Choice("Track matte", parents, layer.MatteId ?? "", value => layer.MatteId = value.Length == 0 ? null : value)));
        if (layer.MatteId is not null) _properties.Children.Add(FieldRow("Matte Mode", Choice("Matte mode", Enum.GetValues<TrackMatte>().Select(v => (v.ToString(), v.ToString())), layer.Matte.ToString(), value => layer.Matte = Enum.Parse<TrackMatte>(value))));
        _properties.Children.Add(Heading("▾  Appearance"));
        _properties.Children.Add(FieldRow("Fill", TextField(layer.Fill, "Fill color", value => Session.Edit("Fill color", () => layer.Fill = value))));
        if (layer.Kind != LayerKind.Text)
        {
            _properties.Children.Add(FieldRow("Gradient End", TextField(layer.GradientEnd ?? "", "Gradient end", value => Session.Edit("Gradient fill", () => layer.GradientEnd = value.Length == 0 ? null : value))));
            _properties.Children.Add(FieldRow("Stroke", TextField(layer.Stroke, "Stroke color", value => Session.Edit("Stroke color", () => layer.Stroke = value))));
            _properties.Children.Add(FieldRow("Stroke Width", Number("Stroke width", layer.StrokeWidth, value => layer.StrokeWidth = value, 0, 4096, .5)));
            _properties.Children.Add(FieldRow("Width", Number("Layer width", layer.Width, value => layer.Width = value, 1, 32768)));
            _properties.Children.Add(FieldRow("Height", Number("Layer height", layer.Height, value => layer.Height = value, 1, 32768)));
            if (layer.Kind == LayerKind.Rectangle) _properties.Children.Add(FieldRow("Roundness", Number("Roundness", layer.CornerRadius, value => layer.CornerRadius = value, 0, 4096)));
            if (layer.Kind == LayerKind.Star) { _properties.Children.Add(FieldRow("Points", Number("Star points", layer.StarPoints, value => layer.StarPoints = (int)value, 3, 128))); _properties.Children.Add(FieldRow("Inner Radius", Number("Inner radius", layer.InnerRadius, value => layer.InnerRadius = value, .01, 1, .01))); }
            _properties.Children.Add(Button(layer.FillEnabled ? "Disable Fill" : "Enable Fill", () => Session.Edit("Toggle fill", () => layer.FillEnabled = !layer.FillEnabled)));
        }
        if (layer.Kind == LayerKind.Text)
        {
            _properties.Children.Add(Heading("▾  Character")); _properties.Children.Add(Studio.Text("Inter  ·  bundled open font", 10, Studio.Muted));
            _properties.Children.Add(FieldRow("Font Size", Number("Font size", layer.FontSize, value => layer.FontSize = value, 1, 4096)));
            _properties.Children.Add(TextField(layer.Text, "Text content", value => Session.Edit("Edit text", () => layer.Text = value), true));
        }
        _properties.Children.Add(Heading("▾  Animation"));
        _properties.Children.Add(Studio.Row(Button("Add key", () => Session.AddKey(Session.Property), IconKind.Keyframe), Button("Ease", () => Session.SetInterpolation(Interpolation.Bezier)), Button("Hold", () => Session.SetInterpolation(Interpolation.Hold))));
        _properties.Children.Add(FieldRow("Property", Choice("Expression property", layer.Transform.Channels().Select(p => (TimelineView.PropertyName(p.Name), p.Name)), Session.Property, value => { Session.Property = value; })));
        var expression = TextField(layer.Transform.Get(Session.Property).Expression, "Scalar expression", value =>
        {
            if (value.Length > 0 && value.Trim() != "loopOut()" && !ScalarExpression.TryEvaluate(value, Session.Time, 0, 1, out _, out var error)) throw new InvalidOperationException(error);
            Session.Edit("Set expression", () => layer.Transform.Get(Session.Property).Expression = value);
        }); expression.PlaceholderText = "value + sin(time * 2) * 30"; _properties.Children.Add(expression);
        _properties.Children.Add(Studio.Text("Scalar grammar · not JavaScript", 9, Studio.Muted));
        _properties.Children.Add(Studio.Row(Button("Fade", () => AnimatePreset("fade")), Button("Slide", () => AnimatePreset("position")), Button("Spin", () => AnimatePreset("spin"))));
    }
    private void UpdateValues()
    {
        var layer = Session.Primary; if (layer is null) return;
        foreach (var (name, field) in _transformFields) field.Value = CurveEvaluator.Evaluate(layer.Transform.Get(name), Session.Time, Session.Composition.Layers.IndexOf(layer) + 1);
    }
    private void RefreshEffects()
    {
        _effects.Children.Clear(); var layer = Session.Primary;
        _effects.Children.Add(Heading(layer?.Name ?? "No layer selected"));
        if (layer is null) { _effects.Children.Add(Studio.Text("Select a layer to edit its effects.", 10, Studio.Muted)); return; }
        _effects.Children.Add(Studio.Row(Button("Add Effect", () => _rightPanel.Select("Effects & Presets"), IconKind.Add), Button("Add Mask", () => Session.AddMask(), IconKind.Rectangle)));
        foreach (var effect in layer.Effects)
        {
            var definition = EffectCatalog.Get(effect.Kind); var group = new StackPanel { Spacing = 3, Margin = new Thickness(0, 4, 0, 5) };
            var toggle = Button(definition.Name, () => Session.Edit("Toggle effect", () => effect.Enabled = !effect.Enabled), IconKind.Eye); toggle.Active = effect.Enabled;
            group.Children.Add(Studio.Row(toggle, Button("Remove " + definition.Name, () => Session.Edit("Remove effect", () => layer.Effects.Remove(effect)), IconKind.Close, false)));
            foreach (var parameter in definition.Parameters)
            {
                if (!effect.Parameters.TryGetValue(parameter.Key, out var channel)) continue;
                var field = Number(definition.Name + " " + parameter.Name, CurveEvaluator.Evaluate(channel, Session.Time), value => EditorCommands.SetChannel(channel, Session.Time, value, Session.AutoKey), parameter.Minimum, parameter.Maximum, parameter.Step);
                group.Children.Add(FieldRow(parameter.Name, field, 115));
            }
            if (effect.Kind is EffectKind.Glow or EffectKind.DropShadow or EffectKind.Tint) group.Children.Add(FieldRow("Color", TextField(effect.Color, "Effect color", value => Session.Edit("Effect color", () => effect.Color = value))));
            var index = layer.Effects.IndexOf(effect);
            group.Children.Add(Studio.Row(Button("Move effect up", () => Session.Edit("Reorder effects", () => { var i = layer.Effects.IndexOf(effect); if (i > 0) { layer.Effects.RemoveAt(i); layer.Effects.Insert(i - 1, effect); } }), IconKind.Previous, false), Button("Move effect down", () => Session.Edit("Reorder effects", () => { var i = layer.Effects.IndexOf(effect); if (i < layer.Effects.Count - 1) { layer.Effects.RemoveAt(i); layer.Effects.Insert(i + 1, effect); } }), IconKind.Next, false), Studio.Text($"Effect {index + 1}", 9, Studio.Muted)));
            _effects.Children.Add(group); _effects.Children.Add(Studio.Separator(false));
        }
        if (layer.Effects.Count == 0) _effects.Children.Add(Studio.Text("No effects applied.", 10, Studio.Muted));
        _effects.Children.Add(Heading("▾  Masks"));
        foreach (var mask in layer.Masks)
        {
            _effects.Children.Add(Studio.Row(Button(mask.Name, () => Session.Edit("Toggle mask", () => mask.Enabled = !mask.Enabled), IconKind.Eye), Button("Remove mask", () => Session.Edit("Remove mask", () => layer.Masks.Remove(mask)), IconKind.Close, false)));
            _effects.Children.Add(FieldRow("Mode", Choice("Mask mode", Enum.GetValues<MaskMode>().Select(v => (v.ToString(), v.ToString())), mask.Mode.ToString(), value => mask.Mode = Enum.Parse<MaskMode>(value))));
            _effects.Children.Add(FieldRow("Feather", Number("Mask feather", mask.Feather, value => mask.Feather = value, 0, 512)));
            _effects.Children.Add(Button(mask.Inverted ? "Inverted ✓" : "Invert mask", () => Session.Edit("Invert mask", () => mask.Inverted = !mask.Inverted)));
        }
        if (layer.Masks.Count > 1) _effects.Children.Add(new TextBlock { Text = "This renderer feathers the combined mask using the largest feather radius.", FontFamily = Studio.Font, FontSize = 9, Foreground = Studio.Brush(Studio.Muted), TextWrapping = TextWrapping.Wrap });
    }
    private void RefreshCatalog()
    {
        _catalog.Children.Clear();
        _catalog.Children.Add(Studio.Text("Apply to the selected layer", 10, Studio.Muted));
        foreach (var group in EffectCatalog.All.Where(e => (e.Name + " " + e.Category).Contains(_effectFilter, StringComparison.OrdinalIgnoreCase)).GroupBy(e => e.Category))
        {
            _catalog.Children.Add(Heading("▾  " + group.Key));
            foreach (var definition in group) { var button = Button(definition.Name, () => ApplyEffect(definition.Kind), IconKind.Settings); button.HorizontalAlignment = HorizontalAlignment.Stretch; _catalog.Children.Add(button); }
        }
        _catalog.Children.Add(Heading("▾  Animation Presets"));
        _catalog.Children.Add(Button("Fade In / Out", () => AnimatePreset("fade"), IconKind.Keyframe)); _catalog.Children.Add(Button("Slide Up", () => AnimatePreset("position"), IconKind.Keyframe)); _catalog.Children.Add(Button("Spin 360°", () => AnimatePreset("spin"), IconKind.Keyframe));
    }
    private void RefreshQueue()
    {
        _queue.Children.Clear(); _queue.Children.Add(Studio.Row(Button("Render PNG Sequence", () => _ = RunAsync(ExportSequenceAsync), IconKind.Render), Button("Export Current Frame", () => _ = RunAsync(ExportFrameAsync), IconKind.Save), Button("Cancel Render", () => _renderCancellation?.Cancel(), IconKind.Close)));
        _queue.Children.Add(Studio.Text("Lossless PNG / sRGB / alpha · Work area · 256 MiB output limit", 10, Studio.Muted));
        foreach (var item in _renderItems.AsEnumerable().Reverse())
        {
            var row = new StackPanel { Spacing = 3, Margin = new Thickness(3, 7, 3, 5) }; row.Children.Add(Studio.Text(item.Name + "  /  " + item.Format, 12));
            row.Children.Add(Studio.Text($"{item.Status}   {item.Progress:P0}" + (item.Error is null ? "" : "  ·  " + item.Error), 10, item.Error is null ? Studio.Accent : "#E49A90"));
            _queue.Children.Add(row); _queue.Children.Add(Studio.Separator(false));
        }
        if (_renderItems.Count == 0) _queue.Children.Add(Studio.Text("The render queue is empty.", 11, Studio.Muted));
    }
}
