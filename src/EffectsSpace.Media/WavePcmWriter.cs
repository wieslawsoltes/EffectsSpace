using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Forward-only, length-checked RIFF writer. Float delivery includes WAVEFORMATEX and a fact sample-count chunk.</summary>
/// <remarks>The caller owns the stream. Complete is explicit; disposal or failure never fabricates missing samples.</remarks>
public sealed class WavePcmWriter
{
    private readonly Stream _stream;
    private readonly int _channels, _sampleBytes;
    private readonly long _totalFrames;
    private readonly WaveEncoding _encoding;
    private readonly bool _dither;
    private readonly uint _seed;
    private long _writtenFrames;
    private bool _completed;
    public long WrittenFrames => _writtenFrames;

    public static long FileSize(long frames, int channels, WaveEncoding encoding)
    {
        if (frames < 0 || channels is < 1 or > 2 || !Enum.IsDefined(encoding)) throw new ArgumentOutOfRangeException(nameof(frames));
        int bytes = encoding == WaveEncoding.Pcm16 ? 2 : encoding == WaveEncoding.Pcm24 ? 3 : 4;
        long payload = checked(frames * channels * bytes);
        long total = checked(payload + (payload & 1) + (encoding == WaveEncoding.Float32 ? 58 : 44));
        if (total - 8 > uint.MaxValue || frames > uint.MaxValue) throw new InvalidOperationException("Classic RIFF WAVE exceeds its 32-bit size/sample count. RF64 is not implemented.");
        return total;
    }

    public WavePcmWriter(Stream stream, int sampleRate, int channels, long frameCount, WaveEncoding encoding = WaveEncoding.Pcm16,
        bool dither = false, uint ditherSeed = 0x45534658)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(stream));
        long fileSize = FileSize(frameCount, channels, encoding);
        int bits = encoding == WaveEncoding.Pcm16 ? 16 : encoding == WaveEncoding.Pcm24 ? 24 : 32;
        var format = new PcmFormat(sampleRate, channels, bits, encoding == WaveEncoding.Float32);
        format.Validate();
        if (dither && encoding == WaveEncoding.Float32) throw new ArgumentException("Dither applies only to integer delivery.", nameof(dither));
        _stream = stream;
        _channels = channels;
        _sampleBytes = bits / 8;
        _totalFrames = frameCount;
        _encoding = encoding;
        _dither = dither;
        _seed = ditherSeed;
        Span<byte> header = stackalloc byte[58];
        header.Clear();
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(fileSize - 8));
        "WAVEfmt "u8.CopyTo(header[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], (uint)(format.FloatingPoint ? 18 : 16));
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], (ushort)(format.FloatingPoint ? 3 : 1));
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)format.BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)format.BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], (ushort)bits);
        int data = 36;
        if (format.FloatingPoint)
        {
            // cbSize = 0, then a fact chunk describing sample frames, not scalar channel samples.
            "fact"u8.CopyTo(header[38..]);
            BinaryPrimitives.WriteUInt32LittleEndian(header[42..], 4);
            BinaryPrimitives.WriteUInt32LittleEndian(header[46..], (uint)frameCount);
            data = 50;
        }
        "data"u8.CopyTo(header[data..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[(data + 4)..], checked((uint)(frameCount * format.BlockAlign)));
        stream.Write(header[..(data + 8)]);
    }

    public void Write(ReadOnlySpan<float> interleaved)
    {
        if (_completed) throw new InvalidOperationException("WAVE has already been completed.");
        if (interleaved.Length % _channels != 0) throw new ArgumentException("Samples must contain complete channel frames.", nameof(interleaved));
        long frames = interleaved.Length / _channels;
        if (frames > _totalFrames - _writtenFrames) throw new InvalidOperationException("Sample data exceeds the declared WAVE frame count.");
        Span<byte> buffer = stackalloc byte[4096];
        int position = 0;
        long firstSample = _writtenFrames * _channels;
        for (int i = 0; i < interleaved.Length; i++)
        {
            if (position + _sampleBytes > buffer.Length)
            {
                _stream.Write(buffer[..position]);
                position = 0;
            }
            float sample = float.IsFinite(interleaved[i]) ? interleaved[i] : 0;
            if (_encoding == WaveEncoding.Float32)
                BinaryPrimitives.WriteInt32LittleEndian(buffer[position..], BitConverter.SingleToInt32Bits(sample));
            else
            {
                int scale = _encoding == WaveEncoding.Pcm16 ? 32768 : 8388608;
                double value = Math.Clamp((double)sample, -1, 1) * scale;
                if (_dither) value += Noise((ulong)(firstSample + i), _seed);
                int pcm = (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), -scale, scale - 1);
                if (_encoding == WaveEncoding.Pcm16) BinaryPrimitives.WriteInt16LittleEndian(buffer[position..], (short)pcm);
                else
                {
                    buffer[position] = (byte)pcm;
                    buffer[position + 1] = (byte)(pcm >> 8);
                    buffer[position + 2] = (byte)(pcm >> 16);
                }
            }
            position += _sampleBytes;
        }
        if (position > 0) _stream.Write(buffer[..position]);
        _writtenFrames += frames;
    }

    public void Complete()
    {
        if (_completed) return;
        if (_writtenFrames != _totalFrames) throw new InvalidOperationException("Cannot complete a WAVE with missing sample frames.");
        if (((_writtenFrames * _channels * _sampleBytes) & 1) != 0) _stream.WriteByte(0);
        _completed = true;
    }

    private static double Noise(ulong sample, uint seed)
    {
        static double Uniform(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                value ^= value >> 31;
            }
            return (value >> 11) * (1d / 9007199254740992d);
        }
        // TPDF of +/-1 target LSB. Counter indexing keeps output identical across write block sizes.
        return Uniform(sample ^ ((ulong)seed << 32)) - Uniform(sample ^ ((ulong)seed << 32) ^ 0xD1B54A32D192ED03UL);
    }
}
