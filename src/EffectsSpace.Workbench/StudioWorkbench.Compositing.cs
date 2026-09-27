using EffectsSpace.Animation;
using EffectsSpace.Controls;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private readonly StackPanel _compositing = new() { Spacing = 4 };
    private NumericField? _sourceTimeField;

    private void InitializeCompositing() => _rightPanel.AddTab("Composite", Scroll(_compositing));
    private void NewAdjustment()
    {
        Pause(); Session.AddLayer(LayerKind.Adjustment); _rightPanel.Select("Composite");
        ShowStatus("Adjustment layer created · effects apply to the composite below it");
    }
    private void EditTime(string operation)
    {
        Pause();
        if (operation == "enable") Session.EnableTimeRemap();
        else if (operation == "freeze") Session.FreezeFrame();
        else Session.ReverseTime();
        Timeline.RevealProperty("TimeRemap"); _rightPanel.Select("Composite");
    }
    private void RefreshCompositing()
    {
        _compositing.Children.Clear(); _sourceTimeField = null;
        _compositing.Children.Add(Studio.Row(Button("New Adjustment", NewAdjustment, IconKind.Add),
            Button("Render Stats", ShowRenderStats, IconKind.Graph, false)));
        var layer = Session.Primary;
        if (layer is null)
        {
            _compositing.Children.Add(Heading("COMPOSITING"));
            _compositing.Children.Add(Note("Select a layer to edit its matte, guide status, source timing and individual masks. Adjustment layers apply effects to the composite beneath them."));
            return;
        }
        _compositing.Children.Add(Heading(layer.Name));
        var guide = Button("Guide Layer", () => Session.ToggleGuide(), IconKind.Guides); guide.Active = layer.Guide; guide.IsEnabled = !layer.Locked;
        _compositing.Children.Add(guide);
        _compositing.Children.Add(Note(layer.Guide ? "Guide: visible here; omitted from nested previews and exports." : "Render layer: included in previews and exports."));
        if (layer.Kind == LayerKind.Adjustment)
        {
            _compositing.Children.Add(Studio.Row(Button("Invert Composite", () => ApplyEffect(EffectKind.Invert)), Button("Exposure", () => ApplyEffect(EffectKind.Exposure))));
            _compositing.Children.Add(Note("Normal blend · opacity mixes original and filtered pixels. Masks and mattes restrict the adjustment region."));
        }
        var sources = new[] { ("None", "") }.Concat(Session.Composition.Layers.Where(l => l.Id != layer.Id && l.Kind != LayerKind.Adjustment).Select(l => (l.Name, l.Id)));
        _compositing.Children.Add(FieldRow("Matte Source", Choice("Composite matte source", sources, layer.MatteId ?? "", value => layer.MatteId = value.Length == 0 ? null : value)));
        _compositing.Children.Add(FieldRow("Matte Mode", Choice("Composite matte mode", Enum.GetValues<TrackMatte>().Select(v => (v.ToString(),v.ToString())), layer.Matte.ToString(), value => layer.Matte = Enum.Parse<TrackMatte>(value))));
        if (layer.Kind is LayerKind.Composition or LayerKind.Video or LayerKind.Audio)
        {
            _compositing.Children.Add(Heading("SOURCE TIME"));
            _compositing.Children.Add(Studio.Row(Button("Time Remap", () => EditTime("enable")), Button("Freeze", () => EditTime("freeze")), Button("Reverse", () => EditTime("reverse"))));
            if (layer.TimeRemapEnabled)
            {
                _sourceTimeField = Number("Source time (s)", LayerTime.Evaluate(layer, Session.Time), value => EditorCommands.SetChannel(layer.TimeRemap, Session.Time, value, Session.AutoKey), -86400, 86400, .01);
                _compositing.Children.Add(FieldRow("Source (s)", _sourceTimeField));
                _compositing.Children.Add(Studio.Row(Button("Remap Graph", () => { Timeline.RevealProperty("TimeRemap"); Timeline.GraphMode = true; }),
                    Button("Disable Remap", () => EditLayer(layer, "Disable Time Remap", () => layer.TimeRemapEnabled = false))));
            }
            _compositing.Children.Add(Note("Values are source seconds. Out-of-range source times are transparent. Freeze preserves the displayed source frame."));
        }
        _compositing.Children.Add(Heading("MASKS"));
        _compositing.Children.Add(Studio.Row(Button("Add Mask", () => Session.AddMask(), IconKind.Add),
            Button("Finish Mask Edit", () => MaskEditor.Stop(), IconKind.Select, false)));
        foreach (var mask in layer.Masks)
        {
            _compositing.Children.Add(Studio.Row(Button(mask.Name + " · Edit Path", () =>
            { Pause(); MaskEditor.Start(layer.Id, mask.Id); ShowStatus("Mask path · drag nodes; Alt-drag a node to create tangents; Enter finishes"); }, IconKind.Pen),
                Button("Delete " + mask.Name, () => EditLayer(layer, "Delete mask", () => layer.Masks.Remove(mask)), IconKind.Delete, false)));
            _compositing.Children.Add(FieldRow("Operation", Choice(mask.Name + " operation", Enum.GetValues<MaskMode>().Select(v => (v.ToString(),v.ToString())), mask.Mode.ToString(), value => mask.Mode = Enum.Parse<MaskMode>(value))));
            _compositing.Children.Add(FieldRow("Opacity", Number(mask.Name + " opacity", mask.Opacity, value => mask.Opacity = value, 0, 100, .5)));
            _compositing.Children.Add(FieldRow("Feather (px)", Number(mask.Name + " feather", mask.Feather, value => mask.Feather = value, 0, 512, .5)));
            _compositing.Children.Add(FieldRow("Expand (px)", Number(mask.Name + " expansion", mask.Expansion, value => mask.Expansion = value, -512, 512, .5)));
            _compositing.Children.Add(Studio.Row(Button(mask.Inverted ? "Inverted ✓" : "Invert", () => EditLayer(layer, "Invert mask", () => mask.Inverted = !mask.Inverted)),
                Button("Mask Up", () => MoveMask(layer, mask, -1), IconKind.Previous, false),
                Button("Mask Down", () => MoveMask(layer, mask, 1), IconKind.Next, false)));
        }
        _compositing.Children.Add(Note("Each mask is expanded and feathered independently before its coverage is combined. Expansion uses raster dilation/erosion."));
    }
    private static TextBlock Note(string text) => new() { Text = text, FontFamily = Studio.Font, FontSize = 10, Foreground = Studio.Brush(Studio.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,4) };
    private void EditLayer(Layer layer, string name, Action change)
    {
        if (layer.Locked) throw new InvalidOperationException("The layer is locked.");
        Session.Edit(name, change);
    }
    private void MoveMask(Layer layer, LayerMask mask, int direction)
    {
        if (layer.Locked) throw new InvalidOperationException("The layer is locked.");
        Session.Edit("Reorder mask", () => { var index = layer.Masks.IndexOf(mask); var next = index + direction; if (next < 0 || next >= layer.Masks.Count) return; layer.Masks.RemoveAt(index); layer.Masks.Insert(next, mask); });
    }
    private void ShowRenderStats()
    {
        var m = Viewer.Renderer.Metrics;
        ShowStatus($"CPU submission {m.SubmissionMilliseconds:0.00} ms · draws {m.LayerDraws} · direct {m.DirectDraws} · culled {m.CulledLayers} · isolation {m.IsolationLayers} · transforms {m.TransformEvaluations} · native cache {Viewer.Renderer.CachedLayerResources}");
    }
    private void UpdateSourceTime()
    {
        if (_sourceTimeField is not null && Session.Primary is { TimeRemapEnabled: true } layer)
            _sourceTimeField.Value = LayerTime.Evaluate(layer, Session.Time, Session.Composition.Layers.IndexOf(layer) + 1);
    }
}
