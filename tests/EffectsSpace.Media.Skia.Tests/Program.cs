using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Media;
using EffectsSpace.Skia;
using SkiaSharp;

var passed = 0; var failed = 0;
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e}"); } }
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
void Near(double expected, double actual, double tolerance = 1e-5) { if (!double.IsFinite(actual) || Math.Abs(expected-actual)>tolerance) throw new Exception($"Expected {expected}; got {actual}."); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception."); }
var project = MediaStudy.Create(); var comp = project.Compositions[0]; var asset = project.Assets[0];
SKBitmap Render(SkiaCompositor renderer, double time, MotionProject? p = null)
{ p ??= project; return SKBitmap.Decode(new FrameExporter(renderer).Png(p,p.Compositions[0],time,320)); }
byte[] SolidAvi(SKColor color)
{
    using var output = new MemoryStream(); using var writer = new MjpegAviWriter(output,320,180,new(24,1),1);
    using var surface = SKSurface.Create(new SKImageInfo(320,180)); surface.Canvas.Clear(color);
    using var image = surface.Snapshot(); using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg,100);
    writer.WriteFrame(jpeg.AsSpan()); writer.Complete(); return output.ToArray();
}
Test("real media study validates and contains stereo AVI", () => { ProjectValidator.Validate(project); var source=AviSource.Read(asset.Data);Near(48,source.FrameCount);Near(96000,source.Audio!.FrameCount);Near(2,source.Duration); });
Test("all original JPEG frames decode and match container dimensions", () => { var source=AviSource.Read(asset.Data);for(var i=0;i<source.FrameCount;i++){using var data=SKData.CreateCopy(source.FrameData(i).Span);using var image=SKImage.FromEncodedData(data);Check(image is not null && image.Width==320 && image.Height==180);} });
Test("video renderer shows different decoded frames", () => { using var r=new SkiaCompositor();using var first=Render(r,0);using var last=Render(r,1);Check(first.GetPixel(254,90).Green>150);Check(last.GetPixel(254,90).Green<100);Near(2,r.VideoImageCreations); });
Test("repeated source time reuses a decoded image", () => { using var r=new SkiaCompositor();for(var i=0;i<8;i++){using var image=Render(r,.25);}Near(1,r.VideoImageCreations);Near(1,r.MediaIndexBuilds);Near(7,r.VideoFrameCacheHits); });
Test("video frame interval floors rather than rounds", () => { using var r=new SkiaCompositor();using var a=Render(r,.001);using var b=Render(r,1d/24-.0001);Near(1,r.VideoImageCreations);Check(a.Bytes.SequenceEqual(b.Bytes)); });
Test("source time after footage end is transparent", () => { var p=ProjectJson.Clone(project);p.Compositions[0].Layers[0].TimeRemapEnabled=true;p.Compositions[0].Layers[0].TimeRemap.Value=2;using var r=new SkiaCompositor();using var image=Render(r,0,p);Check(image.GetPixel(100,100).Alpha==0); });
Test("source remapping changes the decoded source frame", () => { var p=ProjectJson.Clone(project);var layer=p.Compositions[0].Layers[0];layer.TimeRemapEnabled=true;layer.TimeRemap.Value=1;using var r=new SkiaCompositor();using var actual=Render(r,0,p);using var expected=Render(r,1);Check(actual.Bytes.SequenceEqual(expected.Bytes)); });
Test("two differently remapped instances keep separate video frames", () => { var p=ProjectJson.Clone(project);var first=p.Compositions[0].Layers[0];first.Width=320;var second=EditorCommands.CloneForInsert(first);second.Transform.X.Value=320;second.TimeRemapEnabled=true;second.TimeRemap.Value=1;p.Compositions[0].Layers.Add(second);using var r=new SkiaCompositor();using var image=Render(r,0,p);Near(2,r.VideoImageCreations);Near(1,r.MediaIndexBuilds); });
Test("same-id replacement invalidates video frame and index cache", () => { var p=ProjectJson.Clone(project);using var r=new SkiaCompositor();using var a=Render(r,0,p);p.Assets[0].Data=SolidAvi(SKColors.Red);using var b=Render(r,0,p);Check(b.GetPixel(20,20).Red>240);Near(2,r.MediaIndexBuilds);Near(2,r.VideoImageCreations); });
Test("offscreen video skips JPEG creation", () => { var p=ProjectJson.Clone(project);p.Compositions[0].Layers[0].Transform.X.Value=10000;using var r=new SkiaCompositor();using var image=Render(r,0,p);Near(0,r.VideoImageCreations);Near(1,r.Metrics.CulledLayers); });
Test("video cache respects its decoded-pixel budget", () => { using var r=new SkiaCompositor{ImageCacheLimit=320*180*4*2};for(var i=0;i<8;i++){using var image=Render(r,i/24d);}Check(r.CachedImageBytes<=320*180*4*2);r.ClearResources();Near(0,r.CachedImageBytes); });
Test("invalid JPEG is not rendered as placeholder footage", () => { using var output=new MemoryStream();using(var w=new MjpegAviWriter(output,320,180,new(24,1),1)){w.WriteFrame([255,216,255,0]);w.Complete();}var p=ProjectJson.Clone(project);p.Assets[0].Data=output.ToArray();using var r=new SkiaCompositor();Throws(()=>{using var image=Render(r,0,p);}); });
Test("unsupported MP4 codec fails explicitly", () => { var p=ProjectJson.Clone(project);p.Assets[0].MimeType="video/mp4";using var r=new SkiaCompositor();Throws(()=>{using var image=Render(r,0,p);}); });
Test("image/video id namespaces cannot alias cached frames", () => { var p=ProjectJson.Clone(project);using var r=new SkiaCompositor();using var a=Render(r,0,p);var bytes=new FrameExporter(r).Png(p,comp,1,320);p.Assets[0].MimeType="image/png";p.Assets[0].Data=bytes;p.Compositions[0].Layers[0].Kind=LayerKind.Image;using var b=Render(r,0,p);Check(b.GetPixel(254,90).Green<100); });
Test("audio channels participate in editing and fresh duplicate identifiers", () => { var s=new EditorSession(ProjectJson.Clone(project));s.Select(s.Composition.Layers[0].Id);s.ToggleAnimation("AudioGain");s.SetTime(.5);s.SetProperty("AudioGain",-6);s.DuplicateSelection();ProjectValidator.Validate(s.Project);Near(2,s.Composition.Layers.Count);Near(2,s.Primary!.AudioGain.Keys.Count); });
Test("audio semantic ranges are validated", () => { var p=ProjectJson.Clone(project);p.Compositions[0].Layers[0].AudioGain.Value=25;Throws(()=>ProjectValidator.Validate(p)); });
Test("snapshot shares immutable bytes but isolates asset metadata", () => { var s=new EditorSession(ProjectJson.Clone(project));var copy=s.CaptureSnapshot();Check(ReferenceEquals(s.Project.Assets[0].Data,copy.Assets[0].Data));copy.Assets[0].Name="Changed copy";Check(s.Project.Assets[0].Name!="Changed copy");copy.Compositions[0].Layers[0].Name="Changed layer";Check(s.Composition.Layers[0].Name!="Changed layer"); });
Test("history detects payload replacement even with unchanged metadata", () => { var s=new EditorSession(ProjectJson.Clone(project));var original=s.Project.Assets[0].Data;var replacement=original.ToArray();s.Edit("Replace bytes",()=>s.Project.Assets[0].Data=replacement);Check(s.CanUndo);s.Undo();Check(ReferenceEquals(original,s.Project.Assets[0].Data));s.Redo();Check(ReferenceEquals(replacement,s.Project.Assets[0].Data)); });
Test("history restores asset names independently of later mutation", () => { var s=new EditorSession(ProjectJson.Clone(project));var name=s.Project.Assets[0].Name;s.Edit("Rename asset",()=>s.Project.Assets[0].Name="Renamed");s.Undo();Check(s.Project.Assets[0].Name==name);s.Redo();Check(s.Project.Assets[0].Name=="Renamed"); });
Test("AVI export honors rational frame and exact audio sample counts", () => { using var r=new SkiaCompositor();var p=ProjectJson.Clone(project);p.Compositions[0].WorkStart=.5;p.Compositions[0].WorkEnd=1.5;var bytes=new AviExporter(r).ExportAsync(p,p.Compositions[0],320).GetAwaiter().GetResult();var source=AviSource.Read(bytes);Near(24,source.FrameCount);Near(48000,source.Audio!.FrameCount);Near(320,source.Width); });
Test("AVI visual-only export contains no audio stream", () => { using var r=new SkiaCompositor();var p=ProjectJson.Clone(project);p.Compositions[0].WorkEnd=.125;var source=AviSource.Read(new AviExporter(r).ExportAsync(p,p.Compositions[0],320,audio:false).GetAwaiter().GetResult());Check(source.Audio is null);Near(3,source.FrameCount); });
Test("AVI export cancellation never returns an incomplete file", () => { using var r=new SkiaCompositor();Throws(()=>new AviExporter(r).ExportAsync(project,comp,cancellationToken:new CancellationToken(true)).GetAwaiter().GetResult()); });
Test("AVI export flattens alpha over composition background", () => { var p=MotionProject.Empty();var c=p.Compositions[0];c.Width=32;c.Height=32;c.Duration=c.WorkEnd=1d/30;c.Background="#336699";using var r=new SkiaCompositor();var source=AviSource.Read(new AviExporter(r).ExportAsync(p,c).GetAwaiter().GetResult());using var bytes=SKData.CreateCopy(source.FrameData(0).Span);using var bitmap=SKBitmap.Decode(bytes);var pixel=bitmap.GetPixel(16,16);Near(51,pixel.Red,5);Near(102,pixel.Green,5);Near(153,pixel.Blue,5); });
Test("video guide omitted from AVI export but present in direct preview", () => { var p=ProjectJson.Clone(project);p.Compositions[0].Layers[0].Guide=true;p.Compositions[0].WorkEnd=1d/24;using var r=new SkiaCompositor();var source=AviSource.Read(new AviExporter(r).ExportAsync(p,p.Compositions[0],320).GetAwaiter().GetResult());Check(source.Audio is null);using var data=SKData.CreateCopy(source.FrameData(0).Span);using var image=SKBitmap.Decode(data);Check(image.GetPixel(254,90).Green<100); });
if(failed==0)
{
    Directory.CreateDirectory("artifacts/media");File.WriteAllBytes("artifacts/media/clockwork-source.avi",asset.Data);
    using var renderer=new SkiaCompositor();
    File.WriteAllBytes("artifacts/media/clockwork-export.avi",new AviExporter(renderer).ExportAsync(project,comp,320).GetAwaiter().GetResult());
    File.WriteAllBytes("artifacts/media/clockwork-mix.wav",new AudioMixer(project,comp).WaveAsync(0,2).GetAwaiter().GetResult());
    File.WriteAllBytes("artifacts/media/clockwork-frame.png",new FrameExporter(renderer).Png(project,comp,0,320));
}
Console.WriteLine($"{passed} media integration tests passed; {failed} failed.");
return failed==0?0:1;
