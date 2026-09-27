using System.IO.Compression;
using EffectsSpace.Core;
using EffectsSpace.Editing;
using EffectsSpace.Skia;
using SkiaSharp;

var passed = 0; var failed = 0;
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); } }
void Check(bool value) { if (!value) throw new Exception("Assertion failed."); }
void Near(int expected, int actual, int tolerance = 2) { if (Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected}, got {actual}."); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception."); }
MotionProject Project(params Layer[] layers) { var comp = new Composition { Width = 64, Height = 64, Duration = 1, WorkEnd = 1, Layers = layers.ToList() }; return new() { ActiveCompositionId = comp.Id, Compositions = [comp] }; }
Layer Solid(string color = "#FF0000") => new() { Kind = LayerKind.Solid, Width = 64, Height = 64, OutPoint = 1, Fill = color };
SKBitmap Render(MotionProject project, double time = 0, int samples = 1) { using var renderer = new SkiaCompositor { MotionBlurSamples = samples }; var bytes = new FrameExporter(renderer).Png(project, project.Compositions[0], time); return SKBitmap.Decode(bytes); }

Test("transparent empty composition", () => { using var image = Render(Project()); Check(image.GetPixel(30, 30).Alpha == 0); });
Test("solid fills exact composition", () => { using var image = Render(Project(Solid())); var p = image.GetPixel(30, 30); Near(255, p.Red); Near(0, p.Green); Near(255, p.Alpha); });
Test("stacking order", () => { using var image = Render(Project(Solid("#00FF00"), Solid())); Near(255, image.GetPixel(30, 30).Green); });
Test("hidden layer omitted", () => { var l = Solid(); l.Enabled = false; using var image = Render(Project(l)); Check(image.GetPixel(30, 30).Alpha == 0); });
Test("animated opacity", () => { var l = Solid(); l.Transform.Opacity.SetKey(0, 0, Interpolation.Linear); l.Transform.Opacity.SetKey(1, 100); using var image = Render(Project(l), .5); Near(128, image.GetPixel(30, 30).Alpha); });
Test("transform translation", () => { var l = Solid(); l.Width = l.Height = 20; l.Transform.X.Value = 25; l.Transform.Y.Value = 25; using var image = Render(Project(l)); Check(image.GetPixel(5, 5).Alpha == 0); Near(255, image.GetPixel(30, 30).Alpha); });
Test("scale and anchor", () => { var l = Solid(); l.Width = l.Height = 20; l.Transform.AnchorX.Value = l.Transform.AnchorY.Value = 10; l.Transform.X.Value = l.Transform.Y.Value = 32; l.Transform.ScaleX.Value = l.Transform.ScaleY.Value = 200; using var image = Render(Project(l)); Near(255, image.GetPixel(15, 15).Alpha); Check(image.GetPixel(5, 5).Alpha == 0); });
Test("ellipse geometry", () => { var l = Solid(); l.Kind = LayerKind.Ellipse; using var image = Render(Project(l)); Check(image.GetPixel(0, 0).Alpha == 0); Near(255, image.GetPixel(32, 32).Alpha); });
Test("inverted colors", () => { var l = Solid(); l.Effects.Add(EffectCatalog.Create(EffectKind.Invert)); using var image = Render(Project(l)); var p = image.GetPixel(30, 30); Near(0, p.Red); Near(255, p.Green); Near(255, p.Blue); });
Test("multiply blend", () => { var a = Solid("#808080"); a.Blend = LayerBlend.Multiply; using var image = Render(Project(a, Solid("#808080"))); Near(64, image.GetPixel(32,32).Red, 3); });
Test("screen blend", () => { var a = Solid("#808080"); a.Blend = LayerBlend.Screen; using var image = Render(Project(a, Solid("#808080"))); Near(192, image.GetPixel(32,32).Red, 3); });
Test("add mask clips alpha", () => { var l = Solid(); l.Masks.Add(new() { Path = ShapePath.Rectangle(30, 64) }); using var image = Render(Project(l)); Near(255, image.GetPixel(10,30).Alpha); Near(0, image.GetPixel(45,30).Alpha); });
Test("subtract mask removes alpha", () => { var l = Solid(); l.Masks.Add(new() { Mode = MaskMode.Subtract, Path = ShapePath.Rectangle(30, 64) }); using var image = Render(Project(l)); Near(0, image.GetPixel(10,30).Alpha); Near(255, image.GetPixel(45,30).Alpha); });
Test("inverted mask", () => { var l = Solid(); l.Masks.Add(new() { Inverted = true, Path = ShapePath.Rectangle(30,64) }); using var image = Render(Project(l)); Near(0, image.GetPixel(10,30).Alpha); Near(255, image.GetPixel(45,30).Alpha); });
Test("feather produces partial alpha", () => { var l = Solid(); l.Masks.Add(new() { Feather = 5, Path = ShapePath.Rectangle(30,64) }); using var image = Render(Project(l)); var a = image.GetPixel(30,30).Alpha; Check(a > 10 && a < 245); });
Test("hidden track matte still supplies alpha", () => { var matte = Solid(); matte.Width = 30; matte.Enabled = false; var l = Solid(); l.MatteId = matte.Id; using var image = Render(Project(l, matte)); Near(255, image.GetPixel(10,30).Alpha); Near(0, image.GetPixel(45,30).Alpha); });
Test("inverted alpha matte", () => { var matte = Solid(); matte.Width = 30; matte.Enabled = false; var l = Solid(); l.MatteId = matte.Id; l.Matte = TrackMatte.AlphaInverted; using var image = Render(Project(l, matte)); Near(0, image.GetPixel(10,30).Alpha); Near(255, image.GetPixel(45,30).Alpha); });
Test("nested composition keeps transparent areas", () => { var source = new Composition { Width = 64, Height = 64, Duration = 1, WorkEnd = 1, Layers = [new Layer { Kind = LayerKind.Solid, Width = 20, Height = 20, OutPoint = 1, Fill = "#00FF00" }] }; var wrapper = new Layer { Kind = LayerKind.Composition, SourceId = source.Id, Width = 64, Height = 64, OutPoint = 1 }; var p = Project(wrapper); p.Compositions.Add(source); using var image = Render(p); Near(255, image.GetPixel(10,10).Green); Near(0, image.GetPixel(50,50).Alpha); });
Test("Bezier geometry contains curved area", () => { var shape = new ShapePath { Nodes = [new() { Point = new(0,0), OutHandle = new(30,0) }, new() { Point = new(64,64), InHandle = new(0,-30) }, new() { Point = new(0,64) }] }; using var path = PathGeometry.Build(shape); Check(path.Contains(5,40)); Check(!path.Contains(60,1)); });
foreach (var definition in EffectCatalog.All)
    Test("effect renders " + definition.Name, () => { var l = Solid("#808080"); l.Effects.Add(EffectCatalog.Create(definition.Kind)); using var image = Render(Project(l)); Check(image.Width == 64 && image.Height == 64); });
Test("all blend modes render", () => { foreach (var blend in Enum.GetValues<LayerBlend>()) { var l = Solid("#A87030"); l.Blend = blend; using var image = Render(Project(l, Solid("#345678"))); Near(255, image.GetPixel(30,30).Alpha); } });
Test("motion blur static alpha", () => { using var image = Render(Project(Solid()), .5, 4); Near(255, image.GetPixel(30,30).Alpha, 3); });
Test("PNG signature and dimensions", () => { using var renderer = new SkiaCompositor(); var p = Project(Solid()); var bytes = new FrameExporter(renderer).Png(p,p.Compositions[0],0,32); Check(bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})); var info = SkiaCompositor.ImageInfo(bytes); Check(info == (32,32)); });
Test("embedded image decoding", () => { using var renderer = new SkiaCompositor(); var source = Project(Solid("#00FF00")); var bytes = new FrameExporter(renderer).Png(source,source.Compositions[0],0); var asset = new MediaAsset { Data = bytes, Width = 64, Height = 64 }; var project = Project(new Layer { Kind = LayerKind.Image, SourceId = asset.Id, Width = 64, Height = 64, OutPoint = 1 }); project.Assets.Add(asset); using var image = SKBitmap.Decode(new FrameExporter(renderer).Png(project,project.Compositions[0],0)); Near(255,image.GetPixel(30,30).Green); Check(renderer.CachedImageBytes == 64*64*4); renderer.ClearImages(); Check(renderer.CachedImageBytes == 0); });
Test("corrupt image rejected", () => Throws(() => SkiaCompositor.ImageInfo([1,2,3])));
Test("sample composition export", () => { var p = SampleProject.Create(); using var renderer = new SkiaCompositor(); var bytes = new FrameExporter(renderer).Png(p,p.Compositions[0],2.4,480); using var image = SKBitmap.Decode(bytes); Check(image.Width == 480 && image.Height == 270); Near(255,image.GetPixel(2,2).Alpha); Directory.CreateDirectory("artifacts/screenshots"); File.WriteAllBytes("artifacts/screenshots/orbital-frame.png", bytes); });
Test("PNG sequence manifest and frame count", () => { var p = Project(Solid()); var c = p.Compositions[0]; c.WorkEnd = .1; using var renderer = new SkiaCompositor(); var bytes = new FrameExporter(renderer).PngSequenceAsync(p,c,16).GetAwaiter().GetResult(); using var zip = new ZipArchive(new MemoryStream(bytes)); Check(zip.Entries.Count == 4); Check(zip.GetEntry("sequence.json") is not null); Check(zip.GetEntry("frames/frame-000002.png") is not null); });
Console.WriteLine($"\n{passed} renderer tests passed; {failed} failed.");
return failed == 0 ? 0 : 1;
