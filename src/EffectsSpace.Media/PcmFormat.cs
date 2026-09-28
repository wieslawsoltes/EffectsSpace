using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Interleaved little-endian mono/stereo PCM; integer valid bits are left-aligned in their container.</summary>
public readonly record struct PcmFormat(int SampleRate, int Channels, int BitsPerSample, bool FloatingPoint = false, int ValidBitsPerSample = 0)
{
    public int SamplePrecision => ValidBitsPerSample == 0 ? BitsPerSample : ValidBitsPerSample;
    public int BlockAlign => checked(Channels * (BitsPerSample / 8));
    public int BytesPerSecond => checked(SampleRate * BlockAlign);

    public void Validate()
    {
        if (SampleRate is < 8000 or > 192000 || Channels is < 1 or > 2 ||
            (FloatingPoint ? BitsPerSample is not (32 or 64) : BitsPerSample is not (8 or 16 or 24 or 32)))
            throw new NotSupportedException("PCM supports mono/stereo, 8–192 kHz, integer 8/16/24/32-bit or IEEE float 32/64-bit.");
        if (SamplePrecision < 1 || SamplePrecision > BitsPerSample || (FloatingPoint && SamplePrecision != BitsPerSample))
            throw new InvalidDataException("PCM precision must fit its container; floating-point precision must equal its container size.");
    }

    public static PcmFormat Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16) throw new InvalidDataException("Truncated WAVEFORMAT.");
        int code = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        var average = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        var align = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..]);
        var precision = (int)bits;
        if (bytes.Length != 16 && (bytes.Length < 18 || BinaryPrimitives.ReadUInt16LittleEndian(bytes[16..]) > bytes.Length - 18))
            throw new InvalidDataException("WAVEFORMAT extension exceeds the format chunk.");
        if (code == 0xfffe)
        {
            if (bytes.Length < 40 || BinaryPrimitives.ReadUInt16LittleEndian(bytes[16..]) < 22)
                throw new InvalidDataException("Truncated WAVEFORMATEXTENSIBLE.");
            precision = BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..]);
            var mask = BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);
            var subtype = new Guid(bytes.Slice(24, 16));
            code = subtype == new Guid("00000001-0000-0010-8000-00aa00389b71") ? 1 :
                subtype == new Guid("00000003-0000-0010-8000-00aa00389b71") ? 3 : 0;
            if (precision < 1 || precision > bits)
                throw new InvalidDataException("Invalid WAVEFORMATEXTENSIBLE sample precision.");
            if (mask != 0 && mask != (channels == 1 ? 4u : 3u))
                throw new NotSupportedException("Only standard mono/stereo channel layouts are supported.");
        }
        if (code is not (1 or 3) || rate > int.MaxValue) throw new NotSupportedException("Compressed WAVE audio is not supported.");
        var format = new PcmFormat((int)rate, channels, bits, code == 3, precision == bits ? 0 : precision);
        format.Validate();
        if (align != format.BlockAlign || average != format.BytesPerSecond)
            throw new InvalidDataException("Inconsistent PCM block alignment or byte rate.");
        return format;
    }
}
