namespace EffectsSpace.Documents;

public sealed record ImportedFile(string Name, string MimeType, byte[] Data, int Width = 0, int Height = 0, double Duration = 0);

public interface IWorkspaceStorage
{
    Task<string?> ReadRecoveryAsync();
    Task WriteRecoveryAsync(string json);
    Task<IReadOnlyList<ImportedFile>> PickFilesAsync(bool projectOnly = false);
    Task SaveFileAsync(string name, string mimeType, byte[] data);
}
