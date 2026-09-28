using System.Buffers.Binary;

namespace EffectsSpace.Media;

/// <summary>Bounds-checked little-endian RIFF traversal. Offsets refer to the caller's immutable buffer.</summary>
internal sealed class RiffReader
{
    internal readonly record struct Chunk(uint Id, int Offset, int Length)
    {
        public int End => checked(Offset + Length);
    }

    private readonly byte[] _data;
    private int _chunks;
    public int End { get; }

    public RiffReader(byte[] data, string type)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        if (data.Length < 12 || U32(0) != FourCC("RIFF") || U32(8) != FourCC(type))
            throw new InvalidDataException($"Expected a little-endian RIFF {type} file.");
        long end = 8L + U32(4);
        if (end != data.Length || end < 12)
            throw new InvalidDataException("Truncated RIFF or unsupported trailing/segmented RIFF data.");
        End = (int)end;
    }

    public IReadOnlyList<Chunk> Children(int start, int end)
    {
        if (start < 0 || end < start || end > _data.Length) throw new InvalidDataException("Invalid RIFF range.");
        var result = new List<Chunk>();
        while (start < end)
        {
            if (++_chunks > 100000 || end - start < 8) throw new InvalidDataException("Invalid RIFF chunk count/header.");
            var id = U32(start);
            var size = U32(start + 4);
            long next = start + 8L + size + (size & 1);
            if (next > end || size > int.MaxValue) throw new InvalidDataException("RIFF chunk exceeds its containing list.");
            result.Add(new(id, start + 8, (int)size));
            start = (int)next;
        }
        return result;
    }

    public ReadOnlyMemory<byte> Memory(Chunk chunk) => _data.AsMemory(chunk.Offset, chunk.Length);
    public uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset, 4));
    public static uint FourCC(string value)
    {
        if (value.Length != 4 || value.Any(c => c > 127)) throw new ArgumentException("FourCC requires four ASCII characters.", nameof(value));
        return (uint)(value[0] | value[1] << 8 | value[2] << 16 | value[3] << 24);
    }
}
