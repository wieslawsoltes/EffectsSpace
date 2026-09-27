using System.Diagnostics;
using System.Text.Json;
using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Documents;
using EffectsSpace.Editing;
using EffectsSpace.Rendering;
using EffectsSpace.Skia;
using SkiaSharp;

var passed = 0; var failed = 0;
void Test(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
}
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
void Near(double expected, double actual, double tolerance = 2)
{ if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance) throw new Exception($"Expected {expected}, got {actual}"); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }
Layer Solid(string color = "#FF0000", double width = 64, double height = 64) =>
    new() { Kind = LayerKind.Solid, Fill = color, Width = width, Height = height, OutPoint = 1 };
MotionProject Project(params Layer[] layers)
{
    var c = new Composition { Width = 64, Height = 64, Duration = 1, WorkEnd = 1, Layers = layers.ToList() };
    return new() { ActiveCompositionId = c.Id, Compositions = [c] };
}
SKBitmap Render(MotionProject p, double time = 0, bool optimize = true, bool preview = false)
{
    using var renderer = new SkiaCompositor { EnableOptimizations = optimize };
    if (!preview) return SKBitmap.Decode(new FrameExporter(renderer).Png(p, p.Compositions[0], time));
    using var surface = SKSurface.Create(new SKImageInfo(64, 64)); surface.Canvas.Clear(SKColors.Transparent);
    renderer.Render(surface.Canvas, p, p.Compositions[0], time, includeGuides: true);
    using var image = surface.Snapshot(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
    return SKBitmap.Decode(png);
}
Layer Adjustment(double opacity = 100)
{
    var l = Solid(); l.Kind = LayerKind.Adjustment; l.Transform.Opacity.Value = opacity;
    l.Effects.Add(EffectCatalog.Create(EffectKind.Invert)); return l;
}
void SamePixels(MotionProject p, double time = 0)
{
    using var a = Render(p, time, false); using var b = Render(p, time, true);
    var x = a.Bytes; var y = b.Bytes;
    Check(x.Length == y.Length);
    for (var i = 0; i < x.Length; i++) Near(x[i], y[i], 1);
}

Test("luma white matte is opaque", () => { var m = Solid("#FFFFFF"); m.Enabled = false; var l = Solid(); l.MatteId = m.Id; l.Matte = TrackMatte.Luma; using var b = Render(Project(l, m)); Near(255, b.GetPixel(20,20).Alpha); });
Test("luma black matte is transparent", () => { var m = Solid("#000000"); m.Enabled = false; var l = Solid(); l.MatteId = m.Id; l.Matte = TrackMatte.Luma; using var b = Render(Project(l, m)); Near(0, b.GetPixel(20,20).Alpha); });
Test("luma gray uses luminance not alpha", () => { var m = Solid("#808080"); m.Enabled = false; var l = Solid(); l.MatteId = m.Id; l.Matte = TrackMatte.Luma; using var b = Render(Project(l,m)); Near(128, b.GetPixel(20,20).Alpha); });
Test("luma multiplies source opacity", () => { var m = Solid("#808080"); m.Enabled = false; m.Transform.Opacity.Value = 50; var l = Solid(); l.MatteId = m.Id; l.Matte = TrackMatte.Luma; using var b = Render(Project(l,m)); Near(64, b.GetPixel(20,20).Alpha); });
Test("inverted luma complements gray", () => { var m = Solid("#404040"); m.Enabled = false; var l = Solid(); l.MatteId = m.Id; l.Matte = TrackMatte.LumaInverted; using var b = Render(Project(l,m)); Near(191, b.GetPixel(20,20).Alpha); });
Test("luma matte follows transformed source", () => { var m = Solid("#FFFFFF",20,20); m.Enabled=false; m.Transform.X.Value=30; var l=Solid(); l.MatteId=m.Id; l.Matte=TrackMatte.Luma; using var b=Render(Project(l,m)); Near(0,b.GetPixel(10,10).Alpha); Near(255,b.GetPixel(35,10).Alpha); });
Test("independent feather leaves a different hard mask sharp", () =>
{
    var l=Solid(); var right=ShapePath.Rectangle(24,64); foreach(var n in right.Nodes) n.Point += new Vec2(40,0);
    l.Masks=[new(){Feather=5,Path=ShapePath.Rectangle(16,64)},new(){Path=right}];
    using var b=Render(Project(l)); Check(b.GetPixel(16,32).Alpha is >20 and <230); Near(255,b.GetPixel(42,32).Alpha); Near(0,b.GetPixel(37,32).Alpha,3);
});
Test("mask opacity applies to coverage", () => {var l=Solid();l.Masks=[new(){Opacity=25,Path=ShapePath.Rectangle(64,64)}];using var b=Render(Project(l));Near(64,b.GetPixel(32,32).Alpha);});
Test("mask opacity union is coverage source over", () => {var l=Solid();l.Masks=[new(){Opacity=50,Path=ShapePath.Rectangle(64,64)},new(){Opacity=50,Path=ShapePath.Rectangle(64,64)}];using var b=Render(Project(l));Near(192,b.GetPixel(32,32).Alpha);});
Test("mask expansion dilates before feather", () => {var l=Solid();l.Masks=[new(){Expansion=5,Path=ShapePath.Rectangle(20,64)}];using var b=Render(Project(l));Near(255,b.GetPixel(23,32).Alpha);Near(0,b.GetPixel(27,32).Alpha);});
Test("negative mask expansion erodes", () => {var l=Solid();l.Masks=[new(){Expansion=-5,Path=ShapePath.Rectangle(30,64)}];using var b=Render(Project(l));Near(255,b.GetPixel(20,32).Alpha);Near(0,b.GetPixel(28,32).Alpha);});
Test("None mask is non-rendering metadata", () => {var l=Solid();l.Masks=[new(){Mode=MaskMode.None,Path=new()}];using var b=Render(Project(l));Near(255,b.GetPixel(32,32).Alpha);});
Test("inverted soft mask complements raster alpha", () => {var l=Solid();l.Masks=[new(){Inverted=true,Feather=4,Path=ShapePath.Rectangle(30,64)}];using var b=Render(Project(l));Check(b.GetPixel(30,32).Alpha is >40 and <220);Near(255,b.GetPixel(55,32).Alpha);});
Test("adjustment inverts the underlying composite", () => {using var b=Render(Project(Adjustment(),Solid()));var p=b.GetPixel(32,32);Near(0,p.Red);Near(255,p.Green);Near(255,p.Alpha);});
Test("adjustment does not affect layers above it", () => {using var b=Render(Project(Solid("#00FF00",20,64),Adjustment(),Solid()));Near(255,b.GetPixel(10,32).Green);Near(0,b.GetPixel(10,32).Blue);Near(255,b.GetPixel(32,32).Blue);});
Test("adjustment at bottom does not generate content", () => {using var b=Render(Project(Adjustment()));Near(0,b.GetPixel(32,32).Alpha);});
Test("adjustment opacity interpolates rather than fades", () => {using var b=Render(Project(Adjustment(50),Solid()));var p=b.GetPixel(32,32);Near(127,p.Red);Near(128,p.Green);Near(255,p.Alpha);});
Test("adjustment preserves partial underlying alpha", () => {var l=Solid();l.Transform.Opacity.Value=50;using var b=Render(Project(Adjustment(50),l));Near(128,b.GetPixel(32,32).Alpha);});
Test("adjustment mask restricts effect footprint", () => {var a=Adjustment();a.Masks=[new(){Path=ShapePath.Rectangle(30,64)}];using var b=Render(Project(a,Solid()));Near(255,b.GetPixel(10,32).Green);Near(255,b.GetPixel(45,32).Red);});
Test("adjustment transform moves its coverage", () => {var a=Adjustment();a.Width=20;a.Transform.X.Value=30;using var b=Render(Project(a,Solid()));Near(255,b.GetPixel(10,32).Red);Near(255,b.GetPixel(35,32).Green);});
Test("multiple adjustment scopes restore in stacking order", () => {using var b=Render(Project(Adjustment(),Adjustment(),Solid()));Near(255,b.GetPixel(32,32).Red);Near(0,b.GetPixel(32,32).Green);});
Test("adjustment track matte uses composition coordinates", () => {var m=Solid("#FFFFFF",20,64);m.Enabled=false;m.Transform.X.Value=30;var a=Adjustment();a.MatteId=m.Id;using var b=Render(Project(a,m,Solid()));Near(255,b.GetPixel(10,32).Red);Near(255,b.GetPixel(35,32).Green);});
Test("disabled adjustment bypasses effects", () => {var a=Adjustment();a.Enabled=false;using var b=Render(Project(a,Solid()));Near(255,b.GetPixel(32,32).Red);});
foreach(var mode in new[]{LayerBlend.Hue,LayerBlend.Saturation,LayerBlend.Color,LayerBlend.Luminosity})
    Test("new HSL blend "+mode,()=>{var l=Solid("#40C090");l.Blend=mode;using var b=Render(Project(l,Solid("#904050")));Near(255,b.GetPixel(32,32).Alpha);});
Test("guide layers display in preview", () => {var l=Solid();l.Guide=true;using var b=Render(Project(l),preview:true);Near(255,b.GetPixel(32,32).Alpha);});
Test("guide layers are excluded from exported PNG", () => {var l=Solid();l.Guide=true;using var b=Render(Project(l));Near(0,b.GetPixel(32,32).Alpha);});
Test("nested guide layers stay excluded from parent preview",()=>{var guide=Solid();guide.Guide=true;var p=Project();var sub=Project(guide).Compositions[0];p.Compositions.Add(sub);p.Compositions[0].Layers.Add(new(){Kind=LayerKind.Composition,SourceId=sub.Id,Width=64,Height=64,OutPoint=1});using var b=Render(p,preview:true);Near(0,b.GetPixel(32,32).Alpha);});
Test("guide solo does not blank exported normal layers",()=>{var guide=Solid();guide.Guide=true;guide.Solo=true;using var b=Render(Project(guide,Solid("#00FF00")));Near(255,b.GetPixel(32,32).Green);});
Test("source remapping evaluates composition seconds",()=>{var l=Solid();l.Kind=LayerKind.Composition;l.TimeRemapEnabled=true;l.TimeRemap.SetKey(0,.8,Interpolation.Linear);l.TimeRemap.SetKey(1,.2,Interpolation.Linear);Near(.5,LayerTime.Evaluate(l,.5),1e-8);});
Test("nested remapping changes visible source frame",()=>{var moving=Solid("#00FF00",10,10);moving.Transform.X.SetKey(0,0,Interpolation.Linear);moving.Transform.X.SetKey(1,50,Interpolation.Linear);var sub=Project(moving).Compositions[0];var wrap=new Layer{Kind=LayerKind.Composition,SourceId=sub.Id,Width=64,Height=64,OutPoint=1,TimeRemapEnabled=true,TimeRemap=new(.8)};var p=Project(wrap);p.Compositions.Add(sub);using var b=Render(p,.1);Near(255,b.GetPixel(43,5).Green);Near(0,b.GetPixel(8,5).Alpha);});
Test("time remap keys duplicate with fresh identifiers",()=>{var p=SampleProject.Create();var s=new EditorSession(p);s.Select(s.Composition.Layers.First(l=>l.Kind==LayerKind.Composition).Id);s.EnableTimeRemap();s.DuplicateSelection();ProjectValidator.Validate(s.Project);});
Test("freeze frame has a single undo transaction",()=>{var s=new EditorSession(SampleProject.Create());s.Select(s.Composition.Layers.First(l=>l.Kind==LayerKind.Composition).Id);s.SetTime(2);s.FreezeFrame();Near(2,LayerTime.Evaluate(s.Primary!,5),1e-9);s.Undo();Check(!s.Primary!.TimeRemapEnabled);});
Test("reverse frame initialization and undo",()=>{var s=new EditorSession(SampleProject.Create());s.Select(s.Composition.Layers.First(l=>l.Kind==LayerKind.Composition).Id);s.ReverseTime();Near(s.Composition.LastFrameTime,LayerTime.Evaluate(s.Primary!,0),1e-8);s.Undo();Check(!s.Primary!.TimeRemapEnabled);});
Test("reverse remap Bezier preserves time reflection",()=>{var s=new EditorSession(SampleProject.Create());s.Select(s.Composition.Layers.First(l=>l.Kind==LayerKind.Composition).Id);s.EnableTimeRemap();s.Edit("curve",()=>{var c=s.Primary!.TimeRemap;c.Keys.Clear();c.SetKey(1,0);c.SetKey(3,2);c.Keys[0].X1=.12;c.Keys[0].Y1=-.1;c.Keys[0].X2=.4;c.Keys[0].Y2=.7;});var expected=LayerTime.Evaluate(s.Primary!,1.4);s.ReverseTime();Near(expected,LayerTime.Evaluate(s.Primary!,2.6),1e-7);});
Test("old schema receives safe additive defaults",()=>{var p=Project(Solid());var json=ProjectJson.Save(p).Replace("\"opacity\":100,","");var q=ProjectJson.Load(json);Check(!q.Compositions[0].Layers[0].Guide);Check(!q.Compositions[0].Layers[0].TimeRemapEnabled);});
Test("mask validation rejects invalid opacity",()=>{var p=Project(Solid());p.Compositions[0].Layers[0].Masks.Add(new(){Opacity=-1});Throws(()=>ProjectValidator.Validate(p));});
Test("mask validation rejects unbounded expansion",()=>{var p=Project(Solid());p.Compositions[0].Layers[0].Masks.Add(new(){Expansion=900});Throws(()=>ProjectValidator.Validate(p));});
Test("indexed transforms match reference parenting",()=>{var c=new Composition();for(var i=0;i<30;i++){var l=Solid();l.Transform.X.Value=i;l.Transform.Rotation.Value=i;if(i>0)l.ParentId=c.Layers[^1].Id;c.Layers.Add(l);}var f=new CompositionFrame(c,0);foreach(var item in f.Layers){var m=TransformEvaluator.World(c,item.Layer,0);Near(m.M31,item.World.M31,.01);Near(m.M32,item.World.M32,.01);}Check(f.TransformEvaluations==30);});
Test("shared parents evaluated once for 2000 children",()=>{var c=new Composition();var parent=Solid();parent.Kind=LayerKind.Null;c.Layers.Add(parent);for(var i=0;i<2000;i++)c.Layers.Add(new(){ParentId=parent.Id});var f=new CompositionFrame(c,0);Check(f.TransformEvaluations==2001);});
Test("indexed expression indices follow original layer order",()=>{var a=Solid();var b=Solid();b.Transform.X.Expression="index * 10";var f=new CompositionFrame(Project(a,b).Compositions[0],0);Near(20,f.Find(b.Id)!.World.M31,1e-8);});
Test("optimized ordinary layers pixel-match isolated reference",()=>SamePixels(SampleProject.Create(),2.4));
Test("nested blend isolation is retained",()=>{var top=Solid("#808080");top.Blend=LayerBlend.Multiply;var sub=Project(top).Compositions[0];var wrap=new Layer{Kind=LayerKind.Composition,SourceId=sub.Id,Width=64,Height=64,OutPoint=1};var p=Project(wrap,Solid("#FF0000"));p.Compositions.Add(sub);SamePixels(p);using var b=Render(p);Near(128,b.GetPixel(32,32).Green);});
Test("ordinary opaque layers avoid saveLayer",()=>{using var r=new SkiaCompositor();var p=Project(Solid());using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.DirectDraws==1 && r.Metrics.IsolationLayers==0);});
Test("partial opacity still isolates overlapping content",()=>{var l=Solid();l.StrokeWidth=10;l.Transform.Opacity.Value=50;var p=Project(l);SamePixels(p);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.DirectDraws==0 && r.Metrics.IsolationLayers==1);});
Test("offscreen simple layers are conservatively culled",()=>{var l=Solid();l.Transform.X.Value=10000;var p=Project(l);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.CulledLayers==1 && r.Metrics.LayerDraws==0);});
Test("static filter and geometry resources reuse native objects",()=>{var l=Solid();l.Effects.Add(EffectCatalog.Create(EffectKind.Invert));var p=Project(l);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);r.Render(s.Canvas,p,p.Compositions[0],.5);Check(r.Metrics.FilterBuilds==0 && r.Metrics.FilterHits==1 && r.Metrics.GeometryBuilds==0);});
Test("in-place path mutation invalidates geometry",()=>{var l=Solid();l.Kind=LayerKind.Path;l.Path=ShapePath.Rectangle(30,30);var p=Project(l);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);l.Path.Nodes[1].Point=new(50,0);r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.GeometryBuilds==1);});
Test("animated filter parameter invalidates native filter",()=>{var l=Solid();var fx=EffectCatalog.Create(EffectKind.Exposure);fx.Parameters["Exposure"].SetKey(0,0,Interpolation.Linear);fx.Parameters["Exposure"].SetKey(1,1,Interpolation.Linear);l.Effects.Add(fx);var p=Project(l);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);r.Render(s.Canvas,p,p.Compositions[0],.5);Check(r.Metrics.FilterBuilds==1);});
Test("native resource cache bounded after many layers",()=>{var p=Project(Enumerable.Range(0,700).Select(_=>Solid()).ToArray());using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.CachedLayerResources<=512);r.ClearResources();Check(r.CachedLayerResources==0);});
Test("same source time shares nested frame evaluation",()=>{var sub=Project(Solid()).Compositions[0];var a=new Layer{Kind=LayerKind.Composition,SourceId=sub.Id,Width=64,Height=64,OutPoint=1};var b=ProjectJson.CloneLayer(a);b.Id=Guid.NewGuid().ToString("N");var p=Project(a,b);p.Compositions.Add(sub);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.CompositionEvaluations==2);});
Test("render call reflects parent mutation without stale frame cache",()=>{var parent=Solid();parent.Kind=LayerKind.Null;var child=Solid();child.ParentId=parent.Id;var p=Project(parent,child);using var r=new SkiaCompositor();using var s=SKSurface.Create(new SKImageInfo(64,64));r.Render(s.Canvas,p,p.Compositions[0],0);parent.Transform.X.Value=500;r.Render(s.Canvas,p,p.Compositions[0],0);Check(r.Metrics.CulledLayers==1);});

if (failed == 0)
{
    // CPU-raster submission benchmark, not a physical-GPU or After Effects comparison.
    var p=Project(); var c=p.Compositions[0]; c.Width=512;c.Height=288;
    for(var i=0;i<400;i++){var l=Solid(i%2==0?"#AA88EE":"#55BBAA",14,10);l.Kind=LayerKind.Rectangle;l.CornerRadius=3;l.Transform.X.Value=(i%25)*20;l.Transform.Y.Value=(i/25)*16;c.Layers.Add(l);}
    object Measure(bool optimized)
    {
        using var renderer=new SkiaCompositor{EnableOptimizations=optimized};using var surface=SKSurface.Create(new SKImageInfo(512,288));
        void Draw(){surface.Canvas.Clear(SKColors.Transparent);renderer.Render(surface.Canvas,p,c,.5);surface.Canvas.Flush();}
        for(var i=0;i<5;i++)Draw();var results=new List<double>();long bytes=0;
        for(var round=0;round<7;round++){var before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(var j=0;j<5;j++)Draw();results.Add(watch.Elapsed.TotalMilliseconds/5);bytes+=(GC.GetAllocatedBytesForCurrentThread()-before)/5;}
        results.Sort();return new{optimized,medianMilliseconds=results[3],meanAllocatedBytes=bytes/7,layers=c.Layers.Count,metrics=renderer.Metrics};
    }
    var report=new{methodology="CPU raster, 512x288, 400 rounded rectangles, 5 warmups, 7 rounds of 5 frames; baseline disables native resource caching and direct-draw shortcuts; both use indexed evaluation",reference=Measure(false),optimized=Measure(true)};
    Directory.CreateDirectory("artifacts/performance");File.WriteAllText("artifacts/performance/compositor.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine(JsonSerializer.Serialize(report));
}
Console.WriteLine($"{passed} compositing/performance tests passed; {failed} failed.");
return failed==0?0:1;
