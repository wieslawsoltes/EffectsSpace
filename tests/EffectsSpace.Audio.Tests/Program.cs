using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using EffectsSpace.Core;
using EffectsSpace.Media;

int passed = 0, failed = 0;
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
}
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
void Near(double expected, double actual, double tolerance = 1e-6)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected}, got {actual} (tolerance {tolerance}).");
}
void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
PcmSource Source(int rate, int frames, Func<int, int, float> value, int segmentFrames = int.MaxValue)
{
    var segments = new List<ReadOnlyMemory<byte>>();
    for (int first = 0; first < frames;)
    {
        int count = Math.Min(segmentFrames, frames - first);
        var bytes = new byte[count * 8];
        for (int i = 0; i < count; i++)
            for (int channel = 0; channel < 2; channel++)
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan((i * 2 + channel) * 4), BitConverter.SingleToInt32Bits(value(first + i, channel)));
        segments.Add(bytes);
        first += count;
    }
    return new(new(rate, 2, 32, true), segments);
}
byte[] Encode(float[] samples, WaveEncoding encoding, int channels = 2, int blockFrames = int.MaxValue, bool dither = false, int rate = 48000)
{
    using var stream = new MemoryStream();
    var writer = new WavePcmWriter(stream, rate, channels, samples.Length / channels, encoding, dither);
    for (int i = 0; i < samples.Length;)
    {
        int count = (int)Math.Min((long)blockFrames * channels, samples.Length - i);
        writer.Write(samples.AsSpan(i, count));
        i += count;
    }
    writer.Complete();
    return stream.ToArray();
}
byte[] Extensible(int bits, int valid, bool floating = false)
{
    var b = new byte[40];
    BinaryPrimitives.WriteUInt16LittleEndian(b, 0xfffe);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2), 1);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 48000);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), (uint)(48000 * bits / 8));
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(12), (ushort)(bits / 8));
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(14), (ushort)bits);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(16), 22);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(18), (ushort)valid);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(20), 4);
    new Guid(floating ? "00000003-0000-0010-8000-00aa00389b71" : "00000001-0000-0010-8000-00aa00389b71").TryWriteBytes(b.AsSpan(24));
    return b;
}
(MotionProject Project, Composition Comp, Layer Layer) AudioProject(int rate = 48000, double frequency = 440)
{
    var p = MotionProject.Empty();
    var c = p.Compositions[0]; c.Duration = c.WorkEnd = 2;
    var samples = new float[rate * 4];
    for (int i = 0; i < rate * 2; i++) samples[i * 2] = samples[i * 2 + 1] = (float)(.5 * Math.Sin(2 * Math.PI * frequency * i / rate));
    var asset = new MediaAsset { MimeType = "audio/wav", Data = Encode(samples, WaveEncoding.Float32, rate: rate), Duration = 2 };
    var layer = new Layer { Kind = LayerKind.Audio, SourceId = asset.Id, OutPoint = 2 };
    p.Assets.Add(asset); c.Layers.Add(layer); return (p, c, layer);
}
double Rms(PcmSource source, int outputRate, double speed, AudioResamplingQuality quality, double frequency = 0)
{
    var sampler = new PcmResampler(source, quality);
    double sum = 0;
    for (int i = 0; i < 4096; i++)
    {
        double time = .25 + i * speed / outputRate;
        sampler.Sample(time, speed, outputRate, out float left, out _);
        double error = frequency == 0 ? left : left - .5 * Math.Sin(2 * Math.PI * frequency * time);
        sum += error * error;
    }
    return Math.Sqrt(sum / 4096);
}

Test("extensible 20 valid bits in 24-bit container", () =>
{
    var f = PcmFormat.Parse(Extensible(24, 20)); Check(f.SamplePrecision == 20);
    var source = new PcmSource(f, new ReadOnlyMemory<byte>[] { new byte[] { 15, 0, 64, 15, 0, 128 } });
    Near(.5, source.ReadFrame(0, 0)); Near(-1, source.ReadFrame(1, 0));
});
Test("extensible 24 valid bits in 32-bit container", () =>
{
    var f = PcmFormat.Parse(Extensible(32, 24));
    var source = new PcmSource(f, new ReadOnlyMemory<byte>[] { new byte[] { 255, 0, 0, 64, 255, 0, 0, 128 } });
    Near(.5, source.ReadFrame(0, 0)); Near(-1, source.ReadFrame(1, 0));
});
Test("extensible full-width preserves legacy format identity", () => Check(PcmFormat.Parse(Extensible(16, 16)) == new PcmFormat(48000, 1, 16)));
Test("zero precision rejected", () => Throws<InvalidDataException>(() => PcmFormat.Parse(Extensible(24, 0))));
Test("over-wide precision rejected", () => Throws<InvalidDataException>(() => PcmFormat.Parse(Extensible(24, 25))));
Test("fractional floating point precision rejected", () => Throws<InvalidDataException>(() => PcmFormat.Parse(Extensible(32, 24, true))));
Test("extension length cannot exceed chunk", () => { var b = Extensible(24, 24); b[16] = 80; Throws<InvalidDataException>(() => PcmFormat.Parse(b)); });
Test("unsigned narrow precision is centered", () => { var s = new PcmSource(new(8000, 1, 8, false, 4), new ReadOnlyMemory<byte>[] { new byte[] { 0, 128, 255 } }); Near(-1, s.ReadFrame(0, 0)); Near(0, s.ReadFrame(1, 0)); Near(.875, s.ReadFrame(2, 0)); });

foreach (var f in new[] { new PcmFormat(48000, 1, 8), new(48000, 2, 16), new(48000, 2, 24), new(48000, 2, 32), new(48000, 2, 32, true), new(48000, 1, 64, true), new(48000, 1, 32, false, 20) })
    Test("block decoding equals scalar " + f, () =>
    {
        var random = new Random(2026); var bytes = new byte[f.BlockAlign * 71]; random.NextBytes(bytes);
        var segments = new List<ReadOnlyMemory<byte>>();
        for (int i = 0; i < 71; i += 7) segments.Add(bytes.AsMemory(i * f.BlockAlign, Math.Min(7, 71 - i) * f.BlockAlign));
        var source = new PcmSource(f, segments);
        foreach (int first in new[] { -9, 0, 5, 64, 71, 80 })
        {
            var stereo = new float[64]; source.ReadStereoFrames(first, stereo);
            for (int i = 0; i < 32; i++) for (int channel = 0; channel < 2; channel++) Near(source.ReadFrame(first + i, channel), stereo[i * 2 + channel], 0);
        }
    });
Test("empty PCM block is silence", () => { var s = new PcmSource(new(48000, 2, 16), []); var b = new float[16]; s.ReadStereoFrames(0, b); Check(b.All(v => v == 0)); });
Test("extreme negative PCM request zero-pads", () => { var s = Source(48000, 20, (_, _) => .5f); var b = new float[16]; s.ReadStereoFrames(long.MinValue, b); Check(b.All(v => v == 0)); });
Test("PCM block detects overflow and incomplete stereo", () => { var s = Source(48000, 20, (_, _) => .5f); Throws<ArgumentOutOfRangeException>(() => s.ReadStereoFrames(long.MaxValue, new float[16])); Throws<ArgumentException>(() => s.ReadStereoFrames(0, new float[3])); });
Test("block decode allocates no per-call objects", () =>
{
    var s = Source(48000, 5000, (i, c) => (i % 17 + c) / 20f, 97); var b = new float[4096];
    for (int i = 0; i < 10; i++) s.ReadStereoFrames(123, b);
    long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 20; i++) s.ReadStereoFrames(123, b);
    Near(0, GC.GetAllocatedBytesForCurrentThread() - before, 256);
});
Test("windowed linear sampler matches legacy including endpoint hold", () =>
{
    var source = Source(48000, 997, (i, c) => (float)Math.Sin(i * .1 + c), 11);
    var reader = new PcmResampler(source, AudioResamplingQuality.Linear);
    for (double frame = 0; frame < source.FrameCount; frame += .37)
    {
        reader.Sample(frame / 48000, 1.1, 48000, out float l, out float r);
        Near(source.Sample(frame / 48000, 0), l, 1e-6); Near(source.Sample(frame / 48000, 1), r, 1e-6);
    }
});
Test("integer same-rate path is exact and avoids convolution", () =>
{
    var s = Source(48000, 48000, (i, c) => (float)Math.Sin(i * .03 + c), 101); var reader = new PcmResampler(s);
    for (int i = 0; i < 48000; i++) { reader.Sample(i / 48000d, 1, 48000, out float l, out float r); Near(s.ReadFrame(i, 0), l, 0); Near(s.ReadFrame(i, 1), r, 0); }
    Check(reader.FilteredFrames == 0 && reader.DirectFrames == 48000 && reader.WindowLoads < 110);
});
Test("band-limited DC gain and stereo independence", () =>
{
    var s = Source(48000, 48000, (_, c) => c == 0 ? .5f : -.25f); var r = new PcmResampler(s);
    for (int i = 0; i < 512; i++) { r.Sample(.2 + i / 44100d, 1, 44100, out float l, out float right); Near(.5, l, 1e-6); Near(-.25, right, 1e-6); }
});
Test("fractional 44.1 to 48 kHz sine reconstruction", () =>
{
    var s = Source(44100, 44100, (i, _) => (float)(.5 * Math.Sin(2 * Math.PI * 997 * i / 44100)));
    Check(Rms(s, 48000, 1, AudioResamplingQuality.BandLimited, 997) < 2e-5);
});
Test("downsample passband is retained", () =>
{
    var s = Source(48000, 48000, (i, _) => (float)(.5 * Math.Sin(2 * Math.PI * 1000 * i / 48000)));
    Near(.5 / Math.Sqrt(2), Rms(s, 16000, 1, AudioResamplingQuality.BandLimited), .001);
});
Test("12 kHz aliases suppressed during 48 to 16 kHz conversion", () =>
{
    var s = Source(48000, 48000, (i, _) => (float)(.5 * Math.Sin(2 * Math.PI * 12000 * i / 48000)));
    var linear = Rms(s, 16000, 1, AudioResamplingQuality.Linear); var filtered = Rms(s, 16000, 1, AudioResamplingQuality.BandLimited);
    Check(linear > .3 && filtered < .0005);
});
Test("varispeed cutoff includes playback derivative", () =>
{
    var s = Source(48000, 96000, (i, _) => (float)(.5 * Math.Sin(2 * Math.PI * 9000 * i / 48000)));
    Check(Rms(s, 24000, 2, AudioResamplingQuality.BandLimited) < .0005);
});
Test("negative playback rate reads reversed source", () =>
{
    var s = Source(48000, 48000, (i, _) => (i % 1000) / 1000f); var r = new PcmResampler(s);
    for (int i = 0; i < 3000; i++) { r.Sample((30000 - i) / 48000d, -1, 48000, out float l, out _); Near(s.ReadFrame(30000 - i, 0), l, 0); }
});
Test("fractional seek results do not depend on cache history", () =>
{
    var s = Source(48000, 48000, (i, _) => (float)Math.Cos(i * .03), 17); var a = new PcmResampler(s); var b = new PcmResampler(s);
    for (int i = 0; i < 80; i++) { a.Sample(.1 + i / 44100d, 1, 44100, out _, out _); a.Sample(.65, -1.3, 48000, out _, out _); a.Sample(.2 + i / 44100d, 1, 44100, out float l, out _); b.Sample(.2 + i / 44100d, 1, 44100, out float expected, out _); Near(expected, l, 0); }
});
Test("sampler silence at frozen time and source exterior", () => { var r = new PcmResampler(Source(48000, 48000, (_, _) => .5f)); foreach (var (t, speed) in new[] { (-1d, 1d), (1d, 1d), (.5d, 0d) }) { r.Sample(t, speed, 48000, out float l, out float right); Near(0, l); Near(0, right); } });
Test("unsupported antialias ratio fails explicitly", () => { var r = new PcmResampler(Source(48000, 48000, (_, _) => .5f)); Throws<NotSupportedException>(() => r.Sample(.1, 33, 48000, out _, out _)); });
Test("warmed fractional filtering has no sample allocations", () =>
{
    var r = new PcmResampler(Source(44100, 44100, (i, _) => (float)Math.Cos(i * .1)));
    for (int i = 0; i < 4096; i++) r.Sample(.1 + i / 48000d, 1, 48000, out _, out _);
    long bytes = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 4096; i++) r.Sample(.2 + i / 48000d, 1, 48000, out _, out _);
    Near(0, GC.GetAllocatedBytesForCurrentThread() - bytes, 256);
});
Test("mixer defaults preserve legacy resampling choice", () => { var (p, c, _) = AudioProject(); Check(new AudioMixer(p, c).Quality == AudioResamplingQuality.Linear); });
Test("band-limited mixer is invariant to output block partition", () =>
{
    var (p, c, _) = AudioProject(44100); var mixer = new AudioMixer(p, c, quality: AudioResamplingQuality.BandLimited);
    var whole = new float[20000]; var split = new float[20000]; mixer.Mix(.1, 0, whole);
    mixer.Mix(.1, 0, split.AsSpan(0, 174)); mixer.Mix(.1, 87, split.AsSpan(174, 7092)); mixer.Mix(.1, 3633, split.AsSpan(7266));
    Check(whole.SequenceEqual(split));
});
Test("nested time derivatives multiply for antialiasing", () =>
{
    var (p, c, l) = AudioProject(48000, 4000); l.Stretch = .5;
    var nested = new Composition { Duration = 2, WorkEnd = 2, Layers = [l] }; p.Compositions.Add(nested);
    c.Layers = [new Layer { Kind = LayerKind.Composition, SourceId = nested.Id, Stretch = .5, OutPoint = 2 }];
    var mix = new AudioMixer(p, c, quality: AudioResamplingQuality.BandLimited); var b = new float[2048]; mix.Mix(.1, 0, b, 24000);
    Check(Math.Sqrt(b.Sum(x => (double)x * x) / b.Length) < .001);
});

Test("PCM16 writer matches existing nondithered encoder", () => { var a = new[] { -2f, -1f, -.1f, 0f, .1f, 1f, 2f, float.NaN }; Check(WaveFile.Encode(a).SequenceEqual(Encode(a, WaveEncoding.Pcm16))); });
Test("PCM24 writer retains low amplitude precision", () => { var s = WaveFile.Read(Encode([1f / 8388608, -1f / 8388608, .125f, -.25f], WaveEncoding.Pcm24)); Check(s.Format.BitsPerSample == 24); Near(1d / 8388608, s.ReadFrame(0, 0), 0); Near(-1d / 8388608, s.ReadFrame(0, 1), 0); });
Test("float delivery retains headroom and sanitizes nonfinite samples", () => { var s = WaveFile.Read(Encode([1.5f, -2.25f, float.NaN, float.PositiveInfinity], WaveEncoding.Float32)); Check(s.Format.FloatingPoint); Near(1.5, s.ReadFrame(0, 0)); Near(-2.25, s.ReadFrame(0, 1)); Near(0, s.ReadFrame(1, 0)); Near(0, s.ReadFrame(1, 1)); });
Test("float WAVE fact stores frames not samples", () => { var b = Encode(new float[20], WaveEncoding.Float32); Check(b.AsSpan(38, 4).SequenceEqual("fact"u8)); Near(10, BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(46))); Near(80, BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(54))); });
Test("odd mono PCM24 has RIFF padding excluded from data size", () => { var b = Encode([.5f], WaveEncoding.Pcm24, channels: 1); Near(48, b.Length); Near(3, BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(40))); Near(40, BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4))); Near(.5, WaveFile.Read(b).ReadFrame(0, 0)); });
Test("TPDF dither is independent of write partition", () => { var samples = Enumerable.Repeat(.000017f, 10000).ToArray(); Check(Encode(samples, WaveEncoding.Pcm16, dither: true).SequenceEqual(Encode(samples, WaveEncoding.Pcm16, blockFrames: 17, dither: true))); });
Test("TPDF on silence stays within one quantized LSB", () => { var source = WaveFile.Read(Encode(new float[8000], WaveEncoding.Pcm16, dither: true)); int nonzero = 0; double mean = 0; for (int i = 0; i < source.FrameCount; i++) { double sample = source.ReadFrame(i, 0) * 32768; Check(Math.Abs(sample) <= 1); if (sample != 0) nonzero++; mean += sample; } Check(nonzero > 200 && Math.Abs(mean / source.FrameCount) < .05); });
Test("float delivery rejects meaningless dither", () => { using var stream = new MemoryStream(); Throws<ArgumentException>(() => new WavePcmWriter(stream, 48000, 2, 1, WaveEncoding.Float32, true)); Near(0, stream.Length); });
Test("incomplete writer cannot finalize", () => { using var stream = new MemoryStream(); var w = new WavePcmWriter(stream, 48000, 2, 2); w.Write([0f, 0f]); Throws<InvalidOperationException>(w.Complete); });
Test("overflow write rejected before touching stream", () => { using var stream = new MemoryStream(); var w = new WavePcmWriter(stream, 48000, 2, 1); long length = stream.Length; Throws<InvalidOperationException>(() => w.Write(new float[4])); Near(length, stream.Length); });
Test("write after Complete rejected and Complete is idempotent", () => { using var stream = new MemoryStream(); var w = new WavePcmWriter(stream, 48000, 2, 1); w.Write([0f, 0f]); w.Complete(); w.Complete(); Throws<InvalidOperationException>(() => w.Write([])); Check(stream.CanWrite); });
Test("RIFF budget is checked without allocating sample storage", () => Throws<InvalidOperationException>(() => WavePcmWriter.FileSize(uint.MaxValue, 2, WaveEncoding.Float32)));
Test("WAVE renderer enforces selected-format budget", () => { var (p, c, _) = AudioProject(); Throws<InvalidOperationException>(() => WaveRenderer.RenderAsync(new AudioMixer(p, c), 0, 1, WaveEncoding.Float32, maximumBytes: 100000).GetAwaiter().GetResult()); });
Test("WAVE renderer checks cancellation before rendering", () => { var (p, c, _) = AudioProject(); Throws<OperationCanceledException>(() => WaveRenderer.RenderAsync(new AudioMixer(p, c), 0, 1, cancellationToken: new CancellationToken(true)).GetAwaiter().GetResult()); });
foreach (var encoding in Enum.GetValues<WaveEncoding>())
    Test("delivery exact rate and sample count " + encoding, () =>
    {
        var (p, c, _) = AudioProject();
        byte[] bytes = WaveRenderer.RenderAsync(new AudioMixer(p, c, quality: AudioResamplingQuality.BandLimited), .1, .1, encoding, 44100).GetAwaiter().GetResult();
        var source = WaveFile.Read(bytes); Near(4410, source.FrameCount); Near(44100, source.Format.SampleRate); Near(.5 * Math.Sin(2 * Math.PI * 440 * (.1 + 100d / 44100)), source.ReadFrame(100, 0), 5e-5);
        Directory.CreateDirectory("artifacts/audio"); File.WriteAllBytes($"artifacts/audio/quality-{encoding}.wav", bytes);
    });

if (failed == 0)
{
    var source = Source(48000, 48000, (i, c) => (float)Math.Sin(i * .03 + c), 97);
    var reader = new PcmResampler(source, AudioResamplingQuality.Linear);
    double checksum = 0;
    void Reference() { for (int i = 0; i < 48000; i++) checksum += source.Sample(i / 48000d, 0) + source.Sample(i / 48000d, 1); }
    void Buffered() { for (int i = 0; i < 48000; i++) { reader.Sample(i / 48000d, 1, 48000, out float l, out float r); checksum += l + r; } }
    double Measure(Action action)
    {
        for (int i = 0; i < 3; i++) action();
        var times = new double[9];
        for (int i = 0; i < times.Length; i++) { var timer = Stopwatch.StartNew(); action(); times[i] = timer.Elapsed.TotalMilliseconds; }
        Array.Sort(times); return times[4];
    }
    double reference = Measure(Reference), buffered = Measure(Buffered);
    Directory.CreateDirectory("artifacts/performance");
    File.WriteAllText("artifacts/performance/audio-window.json", JsonSerializer.Serialize(new
    {
        workload = "48000 stereo float source frames, 495 encoded segments, legacy scalar linear lookup vs bounded stereo linear reader; 3 warmups, median of 9 passes",
        runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
        referenceMilliseconds = reference, bufferedMilliseconds = buffered, decodedWindowBytes = 8192,
        reader.WindowLoads, checksum, physicalGpuMeasurement = false
    }, new JsonSerializerOptions { WriteIndented = true }));
}
Console.WriteLine($"{passed} audio fidelity/performance tests passed; {failed} failed.");
return failed == 0 ? 0 : 1;
