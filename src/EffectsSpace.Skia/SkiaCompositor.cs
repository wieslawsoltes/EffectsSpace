using System.Numerics;
using EffectsSpace.Animation;
using EffectsSpace.Core;
using EffectsSpace.Rendering;
using SkiaSharp;

namespace EffectsSpace.Skia;

/// <summary>Renders directly into the host's Skia canvas. Preview never uploads a CPU-rendered full frame.</summary>
public sealed class SkiaCompositor : IDisposable
{
    private sealed record CachedImage(SKImage Image, long Bytes, long Stamp);
    private readonly Dictionary<string, CachedImage> _images = new(StringComparer.Ordinal);
    private long _stamp, _bytes;
    public SKTypeface Typeface { get; set; } = SKTypeface.Default;
    public long ImageCacheLimit { get; set; } = 128 * 1024 * 1024;
    public long CachedImageBytes => _bytes;
    public int MotionBlurSamples { get; set; } = 1;
    public RenderBudget Budget { get; set; } = RenderBudget.Default;
    public HashSet<string> MissingVideoFrames { get; } = new(StringComparer.Ordinal);

    public void Render(SKCanvas canvas, MotionProject project, Composition composition, double time)
    {
        MissingVideoFrames.Clear(); Budget.Check(composition.Width, composition.Height);
        var samples = Math.Clamp(MotionBlurSamples, 1, 8);
        if (samples == 1) { RenderComposition(canvas, project, composition, time, 0); return; }
        var saved = canvas.Save();
        try
        {
            canvas.SaveLayer();
            for (var i = 0; i < samples; i++)
            {
                using var sample = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255d / samples)), BlendMode = SKBlendMode.Plus };
                canvas.SaveLayer(sample);
                var t = time + ((i + .5) / samples - .5) * composition.FrameRate.Seconds(1) * .5;
                RenderComposition(canvas, project, composition, Math.Clamp(t, 0, composition.LastFrameTime), 0);
                canvas.Restore();
            }
            canvas.Restore();
        }
        finally { canvas.RestoreToCount(saved); }
    }
    private void RenderComposition(SKCanvas canvas, MotionProject project, Composition composition, double time, int depth)
    {
        if (depth > Budget.MaximumNesting) throw new InvalidOperationException("Composition render nesting exceeded.");
        var save = canvas.Save();
        try
        {
            canvas.ClipRect(SKRect.Create(composition.Width, composition.Height));
            foreach (var item in RenderPlanner.Build(composition, time).Layers) DrawLayer(canvas, project, composition, item.Layer, time, depth, false);
        }
        finally { canvas.RestoreToCount(save); }
    }
    private void DrawLayer(SKCanvas canvas, MotionProject project, Composition comp, Layer layer, double time, int depth, bool matte)
    {
        if (depth > Budget.MaximumNesting || time < layer.InPoint || time >= layer.OutPoint) return;
        var saved = canvas.Save(); var compMatrix = canvas.TotalMatrix;
        try
        {
            var matrix = Matrix(TransformEvaluator.World(comp, layer, time)); canvas.Concat(ref matrix);
            using var filter = EffectPipeline.Build(layer.Effects, time);
            using var paint = new SKPaint { IsAntialias = true, ImageFilter = filter, Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(CurveEvaluator.Evaluate(layer.Transform.Opacity, time, comp.Layers.IndexOf(layer) + 1), 0, 100) * 2.55)), BlendMode = matte ? SKBlendMode.SrcOver : Blend(layer.Blend) };
            canvas.SaveLayer(paint);
            DrawContent(canvas, project, layer, time, depth);
            ApplyMasks(canvas, layer);
            if (layer.MatteId is { } matteId && comp.Layers.FirstOrDefault(l => l.Id == matteId) is { } source)
            {
                using var maskPaint = new SKPaint { BlendMode = layer.Matte == TrackMatte.Alpha ? SKBlendMode.DstIn : SKBlendMode.DstOut };
                canvas.SaveLayer(maskPaint); canvas.SetMatrix(compMatrix);
                DrawLayer(canvas, project, comp, source, time, depth + 1, true); canvas.Restore();
            }
            canvas.Restore();
        }
        finally { canvas.RestoreToCount(saved); }
    }
    private void DrawContent(SKCanvas canvas, MotionProject project, Layer layer, double time, int depth)
    {
        var rect = SKRect.Create((float)layer.Width, (float)layer.Height);
        using var fill = new SKPaint { IsAntialias = true, Color = SKColor.Parse(layer.Fill) };
        using var gradient = layer.GradientEnd is { } end ? SKShader.CreateLinearGradient(new(0, 0), new(rect.Width, rect.Height), [SKColor.Parse(layer.Fill), SKColor.Parse(end)], SKShaderTileMode.Clamp) : null;
        fill.Shader = gradient;
        var noiseEffect = layer.Effects.LastOrDefault(e => e.Enabled && e.Kind == EffectKind.FractalNoise);
        using var noise = noiseEffect is null ? null : SKShader.CreatePerlinNoiseFractalNoise(1 / EffectPipeline.Parameter(noiseEffect, "Scale", time), 1 / EffectPipeline.Parameter(noiseEffect, "Scale", time), (int)EffectPipeline.Parameter(noiseEffect, "Complexity", time), EffectPipeline.Parameter(noiseEffect, "Evolution", time));
        if (noise is not null) fill.Shader = noise;
        if (layer.Kind == LayerKind.Composition)
        {
            var source = project.Compositions.FirstOrDefault(c => c.Id == layer.SourceId); var sourceTime = layer.SourceTime(time);
            if (source is not null && sourceTime >= 0 && sourceTime < source.Duration)
            {
                var count = canvas.Save(); canvas.Scale((float)layer.Width / source.Width, (float)layer.Height / source.Height);
                RenderComposition(canvas, project, source, sourceTime, depth + 1); canvas.RestoreToCount(count);
            }
        }
        else if (layer.Kind is LayerKind.Image or LayerKind.Video)
        {
            var asset = project.Assets.FirstOrDefault(a => a.Id == layer.SourceId);
            var image = asset is null ? null : layer.Kind == LayerKind.Image ? GetImage(asset) : FindImage(asset.Id + ":video");
            if (image is not null) canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            else if (layer.Kind == LayerKind.Video && asset is not null) MissingVideoFrames.Add(asset.Id);
        }
        else if (layer.Kind == LayerKind.Text)
        {
            using var font = new SKFont(Typeface, (float)layer.FontSize) { Edging = SKFontEdging.Antialias, Subpixel = true };
            var lines = layer.Text.Replace("\r", "").Split('\n');
            for (var i = 0; i < lines.Length; i++) canvas.DrawText(lines[i], 0, (float)(layer.FontSize * .88 + i * layer.FontSize * 1.18), SKTextAlign.Left, font, fill);
        }
        else if (layer.Kind is not (LayerKind.Null or LayerKind.Audio))
        {
            using var path = PathGeometry.LayerPath(layer);
            if (layer.FillEnabled) canvas.DrawPath(path, fill);
            if (layer.StrokeWidth > 0)
            { using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = (float)layer.StrokeWidth, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round, Color = SKColor.Parse(layer.Stroke) }; canvas.DrawPath(path, stroke); }
        }
        var vignette = layer.Effects.LastOrDefault(e => e.Enabled && e.Kind == EffectKind.Vignette);
        if (vignette is not null)
        {
            using var shader = SKShader.CreateRadialGradient(new(rect.MidX, rect.MidY), Math.Max(rect.Width, rect.Height) * .65f, [SKColors.Transparent, SKColors.Transparent, SKColors.Black.WithAlpha((byte)(EffectPipeline.Parameter(vignette, "Amount", time) * 2.55f))], [0, .35f, 1], SKShaderTileMode.Clamp);
            using var overlay = new SKPaint { Shader = shader, BlendMode = SKBlendMode.SrcATop, IsAntialias = true }; canvas.DrawRect(rect, overlay);
        }
    }
    private static void ApplyMasks(SKCanvas canvas, Layer layer)
    {
        var masks = layer.Masks.Where(m => m.Enabled).ToArray(); if (masks.Length == 0) return;
        using var combined = new SKPath(); var first = true;
        foreach (var mask in masks)
        {
            using var path = PathGeometry.Build(mask.Path);
            if (mask.Inverted)
            {
                using var full = new SKPath(); full.AddRect(SKRect.Create((float)layer.Width, (float)layer.Height));
                using var inverted = new SKPath(); if (full.Op(path, SKPathOp.Difference, inverted)) { path.Reset(); path.AddPath(inverted); }
            }
            if (first)
            {
                if (mask.Mode == MaskMode.Subtract) { combined.AddRect(SKRect.Create((float)layer.Width, (float)layer.Height)); using var result = new SKPath(); combined.Op(path, SKPathOp.Difference, result); combined.Reset(); combined.AddPath(result); }
                else combined.AddPath(path);
                first = false;
            }
            else
            {
                using var result = new SKPath();
                var op = mask.Mode switch { MaskMode.Add => SKPathOp.Union, MaskMode.Subtract => SKPathOp.Difference, _ => SKPathOp.Intersect };
                if (combined.Op(path, op, result)) { combined.Reset(); combined.AddPath(result); }
            }
        }
        using var composite = new SKPaint { BlendMode = SKBlendMode.DstIn };
        canvas.SaveLayer(composite);
        var feather = (float)masks.Max(m => m.Feather);
        using var blur = feather > 0 ? SKImageFilter.CreateBlur(feather, feather) : null;
        using var maskFill = new SKPaint { Color = SKColors.White, IsAntialias = true, ImageFilter = blur };
        canvas.DrawPath(combined, maskFill); canvas.Restore();
    }
    private SKImage? GetImage(MediaAsset asset)
    {
        var existing = FindImage(asset.Id); if (existing is not null) return existing;
        var image = DecodeImage(asset.Data); PutImage(asset.Id, image); return image;
    }
    private SKImage? FindImage(string key)
    {
        if (!_images.TryGetValue(key, out var item)) return null;
        _images[key] = item with { Stamp = ++_stamp }; return item.Image;
    }
    private void PutImage(string key, SKImage image)
    {
        if (_images.Remove(key, out var old)) { _bytes -= old.Bytes; old.Image.Dispose(); }
        var bytes = (long)image.Width * image.Height * 4;
        while (_images.Count > 0 && _bytes + bytes > ImageCacheLimit)
        { var oldest = _images.MinBy(p => p.Value.Stamp); _images.Remove(oldest.Key); _bytes -= oldest.Value.Bytes; oldest.Value.Image.Dispose(); }
        _images[key] = new(image, bytes, ++_stamp); _bytes += bytes;
    }
    public void SetVideoFrame(string assetId, byte[] png) => PutImage(assetId + ":video", DecodeImage(png));
    public SKImage DecodeImage(byte[] data)
    {
        var (w, h) = ImageInfo(data); Budget.Check(w, h);
        return SKImage.FromEncodedData(data) ?? throw new InvalidDataException("The image could not be decoded.");
    }
    public static (int Width, int Height) ImageInfo(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 32 * 1024 * 1024) throw new InvalidDataException("Image must be between 1 byte and 32 MiB.");
        using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
        if (codec is null) throw new InvalidDataException("Unsupported or corrupt image.");
        RenderBudget.Default.Check(codec.Info.Width, codec.Info.Height); return (codec.Info.Width, codec.Info.Height);
    }
    public void ClearImages() { foreach (var i in _images.Values) i.Image.Dispose(); _images.Clear(); _bytes = 0; }
    public void Dispose() => ClearImages();
    public static SKMatrix Matrix(Matrix3x2 m) => new() { ScaleX = m.M11, SkewX = m.M21, TransX = m.M31, SkewY = m.M12, ScaleY = m.M22, TransY = m.M32, Persp2 = 1 };
    public static SKBlendMode Blend(LayerBlend blend) => blend switch
    {
        LayerBlend.Multiply => SKBlendMode.Multiply, LayerBlend.Screen => SKBlendMode.Screen, LayerBlend.Add => SKBlendMode.Plus,
        LayerBlend.Overlay => SKBlendMode.Overlay, LayerBlend.SoftLight => SKBlendMode.SoftLight, LayerBlend.HardLight => SKBlendMode.HardLight,
        LayerBlend.Difference => SKBlendMode.Difference, LayerBlend.Darken => SKBlendMode.Darken, LayerBlend.Lighten => SKBlendMode.Lighten,
        LayerBlend.ColorDodge => SKBlendMode.ColorDodge, LayerBlend.ColorBurn => SKBlendMode.ColorBurn, LayerBlend.Exclusion => SKBlendMode.Exclusion, _ => SKBlendMode.SrcOver
    };
}
