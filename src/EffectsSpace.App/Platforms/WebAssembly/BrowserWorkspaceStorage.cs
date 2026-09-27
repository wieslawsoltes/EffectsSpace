using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using EffectsSpace.Documents;

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
