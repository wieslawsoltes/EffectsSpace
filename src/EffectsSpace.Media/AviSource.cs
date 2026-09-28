using System.Buffers.Binary;
using EffectsSpace.Core;

namespace EffectsSpace.Media;

/// <summary>Classic AVI 1.0: one independently decodable Motion JPEG video stream and optional PCM audio.</summary>
public sealed class AviSource
{
    private readonly ReadOnlyMemory<byte>[] _frames;
    public int Width { get; }
    public int Height { get; }
    public FrameRate FrameRate { get; }
    public int FrameCount => _frames.Length;
    public double StartTime { get; }
    public double VideoDuration => FrameRate.Seconds(FrameCount);
    public double Duration => Math.Max(StartTime + VideoDuration, Audio is null ? 0 : Audio.StartTime + Audio.Duration);
    public PcmSource? Audio { get; }

    private AviSource(int width, int height, FrameRate rate, double start, ReadOnlyMemory<byte>[] frames, PcmSource? audio)
    { Width = width; Height = height; FrameRate = rate; StartTime = start; _frames = frames; Audio = audio; }

    public int FrameAt(double time)
    {
        var position = (time - StartTime) * FrameRate.Numerator / FrameRate.Denominator;
        if (!double.IsFinite(position) || position < 0) return -1;
        // Repair only floating-point round-off at an exact rational frame boundary.
        var index = Math.Floor(position + (Math.Abs(position) + 1) * 8 * 2.220446049250313e-16);
        return index >= 0 && index < FrameCount ? (int)index : -1;
    }

    public ReadOnlyMemory<byte> FrameData(int index) => index >= 0 && index < _frames.Length
        ? _frames[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public static AviSource Read(byte[] data)
    {
        var riff = new RiffReader(data, "AVI ");
        var root = riff.Children(12, riff.End);
        uint Tag(string text) => RiffReader.FourCC(text);
        IReadOnlyList<RiffReader.Chunk> Children(RiffReader.Chunk chunk)
        {
            if (chunk.Length < 4) throw new InvalidDataException("Truncated AVI list.");
            return riff.Children(chunk.Offset + 4, chunk.End);
        }
        RiffReader.Chunk Single(IEnumerable<RiffReader.Chunk> chunks, Func<RiffReader.Chunk, bool> predicate, string name)
        {
            var matches = chunks.Where(predicate).ToArray();
            return matches.Length == 1 ? matches[0] : throw new InvalidDataException("AVI requires exactly one " + name + ".");
        }
        bool List(RiffReader.Chunk c, string type) => c.Id == Tag("LIST") && c.Length >= 4 && riff.U32(c.Offset) == Tag(type);
        var headers = Children(Single(root, c => List(c, "hdrl"), "header list"));
        var main = Single(headers, c => c.Id == Tag("avih"), "main header");
        if (main.Length < 56) throw new InvalidDataException("Truncated AVI main header.");
        if ((riff.U32(main.Offset + 12) & 0x20) != 0) throw new NotSupportedException("AVI files requiring index-dependent presentation order are not supported.");
        var streamLists = headers.Where(c => List(c, "strl")).ToArray();
        if (streamLists.Length is < 1 or > 2 || riff.U32(main.Offset + 24) != streamLists.Length)
            throw new NotSupportedException("AVI supports one Motion JPEG video stream and at most one PCM audio stream.");
        var videoIndex = -1; var audioIndex = -1; var width = 0; var height = 0;
        var rate = default(FrameRate); var videoStart = 0d; var audioStart = 0d;
        uint videoLength = 0, audioLength = 0;
        PcmFormat? audioFormat = null;
        for (var i = 0; i < streamLists.Length; i++)
        {
            var streams = Children(streamLists[i]);
            var header = Single(streams, c => c.Id == Tag("strh"), "stream header");
            var format = Single(streams, c => c.Id == Tag("strf"), "stream format");
            if (header.Length < 56) throw new InvalidDataException("Truncated AVI stream header.");
            var type = riff.U32(header.Offset); var handler = riff.U32(header.Offset + 4);
            var scale = riff.U32(header.Offset + 20); var numerator = riff.U32(header.Offset + 24);
            var start = riff.U32(header.Offset + 28); var length = riff.U32(header.Offset + 32);
            if (scale == 0 || numerator == 0) throw new InvalidDataException("AVI stream has an invalid time base.");
            if (type == Tag("vids"))
            {
                if (videoIndex >= 0 || format.Length < 40) throw new NotSupportedException("Unsupported AVI video stream count/format.");
                var compression = riff.U32(format.Offset + 16);
                if (compression != Tag("MJPG") && compression != Tag("JPEG") && compression != Tag("mjpg"))
                    throw new NotSupportedException("AVI video must use Motion JPEG. H.264, MPEG-4 and other codecs are not decoded.");
                if (handler != 0 && handler != compression && handler != Tag("MJPG") && handler != Tag("JPEG"))
                    throw new InvalidDataException("AVI codec headers disagree.");
                width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(format.Offset + 4));
                height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(format.Offset + 8));
                if (riff.U32(format.Offset) < 40 || width is < 1 or > 8192 || height is < 1 or > 8192 || (long)width * height > 33554432)
                    throw new InvalidDataException("AVI video dimensions exceed the frame budget.");
                if (scale > 1000000 || numerator > 240000000 || numerator / (double)scale is < 1 or > 240)
                    throw new NotSupportedException("AVI frame rate must be 1–240 fps.");
                rate = new((int)numerator, (int)scale); videoStart = start * (double)scale / numerator;
                if (length > 100000 || videoStart > 86400) throw new InvalidDataException("AVI video exceeds the time/frame budget.");
                videoLength = length; videoIndex = i;
            }
            else if (type == Tag("auds"))
            {
                if (audioIndex >= 0) throw new NotSupportedException("Multiple AVI audio streams are not supported.");
                audioFormat = PcmFormat.Parse(riff.Memory(format).Span);
                var pcm = audioFormat.Value;
                if (Math.Abs(numerator / (double)scale - pcm.SampleRate) > 1e-7 || riff.U32(header.Offset + 44) != pcm.BlockAlign)
                    throw new InvalidDataException("AVI audio time base or sample size disagrees with PCM format.");
                audioStart = start * (double)scale / numerator; audioLength = length; audioIndex = i;
                if (audioStart > 86400) throw new InvalidDataException("AVI audio start exceeds the time budget.");
            }
            else throw new NotSupportedException("AVI contains an unsupported stream type.");
        }
        if (videoIndex < 0) throw new InvalidDataException("AVI has no Motion JPEG video stream.");
        var video = new List<ReadOnlyMemory<byte>>(); var audio = new List<ReadOnlyMemory<byte>>();
        var videoTag = Tag($"{videoIndex:00}dc"); var videoDb = Tag($"{videoIndex:00}db");
        var audioTag = audioIndex < 0 ? 0u : Tag($"{audioIndex:00}wb");
        void ReadMovie(RiffReader.Chunk parent, int depth)
        {
            if (depth > 8) throw new InvalidDataException("AVI record nesting exceeds eight levels.");
            foreach (var chunk in Children(parent))
            {
                if (List(chunk, "rec ")) { ReadMovie(chunk, depth + 1); continue; }
                if (chunk.Id == videoTag || chunk.Id == videoDb)
                {
                    if (chunk.Length < 3 || video.Count >= 100000) throw new InvalidDataException("Empty/dropped or excessive AVI video frames are not supported.");
                    var memory = riff.Memory(chunk);
                    if (memory.Span[0] != 0xff || memory.Span[1] != 0xd8 || memory.Span[2] != 0xff) throw new InvalidDataException("AVI frame is not a JPEG bitstream.");
                    video.Add(memory);
                }
                else if (audioIndex >= 0 && chunk.Id == audioTag) audio.Add(riff.Memory(chunk));
                else if (chunk.Id != Tag("JUNK") && chunk.Id != Tag("idx1"))
                    throw new NotSupportedException("Unsupported data chunk in AVI movie list.");
            }
        }
        ReadMovie(Single(root, c => List(c, "movi"), "movie list"), 0);
        if (video.Count == 0 || (videoLength != 0 && video.Count != videoLength)) throw new InvalidDataException("AVI video frame count disagrees with stream header.");
        var mainLength = riff.U32(main.Offset + 16);
        if (mainLength != 0 && mainLength != video.Count) throw new InvalidDataException("AVI main frame count is inconsistent.");
        var sound = audioFormat is { } af ? new PcmSource(af, audio, audioStart) : null;
        if (sound is not null && audioLength != 0 && sound.FrameCount != audioLength) throw new InvalidDataException("AVI audio sample count is inconsistent.");
        return new(width, height, rate, videoStart, video.ToArray(), sound);
    }
}
