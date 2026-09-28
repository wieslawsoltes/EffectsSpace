using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Immutable, zero-copy PCM index. Reader buffers are caller-owned; this source contains no shared cursor.</summary>
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
        ArgumentNullException.ThrowIfNull(segments);
        format.Validate();
        if (!double.IsFinite(startTime) || startTime < 0) throw new ArgumentOutOfRangeException(nameof(startTime));
        Format = format;
        StartTime = startTime;
        _segments = segments.Where(s => s.Length > 0).ToArray();
        if (_segments.Length > 100000) throw new InvalidDataException("Too many PCM segments.");
        _ends = new long[_segments.Length];
        long frames = 0;
        for (var i = 0; i < _segments.Length; i++)
        {
            if (_segments[i].Length % format.BlockAlign != 0) throw new InvalidDataException("PCM ends in a partial sample frame.");
            frames = checked(frames + _segments[i].Length / format.BlockAlign);
            _ends[i] = frames;
        }
        FrameCount = frames;
    }

    private int FindSegment(long frame)
    {
        var low = 0;
        var high = _ends.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (frame < _ends[middle]) high = middle;
            else low = middle + 1;
        }
        return low;
    }

    private float DecodeSample(ReadOnlySpan<byte> bytes)
    {
        double sample;
        if (Format.FloatingPoint)
        {
            sample = Format.BitsPerSample == 32
                ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes))
                : BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes));
        }
        else
        {
            int raw = Format.BitsPerSample switch
            {
                8 => bytes[0] - 128,
                16 => BinaryPrimitives.ReadInt16LittleEndian(bytes),
                24 => ((bytes[0] | bytes[1] << 8 | bytes[2] << 16) << 8) >> 8,
                32 => BinaryPrimitives.ReadInt32LittleEndian(bytes),
                _ => throw new InvalidOperationException("Unsupported PCM format.")
            };
            // Ignore unused low bits, including malformed nonzero padding, without sign loss.
            raw >>= Format.BitsPerSample - Format.SamplePrecision;
            sample = raw / (double)(1L << (Format.SamplePrecision - 1));
        }
        return double.IsFinite(sample) ? (float)Math.Clamp(sample, -16, 16) : 0;
    }

    public float ReadFrame(long frame, int channel)
    {
        if (channel is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(channel));
        if (frame < 0 || frame >= FrameCount) return 0;
        int segment = FindSegment(frame);
        long local = frame - (segment == 0 ? 0 : _ends[segment - 1]);
        int offset = checked((int)(local * Format.BlockAlign + Math.Min(channel, Format.Channels - 1) * (Format.BitsPerSample / 8)));
        return DecodeSample(_segments[segment].Span[offset..]);
    }

    /// <summary>Decode consecutive stereo frames with one index search; duplicate mono and zero-pad outside the source.</summary>
    public void ReadStereoFrames(long firstFrame, Span<float> destination)
    {
        if (destination.Length % 2 != 0) throw new ArgumentException("Destination must contain complete stereo frames.", nameof(destination));
        int count = destination.Length / 2;
        if (firstFrame > long.MaxValue - count) throw new ArgumentOutOfRangeException(nameof(firstFrame));
        destination.Clear();
        if (count == 0 || firstFrame >= FrameCount || firstFrame <= -(long)count) return;
        int write = firstFrame < 0 ? checked((int)-firstFrame) : 0;
        long frame = Math.Max(0, firstFrame);
        if (frame >= FrameCount) return;
        int segment = FindSegment(frame);
        int bytesPerSample = Format.BitsPerSample / 8;
        int align = Format.BlockAlign;
        while (write < count && frame < FrameCount)
        {
            long segmentStart = segment == 0 ? 0 : _ends[segment - 1];
            int available = (int)Math.Min(count - write, _ends[segment] - frame);
            int offset = checked((int)((frame - segmentStart) * align));
            var bytes = _segments[segment].Span;
            for (int i = 0; i < available; i++, offset += align)
            {
                float left = DecodeSample(bytes[offset..]);
                destination[(write + i) * 2] = left;
                destination[(write + i) * 2 + 1] = Format.Channels == 1 ? left : DecodeSample(bytes[(offset + bytesPerSample)..]);
            }
            write += available;
            frame += available;
            segment++;
        }
    }

    /// <summary>Legacy linear interpolation. Use PcmResampler for rate-adaptive low-pass filtering.</summary>
    public float Sample(double sourceTime, int channel)
    {
        var position = (sourceTime - StartTime) * Format.SampleRate;
        if (!double.IsFinite(position) || position < 0 || position >= FrameCount) return 0;
        var frame = (long)Math.Floor(position);
        var fraction = (float)(position - frame);
        var a = ReadFrame(frame, channel);
        return a + (ReadFrame(Math.Min(frame + 1, FrameCount - 1), channel) - a) * fraction;
    }
}
