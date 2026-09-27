namespace EffectsSpace.Core;

/// <summary>Embedded user-owned media. Never interpreted as a URL or executable source.</summary>
public sealed class MediaAsset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Asset";
    public string MimeType { get; set; } = "image/png";
    public byte[] Data { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
}
