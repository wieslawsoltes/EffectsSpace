using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Portable RIFF/WAVE parsing and streaming PCM16 writing. No native codec dependency.</summary>
public static class WaveFile
{
    public static PcmSource Read(byte[] data)
    {
        var riff = new RiffReader(data, "WAVE");
        PcmFormat? format = null;
        var segments = new List<ReadOnlyMemory<byte>>();
        foreach (var chunk in riff.Children(12, riff.End))
        {
            if (chunk.Id == RiffReader.FourCC("fmt "))
            {
                if (format is not null) throw new InvalidDataException("Duplicate WAVE format.");
                format = PcmFormat.Parse(riff.Memory(chunk).Span);
            }
            else if (chunk.Id == RiffReader.FourCC("data")) segments.Add(riff.Memory(chunk));
        }
        if (format is null || segments.Count == 0) throw new InvalidDataException("WAVE requires format and sample data.");
        return new PcmSource(format.Value, segments);
    }

    public static void WriteHeader(Stream stream, int sampleRate, int channels, long frameCount)
    {
        var format = new PcmFormat(sampleRate, channels, 16); format.Validate();
        long length = checked(frameCount * format.BlockAlign);
        if (frameCount < 0 || length > uint.MaxValue - 36) throw new ArgumentOutOfRangeException(nameof(frameCount));
        Span<byte> header = stackalloc byte[44]; header.Clear();
        "RIFF"u8.CopyTo(header); BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(length + 36));
        "WAVEfmt "u8.CopyTo(header[8..]); BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 1); BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate); BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)format.BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)format.BlockAlign); BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]); BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)length); stream.Write(header);
    }

    public static void WriteSamples(Stream stream, ReadOnlySpan<float> samples)
    {
        Span<byte> buffer = stackalloc byte[4096]; var count = 0;
        foreach (var sample in samples)
        {
            var value = float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
            var pcm = (short)Math.Clamp((int)Math.Round(value * 32768d, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(buffer[count..], pcm); count += 2;
            if (count == buffer.Length) { stream.Write(buffer); count = 0; }
        }
        if (count > 0) stream.Write(buffer[..count]);
    }

    public static byte[] Encode(ReadOnlySpan<float> samples, int sampleRate = 48000, int channels = 2)
    {
        if (channels < 1 || samples.Length % channels != 0) throw new ArgumentException("Audio must contain complete interleaved frames.", nameof(samples));
        using var stream = new MemoryStream(); WriteHeader(stream, sampleRate, channels, samples.Length / channels); WriteSamples(stream, samples); return stream.ToArray();
    }
}
