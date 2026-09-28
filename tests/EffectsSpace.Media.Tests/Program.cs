using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EffectsSpace.Core;
using EffectsSpace.Media;

var passed = 0; var failed = 0;
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e}"); } }
void Check(bool value) { if (!value) throw new Exception("Assertion failed."); }
void Near(double expected, double actual, double tolerance = 1e-4) { if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected}, got {actual}."); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception."); }
byte[] Pcm(PcmFormat format, params byte[] bytes) => Wave(format, bytes);
byte[] Wave(PcmFormat format, byte[] samples, bool extensible = false, bool oddJunk = false)
{
    using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Encoding.ASCII, true);
    void Tag(string tag) => w.Write(Encoding.ASCII.GetBytes(tag));
    Tag("RIFF"); w.Write(0u); Tag("WAVE");
    if (oddJunk) { Tag("JUNK"); w.Write(1u); w.Write((byte)42); w.Write((byte)0); }
    Tag("fmt "); w.Write(extensible ? 40u : 16u); w.Write((ushort)(extensible ? 0xfffe : format.FloatingPoint ? 3 : 1));
    w.Write((ushort)format.Channels); w.Write(format.SampleRate); w.Write(format.BytesPerSecond); w.Write((ushort)format.BlockAlign); w.Write((ushort)format.BitsPerSample);
    if (extensible) { w.Write((ushort)22); w.Write((ushort)format.BitsPerSample); w.Write(format.Channels == 1 ? 4u : 3u); w.Write(new Guid(format.FloatingPoint ? "00000003-0000-0010-8000-00aa00389b71" : "00000001-0000-0010-8000-00aa00389b71").ToByteArray()); }
    Tag("data"); w.Write((uint)samples.Length); w.Write(samples); if ((samples.Length & 1) != 0) w.Write((byte)0);
    var size = stream.Length; stream.Position = 4; w.Write((uint)(size - 8)); return stream.ToArray();
}
// This suite validates the container, not JPEG pixels; renderer integration tests use real encoded frames.
byte[] jpeg = [255, 216, 255, 224, 0, 2, 255, 217];
byte[] Avi(FrameRate? rate = null, int frames = 3, int audioRate = 0)
{
    using var output = new MemoryStream(); using var writer = new MjpegAviWriter(output, 16, 16, rate ?? new(30, 1), frames, audioRate);
    for (var i = 0; i < frames; i++) writer.WriteFrame(jpeg, Enumerable.Repeat(.25f, checked((int)(writer.AudioFrameBoundary(i + 1) - writer.AudioFrameBoundary(i)) * 2)).ToArray());
    writer.Complete(); return output.ToArray();
}
(MotionProject Project, Composition Comp, Layer Layer) AudioProject(float amplitude = .25f)
{
    var p = MotionProject.Empty(); var c = p.Compositions[0]; c.Duration = c.WorkEnd = 2;
    var asset = new MediaAsset { MimeType = "audio/wav", Data = WaveFile.Encode(Enumerable.Repeat(amplitude, 16000).ToArray(), 8000, 1), Duration = 2 };
    var layer = new Layer { Kind = LayerKind.Audio, SourceId = asset.Id, OutPoint = 2 };
    p.Assets.Add(asset); c.Layers.Add(layer); return (p,c,layer);
}
float[] Mix(MotionProject p, Composition c, double time = .25, int frames = 16)
{ var result = new float[frames * 2]; new AudioMixer(p,c).Mix(time,0,result,8000); return result; }

Test("PCM16 WAVE round trip and clipping", () => { var wave = WaveFile.Read(WaveFile.Encode([-1f,-.5f,0,.5f,1f,2f],8000,1)); Near(6,wave.FrameCount); Near(-1,wave.ReadFrame(0,0)); Near(.5,wave.ReadFrame(3,0)); Near(32767d/32768,wave.ReadFrame(5,0)); });
Test("PCM8 unsigned and odd chunk padding", () => { var wave=WaveFile.Read(Wave(new(8000,1,8),[0,128,255],oddJunk:true)); Near(-1,wave.ReadFrame(0,0)); Near(0,wave.ReadFrame(1,0)); Near(127d/128,wave.ReadFrame(2,0)); });
Test("PCM24 sign extension", () => { var wave=WaveFile.Read(Pcm(new(8000,1,24),0,0,128,0,0,64)); Near(-1,wave.ReadFrame(0,0)); Near(.5,wave.ReadFrame(1,0)); });
Test("PCM32 integer", () => { var wave=WaveFile.Read(Pcm(new(8000,1,32),0,0,0,128,0,0,0,64)); Near(-1,wave.ReadFrame(0,0)); Near(.5,wave.ReadFrame(1,0)); });
Test("IEEE float32", () => { var wave=WaveFile.Read(Wave(new(8000,1,32,true),BitConverter.GetBytes(.25f))); Near(.25,wave.ReadFrame(0,0)); });
Test("IEEE float64", () => { var wave=WaveFile.Read(Wave(new(8000,1,64,true),BitConverter.GetBytes(-.125))); Near(-.125,wave.ReadFrame(0,0)); });
Test("nonfinite float samples are sanitized", () => { var wave=WaveFile.Read(Wave(new(8000,1,32,true),BitConverter.GetBytes(float.NaN))); Near(0,wave.ReadFrame(0,0)); });
Test("standard extensible PCM", () => { var wave=WaveFile.Read(Wave(new(48000,2,16),[0,64,0,32],extensible:true)); Near(.5,wave.ReadFrame(0,0)); Near(.25,wave.ReadFrame(0,1)); });
Test("unsupported channel layout rejected", () => { var bytes=Wave(new(48000,2,16),[0,64,0,32],extensible:true); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40),0x30); Throws(()=>WaveFile.Read(bytes)); });
Test("partial sample frame rejected", () => Throws(()=>WaveFile.Read(Wave(new(48000,2,16),[0,0]))));
Test("bad block alignment rejected", () => { var bytes=WaveFile.Encode([0f,0f]); bytes[32]=3; Throws(()=>WaveFile.Read(bytes)); });
Test("compressed audio rejected", () => { var bytes=WaveFile.Encode([0f,0f]); bytes[20]=2; Throws(()=>WaveFile.Read(bytes)); });
Test("truncated WAVE rejected at every prefix", () => { var bytes=WaveFile.Encode([.25f,.5f]); for(var i=0;i<bytes.Length;i++) {var prefix=bytes[..i];Throws(()=>WaveFile.Read(prefix));} });
Test("RIFF overflow chunk rejected", () => { var bytes=WaveFile.Encode([0f,0f]); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40),uint.MaxValue); Throws(()=>WaveFile.Read(bytes)); });
Test("PCM segment boundary interpolation", () => { var source=new PcmSource(new(8000,1,16),new ReadOnlyMemory<byte>[] {new byte[]{0,0},new byte[]{0,64}}); Near(.25,source.Sample(.5/8000,0)); Near(.5,source.ReadFrame(1,1)); });
Test("PCM source start and end are bounded", () => { var source=new PcmSource(new(8000,1,16),new ReadOnlyMemory<byte>[] {new byte[]{0,64}},1); Near(0,source.Sample(0,0)); Near(.5,source.Sample(1,0)); Near(0,source.Sample(1.001,0)); });
Test("mono is duplicated to stereo", () => { var source=WaveFile.Read(WaveFile.Encode([.25f],8000,1)); Near(source.Sample(0,0),source.Sample(0,1)); });
Test("AVI headers and frame data round trip", () => { var source=AviSource.Read(Avi()); Check(source.FrameRate==new FrameRate(30,1)); Near(3,source.FrameCount); Check(source.FrameData(1).Span.SequenceEqual(jpeg)); });
Test("AVI frame index uses zero-copy slices", () => { var data=Avi();var source=AviSource.Read(data);Check(MemoryMarshal.TryGetArray(source.FrameData(0),out var segment));Check(ReferenceEquals(data,segment.Array)); });
Test("AVI rational frame boundaries", () => { var source=AviSource.Read(Avi(new(30000,1001),600));for(var i=0;i<600;i++)Near(i,source.FrameAt(source.FrameRate.Seconds(i))); });
Test("AVI end is exclusive", () => { var source=AviSource.Read(Avi());Near(-1,source.FrameAt(-.01));Near(-1,source.FrameAt(source.Duration));Near(0,source.FrameAt(1d/30-1e-8));Near(-1,source.FrameAt(double.NaN)); });
Test("AVI interleaved PCM partition has no NTSC drift", () => { var source=AviSource.Read(Avi(new(30000,1001),30,48000));Near(48048,source.Audio!.FrameCount);Near(.25,source.Audio.Sample(.5,0)); });
Test("AVI writer leaves caller stream open", () => { using var stream=new MemoryStream();using(var writer=new MjpegAviWriter(stream,16,16,new(30,1),1)){writer.WriteFrame(jpeg);writer.Complete();}Check(stream.CanRead);Check(AviSource.Read(stream.ToArray()).FrameCount==1); });
Test("AVI rejects incomplete finalization", () => { using var stream=new MemoryStream();using var writer=new MjpegAviWriter(stream,16,16,new(30,1),2);writer.WriteFrame(jpeg);Throws(writer.Complete); });
Test("AVI refuses inconsistent audio before writing", () => { using var stream=new MemoryStream();using var writer=new MjpegAviWriter(stream,16,16,new(30,1),1,48000);var length=stream.Length;Throws(()=>writer.WriteFrame(jpeg,[0f,0f]));Near(length,stream.Length); });
Test("AVI output budget is enforced before sample writes", () => { using var stream=new MemoryStream();using var writer=new MjpegAviWriter(stream,16,16,new(30,1),1,48000,1024);Throws(()=>writer.WriteFrame(jpeg,new float[3200])); });
Test("AVI video codec mismatch is rejected", () => { var bytes=Avi();var index=Encoding.ASCII.GetString(bytes).IndexOf("MJPG",StringComparison.Ordinal);"H264"u8.CopyTo(bytes.AsSpan(index));Throws(()=>AviSource.Read(bytes)); });
Test("AVI invalid time base rejected", () => { var bytes=Avi();var index=Encoding.ASCII.GetString(bytes).IndexOf("strh",StringComparison.Ordinal);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index+28),0);Throws(()=>AviSource.Read(bytes)); });
Test("AVI truncated and segmented RIFF rejected", () => { var bytes=Avi();Throws(()=>AviSource.Read(bytes[..^1]));Throws(()=>AviSource.Read([..bytes,0])); });
Test("media metadata is indexed once", () => { var cache=new MediaCatalog();var asset=new MediaAsset{MimeType="video/x-msvideo",Data=Avi()};var first=cache.Get(asset);for(var i=0;i<100;i++)Check(ReferenceEquals(first,cache.Get(asset)));Near(1,cache.ParseCount);Near(100,cache.CacheHits); });
Test("same-id changed payload invalidates media metadata", () => { var cache=new MediaCatalog();var asset=new MediaAsset{MimeType="video/x-msvideo",Data=Avi()};cache.Get(asset);asset.Data=Avi(frames:5);Near(5,cache.Get(asset).Video!.FrameCount);Near(2,cache.ParseCount); });
Test("media metadata cache capacity is bounded", () => { var cache=new MediaCatalog{Capacity=2};for(var i=0;i<20;i++)cache.Get(new MediaAsset{MimeType="video/x-msvideo",Data=Avi()});Near(2,cache.Count); });
Test("unsupported media never returns a placeholder", () => Throws(()=>new MediaCatalog().Get(new MediaAsset{MimeType="video/mp4"})));
Test("PCM mixing preserves stereo samples", () => { var (p,c,l)=AudioProject();var samples=Mix(p,c);Check(samples.All(s=>Math.Abs(s-.25)<1e-5)); });
Test("gain is measured in decibels", () => { var(p,c,l)=AudioProject();l.AudioGain.Value=-6.020599913;Near(.125,Mix(p,c)[0]); });
Test("balance suppresses the opposite channel", () => { var(p,c,l)=AudioProject();l.AudioPan.Value=100;var s=Mix(p,c);Near(0,s[0]);Near(.25,s[1]); });
Test("minus 96 dB is silence", () => { var(p,c,l)=AudioProject();l.AudioGain.Value=-96;Check(Mix(p,c).All(s=>s==0)); });
Test("audio mute is independent of gain", () => { var(p,c,l)=AudioProject();l.AudioEnabled=false;Check(!new AudioMixer(p,c).HasAudio); });
Test("guide audio excluded from export plan", () => { var(p,c,l)=AudioProject();l.Guide=true;Check(!new AudioMixer(p,c).HasAudio);Check(new AudioMixer(p,c,includeGuides:true).HasAudio); });
Test("layer interval gates audio samples", () => { var(p,c,l)=AudioProject();l.InPoint=.5;l.OutPoint=1;Check(Mix(p,c,.25).All(s=>s==0));Near(.25,Mix(p,c,.75)[0]);Check(Mix(p,c,1).All(s=>s==0)); });
Test("animated gain is evaluated in layer composition time", () => { var(p,c,l)=AudioProject();l.AudioGain.SetKey(0,0,Interpolation.Linear);l.AudioGain.SetKey(1,-12,Interpolation.Linear);Near(.25*Math.Pow(10,-6d/20),Mix(p,c,.5)[0]); });
Test("mixing sums without hidden normalization", () => { var(p,c,l)=AudioProject(.75f);c.Layers.Add(new Layer{Kind=LayerKind.Audio,SourceId=l.SourceId,OutPoint=2});Near(1.5,Mix(p,c)[0]); });
Test("nested composition audio observes source offsets", () => { var(p,c,l)=AudioProject();var nested=new Composition{Duration=2,WorkEnd=2,Layers=[l]};p.Compositions.Add(nested);c.Layers=[new Layer{Kind=LayerKind.Composition,SourceId=nested.Id,InPoint=.5,StartTime=.5,OutPoint=2}];Near(0,Mix(p,c,.25)[0]);Near(.25,Mix(p,c,.75)[0]); });
Test("nested gain and balance multiply", () => { var(p,c,l)=AudioProject();var nested=new Composition{Duration=2,WorkEnd=2,Layers=[l]};p.Compositions.Add(nested);var wrapper=new Layer{Kind=LayerKind.Composition,SourceId=nested.Id,OutPoint=2};wrapper.AudioGain.Value=-6.020599913;wrapper.AudioPan.Value=-100;c.Layers=[wrapper];var s=Mix(p,c);Near(.125,s[0]);Near(0,s[1]); });
Test("frozen audio is silence rather than DC", () => { var(p,c,l)=AudioProject();l.TimeRemapEnabled=true;l.TimeRemap.Value=.5;Check(Mix(p,c).All(s=>s==0)); });
Test("continuous remap supplies audio", () => { var(p,c,l)=AudioProject();l.TimeRemapEnabled=true;l.TimeRemap.SetKey(0,0,Interpolation.Linear);l.TimeRemap.SetKey(2,1,Interpolation.Linear);Near(.25,Mix(p,c)[0]); });
Test("audio cycle is rejected", () => { var p=MotionProject.Empty();var c=p.Compositions[0];c.Layers.Add(new Layer{Kind=LayerKind.Composition,SourceId=c.Id});Throws(()=>new AudioMixer(p,c)); });
Test("block partitioning preserves exact samples", () => { var(p,c,l)=AudioProject();var mixer=new AudioMixer(p,c);var whole=new float[2000];var split=new float[2000];mixer.Mix(.1,0,whole);mixer.Mix(.1,0,split.AsSpan(0,874));mixer.Mix(.1,437,split.AsSpan(874));Check(whole.SequenceEqual(split)); });
Test("prepared static mixing has no per-block allocation", () => { var(p,c,l)=AudioProject();var mixer=new AudioMixer(p,c);var buffer=new float[2048];for(var i=0;i<10;i++)mixer.Mix(0,0,buffer);var start=GC.GetAllocatedBytesForCurrentThread();for(var i=0;i<20;i++)mixer.Mix(0,0,buffer);Near(0,GC.GetAllocatedBytesForCurrentThread()-start,256); });
Test("WAVE export frame count and amplitude", () => { var(p,c,l)=AudioProject();var bytes=new AudioMixer(p,c).WaveAsync(0,.1).GetAwaiter().GetResult();var source=WaveFile.Read(bytes);Near(4800,source.FrameCount);Near(.25,source.ReadFrame(100,0)); });
Test("WAVE export byte budget", () => { var(p,c,l)=AudioProject();Throws(()=>new AudioMixer(p,c).WaveAsync(0,2,maximumBytes:100).GetAwaiter().GetResult()); });
Test("WAVE export cancellation", () => { var(p,c,l)=AudioProject();Throws(()=>new AudioMixer(p,c).WaveAsync(0,1,cancellationToken:new CancellationToken(true)).GetAwaiter().GetResult()); });

if (failed == 0)
{
    var data=Avi(new(30,1),120,48000);var asset=new MediaAsset{MimeType="video/x-msvideo",Data=data};var cache=new MediaCatalog();cache.Get(asset);
    double Measure(Action action) { var samples=new double[7];for(var k=0;k<7;k++){var timer=Stopwatch.StartNew();for(var i=0;i<100;i++)action();samples[k]=timer.Elapsed.TotalMilliseconds/100;}Array.Sort(samples);return samples[3]; }
    var cold=Measure(()=>AviSource.Read(data));var warm=Measure(()=>cache.Get(asset));
    Directory.CreateDirectory("artifacts/performance");
    File.WriteAllText("artifacts/performance/media-index.json",JsonSerializer.Serialize(new{workload="120-frame AVI metadata lookup; 7 rounds of 100 calls",referenceMilliseconds=cold,cachedMilliseconds=warm,cache.ParseCount,cache.CacheHits,encodedBytes=data.Length,encodedFrameCopies=0},new JsonSerializerOptions{WriteIndented=true}));
}
Console.WriteLine($"{passed} portable media tests passed; {failed} failed.");
return failed==0?0:1;
