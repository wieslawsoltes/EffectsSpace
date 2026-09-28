using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Zero-copy indexed PCM. The supplied memories must remain immutable for this source's lifetime.</summary>
public sealed class PcmSource
{
    private readonly ReadOnlyMemory<byte>[] _segments;
    private readonly long[] _ends;
    public PcmFormat Format { get; }
    public long FrameCount { get; }
    public double StartTime { get; }
    public double Duration => FrameCount / (double)Format.SampleRate;

    public PcmSource(PcmFormat format, IEnumerable<ReadOnlyMemory<byte>> segments, double startTime = 0)
    {
        format.Validate();
        if (!double.IsFinite(startTime) || startTime < 0) throw new ArgumentOutOfRangeException(nameof(startTime));
        Format = format; StartTime = startTime;
        _segments = segments.Where(s => s.Length > 0).ToArray();
        if (_segments.Length > 100000) throw new InvalidDataException("Too many PCM segments.");
        _ends = new long[_segments.Length];
        long frames = 0;
        for (var i = 0; i < _segments.Length; i++)
        {
            if (_segments[i].Length % format.BlockAlign != 0) throw new InvalidDataException("PCM ends in a partial sample frame.");
            frames = checked(frames + _segments[i].Length / format.BlockAlign); _ends[i] = frames;
        }
        FrameCount = frames;
    }

    public float ReadFrame(long frame, int channel)
    {
        if (channel < 0 || channel > 1) throw new ArgumentOutOfRangeException(nameof(channel));
        if (frame < 0 || frame >= FrameCount) return 0;
        var low = 0; var high = _ends.Length - 1;
        while (low < high) { var middle = (low + high) / 2; if (frame < _ends[middle]) high = middle; else low = middle + 1; }
        var local = frame - (low == 0 ? 0 : _ends[low - 1]);
        var offset = checked((int)(local * Format.BlockAlign + Math.Min(channel, Format.Channels - 1) * (Format.BitsPerSample / 8)));
        var span = _segments[low].Span[offset..];
        double sample;
        if (Format.FloatingPoint)
            sample = Format.BitsPerSample == 32 ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span)) : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(span));
        else sample = Format.BitsPerSample switch
        {
            8 => (span[0] - 128) / 128d,
            16 => BinaryPrimitives.ReadInt16LittleEndian(span) / 32768d,
            24 => ((span[0] | span[1] << 8 | span[2] << 16) << 8 >> 8) / 8388608d,
            32 => BinaryPrimitives.ReadInt32LittleEndian(span) / 2147483648d,
            _ => throw new InvalidOperationException("Unsupported PCM format.")
        };
        return double.IsFinite(sample) ? (float)Math.Clamp(sample, -16, 16) : 0;
    }

    /// <summary>Linear sample-rate/time-stretch interpolation; no pitch preservation or mastering-grade antialias filter.</summary>
    public float Sample(double sourceTime, int channel)
    {
        var position = (sourceTime - StartTime) * Format.SampleRate;
        if (!double.IsFinite(position) || position < 0 || position >= FrameCount) return 0;
        var frame = (long)Math.Floor(position); var fraction = (float)(position - frame);
        var a = ReadFrame(frame, channel);
        return a + (ReadFrame(Math.Min(frame + 1, FrameCount - 1), channel) - a) * fraction;
    }
}
