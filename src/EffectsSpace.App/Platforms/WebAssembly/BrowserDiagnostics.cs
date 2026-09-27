using System.Text;
using System.Text.Json;
using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Workbench;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EffectsSpace.App;

/// <summary>Opt-in observation only. Tests use genuine pointer/keyboard/file-picker events, never a mutation endpoint.</summary>
internal static class BrowserDiagnostics
{
    public static void Attach(EditorSession session, StudioWorkbench workbench)
    {
        if (!BrowserFiles.IsTestMode()) return;
        void Publish()
        {
            try
            {
                using var stream = new MemoryStream();
                using (var json = new Utf8JsonWriter(stream))
                {
                    var layer = session.Primary; var viewer = workbench.Viewer; var timeline = workbench.Timeline;
                    json.WriteStartObject(); json.WriteBoolean("ready", workbench.IsLoaded);
                    json.WriteString("runtime", "Uno WebAssembly / C# / Skia"); json.WriteString("project", session.Project.Name); json.WriteString("composition", session.Composition.Name);
                    json.WriteNumber("layers", session.Composition.Layers.Count); json.WriteNumber("compositions", session.Project.Compositions.Count); json.WriteNumber("assets", session.Project.Assets.Count);
                    json.WriteNumber("time", session.Time); json.WriteNumber("revision", session.Revision); json.WriteNumber("selection", session.SelectedIds.Count);
                    json.WriteBoolean("canUndo", session.CanUndo); json.WriteBoolean("canRedo", session.CanRedo); json.WriteBoolean("playing", workbench.IsPlaying); json.WriteBoolean("editing", session.IsEditing);
                    json.WriteBoolean("graph", timeline.GraphMode); json.WriteString("graphKind", timeline.GraphKind.ToString()); json.WriteString("property", session.Property);
                    json.WriteString("tool", viewer.Tool.ToString()); json.WriteString("name", layer?.Name); json.WriteString("kind", layer?.Kind.ToString()); json.WriteString("text", layer?.Text);
                    json.WriteNumber("effects", layer?.Effects.Count ?? 0); json.WriteNumber("masks", layer?.Masks.Count ?? 0); json.WriteNumber("keys", layer is null ? 0 : LayerChannels.Enumerate(layer).Sum(p => p.Channel.Keys.Count));
                    json.WriteNumber("selectedKeys", session.SelectedKeyIds.Count); json.WriteNumber("clipboardKeys", session.KeyClipboard?.KeyCount ?? 0);
                    json.WriteNumber("x", layer is null ? 0 : CurveEvaluator.Evaluate(layer.Transform.X, session.Time)); json.WriteNumber("y", layer is null ? 0 : CurveEvaluator.Evaluate(layer.Transform.Y, session.Time));
                    json.WriteNumber("width", layer?.Width ?? 0); json.WriteNumber("height", layer?.Height ?? 0); json.WriteString("status", workbench.Status); json.WriteString("renderError", viewer.LastRenderError);
                    json.WriteNumber("zoom", viewer.Zoom); json.WriteNumber("panX", viewer.Pan.X); json.WriteNumber("panY", viewer.Pan.Y); json.WriteNumber("renderMs", viewer.LastRenderMilliseconds);
                    WriteBounds(json, "viewer", viewer); WriteBounds(json, "timeline", timeline);
                    json.WriteNumber("headerWidth", timeline.HeaderWidth); json.WriteNumber("pixelsPerSecond", timeline.PixelsPerSecond); json.WriteNumber("scrollSeconds", timeline.ScrollSeconds); json.WriteNumber("scrollY", timeline.ScrollY);
                    json.WriteStartArray("rows");
                    foreach (var row in timeline.Rows.Take(100))
                    { json.WriteStartObject(); json.WriteString("id", row.Layer.Id); json.WriteString("name", row.Layer.Name); json.WriteString("property", row.Property); json.WriteString("displayName", row.Descriptor?.Name); json.WriteNumber("y", row.Y - timeline.ScrollY); json.WriteNumber("height", row.Height); json.WriteEndObject(); }
                    json.WriteEndArray();
                    json.WriteStartArray("keyframes");
                    foreach (var selected in session.Selection)
                    foreach (var property in LayerChannels.Enumerate(selected))
                    foreach (var key in property.Channel.Keys.Take(4096))
                    {
                        json.WriteStartObject(); json.WriteString("id", key.Id); json.WriteString("layer", selected.Id); json.WriteString("property", property.Path); json.WriteString("name", property.Name);
                        json.WriteNumber("time", key.Time); json.WriteNumber("value", key.Value); json.WriteString("interpolation", key.Interpolation.ToString()); json.WriteBoolean("selected", session.SelectedKeyIds.Contains(key.Id));
                        json.WriteNumber("x1", key.X1); json.WriteNumber("y1", key.Y1); json.WriteNumber("x2", key.X2); json.WriteNumber("y2", key.Y2); json.WriteEndObject();
                    }
                    json.WriteEndArray();
                    json.WriteStartArray("keyPositions");
                    foreach (var key in timeline.VisibleKeyPositions().Take(4096))
                    { json.WriteStartObject(); json.WriteString("id", key.Id); json.WriteNumber("x", key.X); json.WriteNumber("y", key.Y); json.WriteEndObject(); }
                    json.WriteEndArray();
                    json.WriteStartArray("handlePositions");
                    foreach (var handle in timeline.VisibleHandlePositions().Take(1024))
                    { json.WriteStartObject(); json.WriteString("leftId", handle.LeftId); json.WriteString("rightId", handle.RightId); json.WriteBoolean("outgoing", handle.Outgoing); json.WriteNumber("x", handle.X); json.WriteNumber("y", handle.Y); json.WriteEndObject(); }
                    json.WriteEndArray();
                    json.WriteNumber("graphMinimum", timeline.GraphMinimum); json.WriteNumber("graphMaximum", timeline.GraphMaximum);
                    json.WriteStartArray("controls");
                    foreach (var element in Descendants(workbench).OfType<Control>().Where(c => c.IsLoaded && c.ActualWidth > 0 && c.ActualHeight > 0).Take(1000))
                    {
                        var name = AutomationProperties.GetName(element); if (string.IsNullOrEmpty(name)) continue;
                        var origin = element.TransformToVisual(null).TransformPoint(new Point());
                        if (origin.X < 0 || origin.Y < 0 || origin.X >= workbench.ActualWidth || origin.Y >= workbench.ActualHeight) continue;
                        json.WriteStartObject(); json.WriteString("name", name); json.WriteString("type", element.GetType().Name); json.WriteBoolean("enabled", element.IsEnabled);
                        json.WriteNumber("x", origin.X); json.WriteNumber("y", origin.Y); json.WriteNumber("width", element.ActualWidth); json.WriteNumber("height", element.ActualHeight); json.WriteEndObject();
                    }
                    json.WriteEndArray(); json.WriteEndObject();
                }
                BrowserFiles.PublishDiagnostics(Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (Exception ex) { Console.WriteLine("Diagnostics not ready: " + ex.Message); }
        }
        session.Changed += _ => Publish(); workbench.DiagnosticsChanged += Publish; workbench.Loaded += (_, _) => Publish(); workbench.SizeChanged += (_, _) => Publish();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; timer.Tick += (_, _) => Publish(); timer.Start(); workbench.Unloaded += (_, _) => timer.Stop(); Publish();
    }
    private static void WriteBounds(Utf8JsonWriter json, string name, FrameworkElement element)
    {
        var origin = element.TransformToVisual(null).TransformPoint(new Point());
        json.WritePropertyName(name); json.WriteStartObject(); json.WriteNumber("x", origin.X); json.WriteNumber("y", origin.Y); json.WriteNumber("width", element.ActualWidth); json.WriteNumber("height", element.ActualHeight); json.WriteEndObject();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root); var visited = 0;
        while (pending.TryPop(out var item) && visited++ < 10000)
        {
            yield return item;
            for (var i = VisualTreeHelper.GetChildrenCount(item) - 1; i >= 0; i--) pending.Push(VisualTreeHelper.GetChild(item, i));
        }
    }
}
