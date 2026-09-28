using EffectsSpace.Documents;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace EffectsSpace.App;

internal sealed class DesktopWorkspaceStorage : IWorkspaceStorage
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EffectsSpace");
    private static string RecoveryPath => Path.Combine(DirectoryPath, "workspace.effects");
    public async Task<string?> ReadRecoveryAsync()
    {
        if (!File.Exists(RecoveryPath)) return null;
        if (new FileInfo(RecoveryPath).Length > ProjectJson.MaximumCharacters) throw new InvalidDataException("Recovery file exceeds the size limit.");
        return await File.ReadAllTextAsync(RecoveryPath);
    }
    public async Task WriteRecoveryAsync(string json)
    {
        Directory.CreateDirectory(DirectoryPath); var temporary = RecoveryPath + ".tmp";
        await File.WriteAllTextAsync(temporary, json); File.Move(temporary, RecoveryPath, true);
    }
    public async Task<IReadOnlyList<ImportedFile>> PickFilesAsync(bool projectOnly = false)
    {
        var picker = new FileOpenPicker(); foreach (var extension in projectOnly ? new[] { ".effects", ".json" } : new[] { ".png", ".jpg", ".jpeg", ".webp", ".avi", ".wav", ".effects", ".json" }) picker.FileTypeFilter.Add(extension);
        var files = await picker.PickMultipleFilesAsync(); var result = new List<ImportedFile>();
        foreach (var file in files)
        {
            var properties = await file.GetBasicPropertiesAsync(); var isProject = file.FileType is ".effects" or ".json";
            if (properties.Size > (ulong)(isProject ? ProjectJson.MaximumCharacters : 32 * 1024 * 1024)) throw new InvalidDataException("The file exceeds the import size limit.");
            using var stream = await file.OpenStreamForReadAsync(); using var data = new MemoryStream(); await stream.CopyToAsync(data);
            var mime = file.FileType.ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".avi" => "video/x-msvideo", ".wav" => "audio/wav", _ => "application/json" };
            result.Add(new(file.Name, mime, data.ToArray()));
        }
        return result;
    }
    public async Task SaveFileAsync(string name, string mimeType, byte[] data)
    {
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(name) }; picker.FileTypeChoices.Add(mimeType, [Path.GetExtension(name)]);
        var file = await picker.PickSaveFileAsync(); if (file is null) throw new OperationCanceledException("Save cancelled."); await FileIO.WriteBytesAsync(file, data);
    }
}
