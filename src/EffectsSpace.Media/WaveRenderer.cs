namespace EffectsSpace.Media;

/// <summary>Bounded, cancellable stereo WAVE delivery from a prepared mixer.</summary>
public static class WaveRenderer
{
    public static async Task<byte[]> RenderAsync(AudioMixer mixer, double start, double duration, WaveEncoding encoding = WaveEncoding.Pcm16,
        int sampleRate = 48000, bool dither = false, long maximumBytes = 256L * 1024 * 1024,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        if (!double.IsFinite(start) || !double.IsFinite(duration) || start < 0 || duration <= 0 || sampleRate is < 8000 or > 192000)
            throw new ArgumentOutOfRangeException(nameof(duration));
        long total = checked((long)Math.Floor(duration * sampleRate + 1e-8));
        long bytes = WavePcmWriter.FileSize(total, 2, encoding);
        if (bytes > maximumBytes || bytes > int.MaxValue) throw new InvalidOperationException("WAVE output exceeds the configured byte budget.");
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream((int)bytes);
        var writer = new WavePcmWriter(stream, sampleRate, 2, total, encoding, dither);
        var block = new float[4096 * 2];
        for (long frame = 0; frame < total; frame += 4096)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(4096, total - frame);
            mixer.Mix(start, frame, block.AsSpan(0, count * 2), sampleRate);
            writer.Write(block.AsSpan(0, count * 2));
            progress?.Report((frame + count) / (double)total);
            await Task.Yield();
        }
        cancellationToken.ThrowIfCancellationRequested();
        writer.Complete();
        return stream.ToArray();
    }
}
