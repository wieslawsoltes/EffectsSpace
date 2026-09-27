using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using EffectsSpace.Animation;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Workbench;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace EffectsSpace.App;

internal sealed class BrowserWorkspaceStorage : IWorkspaceStorage
{
    public async Task<string?> ReadRecoveryAsync() => await BrowserFiles.Load();
    public async Task WriteRecoveryAsync(string json) => await BrowserFiles.Save(json);
    public async Task<IReadOnlyList<ImportedFile>> PickFilesAsync(bool projectOnly = false)
    {
        var response = await BrowserFiles.Open(projectOnly); if (string.IsNullOrEmpty(response)) return [];
        using var json = JsonDocument.Parse(response); var result = new List<ImportedFile>();
        foreach (var file in json.RootElement.EnumerateArray()) result.Add(new(file.GetProperty("name").GetString()!, file.GetProperty("type").GetString()!, Convert.FromBase64String(file.GetProperty("data").GetString()!)));
        return result;
    }
    public async Task SaveFileAsync(string name, string mimeType, byte[] data) => await BrowserFiles.Download(name, Convert.ToBase64String(data), mimeType);
}
internal static partial class BrowserFiles
{
    [JSImport("globalThis.effectsSpaceStorage.load")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Load();
    [JSImport("globalThis.effectsSpaceStorage.save")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Save(string document);
    [JSImport("globalThis.effectsSpaceStorage.open")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Open(bool projectOnly);
    [JSImport("globalThis.effectsSpaceStorage.download")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Download(string name, string base64, string contentType);
    [JSImport("globalThis.effectsSpaceStorage.isTestMode")]
    internal static partial bool IsTestMode();
    [JSImport("globalThis.effectsSpaceStorage.publishDiagnostics")]
    internal static partial void PublishDiagnostics(string json);
}
internal static class BrowserDiagnostics
{
    // Observation only. Browser tests operate through actual pointer/keyboard/file-picker input.
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
                    var l = session.Primary; var viewer = workbench.Viewer; var timeline = workbench.Timeline;
                    json.WriteStartObject(); json.WriteBoolean("ready", workbench.IsLoaded); json.WriteString("runtime", "Uno WebAssembly / C# / Skia"); json.WriteString("project", session.Project.Name); json.WriteString("composition", session.Composition.Name);
                    json.WriteNumber("layers", session.Composition.Layers.Count); json.WriteNumber("compositions", session.Project.Compositions.Count); json.WriteNumber("assets", session.Project.Assets.Count);
                    json.WriteNumber("time", session.Time); json.WriteNumber("revision", session.Revision); json.WriteNumber("selection", session.SelectedIds.Count); json.WriteBoolean("canUndo", session.CanUndo); json.WriteBoolean("canRedo", session.CanRedo); json.WriteBoolean("playing", workbench.IsPlaying); json.WriteBoolean("graph", timeline.GraphMode);
                    json.WriteString("tool", viewer.Tool.ToString()); json.WriteString("name", l?.Name); json.WriteString("kind", l?.Kind.ToString()); json.WriteString("text", l?.Text); json.WriteNumber("effects", l?.Effects.Count ?? 0); json.WriteNumber("masks", l?.Masks.Count ?? 0); json.WriteNumber("keys", l?.Transform.Channels().Sum(p => p.Channel.Keys.Count) ?? 0);
                    json.WriteNumber("x", l is null ? 0 : CurveEvaluator.Evaluate(l.Transform.X, session.Time)); json.WriteNumber("y", l is null ? 0 : CurveEvaluator.Evaluate(l.Transform.Y, session.Time)); json.WriteNumber("width", l?.Width ?? 0); json.WriteNumber("height", l?.Height ?? 0); json.WriteString("status", workbench.Status); json.WriteString("renderError", viewer.LastRenderError);
                    json.WriteNumber("zoom", viewer.Zoom); json.WriteNumber("panX", viewer.Pan.X); json.WriteNumber("panY", viewer.Pan.Y); json.WriteNumber("renderMs", viewer.LastRenderMilliseconds);
                    void Bounds(string name, FrameworkElement element)
                    {
                        var origin = element.TransformToVisual(null).TransformPoint(new Point(0, 0)); json.WritePropertyName(name); json.WriteStartObject(); json.WriteNumber("x", origin.X); json.WriteNumber("y", origin.Y); json.WriteNumber("width", element.ActualWidth); json.WriteNumber("height", element.ActualHeight); json.WriteEndObject();
                    }
                    Bounds("viewer", viewer); Bounds("timeline", timeline);
                    json.WriteNumber("headerWidth", timeline.HeaderWidth); json.WriteNumber("pixelsPerSecond", timeline.PixelsPerSecond); json.WriteNumber("scrollSeconds", timeline.ScrollSeconds); json.WriteNumber("scrollY", timeline.ScrollY);
                    json.WriteStartArray("rows"); foreach (var row in timeline.Rows.Take(80)) { json.WriteStartObject(); json.WriteString("id", row.Layer.Id); json.WriteString("name", row.Layer.Name); json.WriteString("property", row.Property); json.WriteNumber("y", row.Y - timeline.ScrollY); json.WriteNumber("height", row.Height); json.WriteEndObject(); } json.WriteEndArray();
                    json.WriteEndObject();
                }
                BrowserFiles.PublishDiagnostics(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
            }
            catch (Exception ex) { Console.WriteLine("Diagnostics not ready: " + ex.Message); }
        }
        session.Changed += _ => Publish(); workbench.DiagnosticsChanged += Publish; workbench.Loaded += (_, _) => Publish(); workbench.SizeChanged += (_, _) => Publish();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) }; timer.Tick += (_, _) => Publish(); timer.Start(); workbench.Unloaded += (_, _) => timer.Stop(); Publish();
    }
}
