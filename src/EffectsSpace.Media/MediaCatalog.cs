using EffectsSpace.Core;

namespace EffectsSpace.Media;

public sealed record MediaContent(AviSource? Video, PcmSource? Audio)
{
    public double Duration => Math.Max(Video?.Duration ?? 0, Audio is null ? 0 : Audio.StartTime + Audio.Duration);
}

/// <summary>Bounded metadata cache. Encoded media stays in the project-owned immutable buffer; it is never duplicated here.</summary>
public sealed class MediaCatalog
{
    private sealed record Entry(byte[] Payload, string MimeType, MediaContent Content)
    { public long Stamp { get; set; } }
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _clock;
    public int Capacity { get; init; } = 32;
    public long ParseCount { get; private set; }
    public long CacheHits { get; private set; }
    public int Count => _entries.Count;

    public static bool SupportsVideo(string mimeType) => mimeType == "video/x-msvideo";
    public static bool SupportsAudio(string mimeType) => mimeType is "audio/wav" or "video/x-msvideo";

    public MediaContent Get(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (_entries.TryGetValue(asset.Id, out var entry) && ReferenceEquals(entry.Payload, asset.Data) && entry.MimeType == asset.MimeType)
        { entry.Stamp = ++_clock; CacheHits++; return entry.Content; }
        MediaContent source;
        if (asset.MimeType == "video/x-msvideo") { var video = AviSource.Read(asset.Data); source = new(video, video.Audio); }
        else if (asset.MimeType == "audio/wav") source = new(null, WaveFile.Read(asset.Data));
        else throw new NotSupportedException($"Media codec/container '{asset.MimeType}' is not supported. Use Motion JPEG AVI or PCM WAVE.");
        while (_entries.Count >= Math.Max(1, Capacity)) _entries.Remove(_entries.MinBy(p => p.Value.Stamp).Key);
        _entries[asset.Id] = new(asset.Data, asset.MimeType, source) { Stamp = ++_clock }; ParseCount++; return source;
    }

    public void Clear() => _entries.Clear();
}
