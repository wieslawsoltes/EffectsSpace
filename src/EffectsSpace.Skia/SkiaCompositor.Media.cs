using EffectsSpace.Core;
using EffectsSpace.Media;
using EffectsSpace.Rendering;
using SkiaSharp;

namespace EffectsSpace.Skia;

public sealed partial class SkiaCompositor
{
    private sealed class CachedImage(SKImage image, long bytes, long stamp, byte[]? payload)
    {
        public SKImage Image { get; } = image;
        public long Bytes { get; } = bytes;
        public long Stamp { get; set; } = stamp;
        public byte[]? Payload { get; } = payload;
    }
    private readonly Dictionary<(string Asset, int Frame), CachedImage> _images = new();
    private long _stamp, _bytes;
    private readonly MediaCatalog _media = new();
    public long MediaIndexBuilds => _media.ParseCount;
    public long VideoImageCreations { get; private set; }
    public long VideoFrameCacheHits { get; private set; }
    private SKImage? GetImage(MediaAsset asset)
    {
        if (_images.TryGetValue((asset.Id, -1), out var cached) && ReferenceEquals(cached.Payload, asset.Data))
        { cached.Stamp = ++_stamp; return cached.Image; }
        RemoveAssetImages(asset.Id);
        var image = DecodeImage(asset.Data); PutImage((asset.Id, -1), image, asset.Data); return image;
    }
    private SKImage? GetVideoImage(MediaAsset asset, double sourceTime)
    {
        if (!MediaCatalog.SupportsVideo(asset.MimeType) && FindImage((asset.Id, -2)) is { } external) return external;
        var parses = _media.ParseCount;
        var video = _media.Get(asset).Video ?? throw new InvalidDataException("Asset has no video stream.");
        if (_media.ParseCount != parses) RemoveAssetImages(asset.Id);
        var index = video.FrameAt(sourceTime);
        if (index < 0) return null;
        var key = (asset.Id, index);
        if (_images.TryGetValue(key, out var cached) && ReferenceEquals(cached.Payload, asset.Data))
        { cached.Stamp = ++_stamp; VideoFrameCacheHits++; return cached.Image; }
        var bytes = video.FrameData(index);
        using var data = SKData.CreateCopy(bytes.Span);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Motion JPEG frame cannot be decoded.");
        Budget.Check(codec.Info.Width, codec.Info.Height);
        if (codec.Info.Width != video.Width || codec.Info.Height != video.Height)
            throw new InvalidDataException("Motion JPEG dimensions disagree with AVI stream metadata.");
        var image = SKImage.FromEncodedData(data) ?? throw new InvalidDataException("Could not create the video frame image.");
        PutImage(key, image, asset.Data); VideoImageCreations++; return image;
    }
    private SKImage? FindImage((string Asset, int Frame) key)
    {
        if (!_images.TryGetValue(key, out var item)) return null;
        item.Stamp = ++_stamp; return item.Image;
    }
    private void PutImage((string Asset, int Frame) key, SKImage image, byte[]? payload = null)
    {
        if (_images.Remove(key, out var old)) { _bytes -= old.Bytes; old.Image.Dispose(); }
        var bytes = (long)image.Width * image.Height * 4;
        while (_images.Count > 0 && (_bytes + bytes > ImageCacheLimit || _images.Count >= 256))
        { var oldest = _images.MinBy(p => p.Value.Stamp); _images.Remove(oldest.Key); _bytes -= oldest.Value.Bytes; oldest.Value.Image.Dispose(); }
        _images[key] = new(image, bytes, ++_stamp, payload); _bytes += bytes;
    }
    private void RemoveAssetImages(string assetId)
    {
        // Only a source/cache miss scans the bounded cache; unchanged frame hits stay O(1).
        foreach (var key in _images.Keys.Where(k => k.Asset == assetId).ToArray())
        { var old = _images[key]; _images.Remove(key); _bytes -= old.Bytes; old.Image.Dispose(); }
    }
    /// <summary>Supply a current frame for an external decoder. Portable AVI uses its own timestamp-indexed frames.</summary>
    public void SetVideoFrame(string assetId, byte[] png) => PutImage((assetId, -2), DecodeImage(png));
    public SKImage DecodeImage(byte[] data)
    {
        var (w, h) = ImageInfo(data); Budget.Check(w, h);
        return SKImage.FromEncodedData(data) ?? throw new InvalidDataException("The image could not be decoded.");
    }
    public static (int Width, int Height) ImageInfo(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("Image must be between 1 byte and 32 MiB.");
        using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
        if (codec is null) throw new InvalidDataException("Unsupported or corrupt image.");
        RenderBudget.Default.Check(codec.Info.Width, codec.Info.Height); return (codec.Info.Width, codec.Info.Height);
    }
    public void ClearImages() { foreach (var i in _images.Values) i.Image.Dispose(); _images.Clear(); _bytes = 0; }
    public void ClearResources() { ClearImages(); _resources.Clear(); _media.Clear(); }
}
