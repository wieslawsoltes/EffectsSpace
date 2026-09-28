using System.Text;
using EffectsSpace.Core;

namespace EffectsSpace.Media;

/// <summary>Seekable, bounded AVI 1.0 muxer. Frames are JPEG; optional audio is interleaved stereo PCM16.</summary>
public sealed class MjpegAviWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryWriter _writer;
    private readonly FrameRate _rate;
    private readonly int _expectedFrames, _audioRate;
    private readonly long _maximumBytes, _moviSize, _moviType;
    private readonly List<(uint Tag, uint Offset, uint Length)> _index = [];
    private int _written;
    private bool _complete, _disposed;
    public long BytesWritten => _stream.Position;

    public MjpegAviWriter(Stream stream, int width, int height, FrameRate frameRate, int frameCount,
        int audioSampleRate = 0, long maximumBytes = 256L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite || !stream.CanSeek || stream.Position != 0 || stream.Length != 0) throw new ArgumentException("AVI output must be an empty, seekable writable stream.", nameof(stream));
        if (width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 33554432 || frameCount is < 1 or > 100000 ||
            frameRate.Numerator is <= 0 or > 240000000 || frameRate.Denominator is <= 0 or > 1000000 || frameRate.FramesPerSecond is < 1 or > 240)
            throw new ArgumentOutOfRangeException(nameof(frameCount));
        if (audioSampleRate != 0) new PcmFormat(audioSampleRate, 2, 16).Validate();
        if (maximumBytes is < 1024 or > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _stream = stream; _writer = new BinaryWriter(stream, Encoding.ASCII, true); _rate = frameRate;
        _expectedFrames = frameCount; _audioRate = audioSampleRate; _maximumBytes = maximumBytes;
        Tag("RIFF"); _writer.Write(0u); Tag("AVI ");
        var headers = BeginList("hdrl");
        WriteChunk("avih", () =>
        {
            _writer.Write((uint)Math.Round(frameRate.Seconds(1) * 1000000)); _writer.Write(0u); _writer.Write(0u); _writer.Write(0x110u);
            _writer.Write((uint)frameCount); _writer.Write(0u); _writer.Write(audioSampleRate > 0 ? 2u : 1u); _writer.Write((uint)(width * height * 3));
            _writer.Write((uint)width); _writer.Write((uint)height); for (var i = 0; i < 4; i++) _writer.Write(0u);
        });
        var video = BeginList("strl");
        WriteChunk("strh", () => StreamHeader("vids", "MJPG", (uint)frameRate.Denominator, (uint)frameRate.Numerator,
            (uint)frameCount, 0, width, height));
        WriteChunk("strf", () =>
        {
            _writer.Write(40u); _writer.Write(width); _writer.Write(height); _writer.Write((ushort)1); _writer.Write((ushort)24); Tag("MJPG");
            _writer.Write((uint)(width * height * 3)); for (var i = 0; i < 4; i++) _writer.Write(0u);
        });
        EndList(video);
        if (audioSampleRate > 0)
        {
            var sound = BeginList("strl");
            WriteChunk("strh", () => StreamHeader("auds", "\0\0\0\0", 4, (uint)audioSampleRate * 4,
                checked((uint)AudioFrameBoundary(frameCount)), 4, 0, 0));
            WriteChunk("strf", () =>
            {
                _writer.Write((ushort)1); _writer.Write((ushort)2); _writer.Write((uint)audioSampleRate); _writer.Write((uint)audioSampleRate * 4);
                _writer.Write((ushort)4); _writer.Write((ushort)16);
            });
            EndList(sound);
        }
        EndList(headers); _moviSize = BeginList("movi"); _moviType = _moviSize + 4;
    }

    /// <summary>Exact integer sample partitioning prevents audio/video drift at NTSC rational rates.</summary>
    public long AudioFrameBoundary(int frame)
    {
        if (frame < 0 || frame > _expectedFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        return checked((long)frame * _audioRate * _rate.Denominator) / _rate.Numerator;
    }

    public void WriteFrame(ReadOnlySpan<byte> jpeg, ReadOnlySpan<float> stereoAudio = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_complete || _written >= _expectedFrames) throw new InvalidOperationException("AVI has no remaining video frame slots.");
        if (jpeg.Length < 3 || jpeg[0] != 0xff || jpeg[1] != 0xd8 || jpeg[2] != 0xff) throw new InvalidDataException("Expected a JPEG frame.");
        var audioFrames = AudioFrameBoundary(_written + 1) - AudioFrameBoundary(_written);
        if (stereoAudio.Length != audioFrames * 2) throw new ArgumentException("Audio block does not match the exact video-frame interval.", nameof(stereoAudio));
        long projected = _stream.Position + jpeg.Length + (jpeg.Length & 1) + stereoAudio.Length * 2L + 16 + (_index.Count + 2L) * 16 + 8;
        if (projected > _maximumBytes) throw new InvalidOperationException("AVI output exceeds the configured byte budget.");
        Sample("00dc", jpeg);
        if (stereoAudio.Length > 0)
        {
            var offset = checked((uint)(_stream.Position - _moviType)); Tag("01wb"); _writer.Write(checked((uint)stereoAudio.Length * 2));
            WaveFile.WriteSamples(_stream, stereoAudio); _index.Add((RiffReader.FourCC("01wb"), offset, checked((uint)stereoAudio.Length * 2)));
        }
        _written++;
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_complete) return;
        if (_written != _expectedFrames) throw new InvalidOperationException("Cannot finalize an incomplete AVI.");
        EndList(_moviSize);
        WriteChunk("idx1", () => { foreach (var item in _index) { _writer.Write(item.Tag); _writer.Write(0x10u); _writer.Write(item.Offset); _writer.Write(item.Length); } });
        var end = _stream.Position; Patch(4, checked((uint)(end - 8))); _writer.Flush(); _complete = true;
    }

    private void StreamHeader(string type, string handler, uint scale, uint rate, uint length, uint sampleSize, int width, int height)
    {
        Tag(type); Tag(handler); _writer.Write(0u); _writer.Write((ushort)0); _writer.Write((ushort)0); _writer.Write(0u);
        _writer.Write(scale); _writer.Write(rate); _writer.Write(0u); _writer.Write(length); _writer.Write(0u); _writer.Write(uint.MaxValue); _writer.Write(sampleSize);
        _writer.Write((short)0); _writer.Write((short)0); _writer.Write((short)width); _writer.Write((short)height);
    }
    private void Sample(string tag, ReadOnlySpan<byte> bytes)
    {
        var offset = checked((uint)(_stream.Position - _moviType)); Tag(tag); _writer.Write((uint)bytes.Length); _stream.Write(bytes);
        if ((bytes.Length & 1) != 0) _writer.Write((byte)0);
        _index.Add((RiffReader.FourCC(tag), offset, (uint)bytes.Length));
    }
    private void Tag(string value) => _writer.Write(RiffReader.FourCC(value));
    private long BeginList(string type) { Tag("LIST"); var position = _stream.Position; _writer.Write(0u); Tag(type); return position; }
    private void EndList(long sizePosition) => Patch(sizePosition, checked((uint)(_stream.Position - sizePosition - 4)));
    private void Patch(long position, uint value) { var end = _stream.Position; _stream.Position = position; _writer.Write(value); _stream.Position = end; }
    private void WriteChunk(string name, Action body)
    { Tag(name); var position = _stream.Position; _writer.Write(0u); body(); var length = checked((uint)(_stream.Position - position - 4)); Patch(position, length); if ((length & 1) != 0) _writer.Write((byte)0); }
    public void Dispose() { if (_disposed) return; _disposed = true; _writer.Dispose(); /* The caller always owns the stream. */ }
}
