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

    private readonly LayerResourceCache _resources = new();
    private readonly Dictionary<(Composition Composition, double Time, bool Guides), CompositionFrame> _frames = [];
    private readonly Dictionary<string, Composition> _compositions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MediaAsset> _assets = new(StringComparer.Ordinal);
    private bool _disposed;
    public bool EnableOptimizations { get; set; } = true;
    public RenderMetrics Metrics { get; private set; } = new();
    public int CachedLayerResources => _resources.Count;

    public void Render(SKCanvas canvas, MotionProject project, Composition composition, double time, bool includeGuides = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Metrics = new(); MissingVideoFrames.Clear(); _frames.Clear(); _compositions.Clear(); _assets.Clear();
        foreach (var item in project.Compositions) _compositions.Add(item.Id, item);
        foreach (var asset in project.Assets) _assets.Add(asset.Id, asset);
        Budget.Check(composition.Width, composition.Height);
        var saved = canvas.Save();
        try
        {
            var samples = Math.Clamp(MotionBlurSamples, 1, 8);
            if (samples == 1) { RenderComposition(canvas, project, composition, time, 0, includeGuides); return; }
            canvas.SaveLayer(); Metrics.IsolationLayers++;
            for (var i = 0; i < samples; i++)
            {
                // Distribute the 255 integer alpha levels exactly (no darkening from 255/N rounding).
                var alpha = 255 / samples + (i < 255 % samples ? 1 : 0);
                using var sample = new SKPaint { Color = SKColors.White.WithAlpha((byte)alpha), BlendMode = SKBlendMode.Plus };
                canvas.SaveLayer(sample); Metrics.IsolationLayers++;
                var t = time + ((i + .5) / samples - .5) * composition.FrameRate.Seconds(1) * .5;
                RenderComposition(canvas, project, composition, Math.Clamp(t, 0, composition.LastFrameTime), 0, includeGuides);
                canvas.Restore();
            }
            canvas.Restore();
        }
        finally
        {
            canvas.RestoreToCount(saved);
            Metrics.TransformEvaluations = _frames.Values.Sum(f => f.TransformEvaluations);
            _frames.Clear(); _resources.Trim();
            Metrics.SubmissionMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
    }

    private CompositionFrame Frame(Composition composition, double time, bool guides)
    {
        var key = (composition, time, guides);
        if (!_frames.TryGetValue(key, out var frame))
        {
            frame = new CompositionFrame(composition, time, guides, Budget.MaximumNesting);
            _frames.Add(key, frame); Metrics.CompositionEvaluations++;
        }
        return frame;
    }

    private void RenderComposition(SKCanvas canvas, MotionProject project, Composition composition, double time, int depth, bool guides = false)
    {
        if (depth > Budget.MaximumNesting) throw new InvalidOperationException("Composition render nesting exceeded.");
        var save = canvas.Save();
        try
        {
            canvas.ClipRect(SKRect.Create(composition.Width, composition.Height));
            var frame = Frame(composition, time, guides);
            // Open effect scopes top-to-bottom, then draw once bottom-to-top. Each adjustment
            // consumes the actual composite below it, without replaying an exponentially growing prefix.
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            for (var i = frame.Layers.Count - 1; i >= 0; i--)
            {
                var item = frame.Layers[i];
                if (item.Layer.Kind != LayerKind.Adjustment || item.Opacity <= 0) continue;
                using var adjustment = BuildAdjustment(project, frame, item, depth);
                if (adjustment is null) continue;
                using var paint = new SKPaint { ImageFilter = adjustment };
                canvas.SaveLayer(SKRect.Create(composition.Width, composition.Height), paint);
                scopes.Add(item.Layer.Id); Metrics.IsolationLayers++; Metrics.AdjustmentLayers++;
            }
            foreach (var item in frame.Layers)
            {
                if (item.Layer.Kind == LayerKind.Adjustment) { if (scopes.Contains(item.Layer.Id)) canvas.Restore(); continue; }
                DrawLayer(canvas, project, frame, item, depth, false);
            }
        }
        finally { canvas.RestoreToCount(save); }
    }

    private SKImageFilter? BuildAdjustment(MotionProject project, CompositionFrame frame, RenderLayer item, int depth)
    {
        if (item.Layer.Blend != LayerBlend.Normal)
            throw new NotSupportedException("Adjustment layers currently require Normal blending.");
        if (item.Layer.Effects.Any(e => e.Enabled && e.Kind is EffectKind.FractalNoise or EffectKind.Vignette))
            throw new NotSupportedException("Fill generators are not supported on adjustment layers.");
        using var filter = EffectPipeline.Build(item.Layer.Effects, frame.Time);
        if (filter is null) return null;
        using var recorder = new SKPictureRecorder();
        var bounds = SKRect.Create(frame.Composition.Width, frame.Composition.Height);
        var maskCanvas = recorder.BeginRecording(bounds);
        var matrix = Matrix(item.World); maskCanvas.Concat(in matrix);
        using var opacity = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(item.Opacity * 255)) };
        maskCanvas.SaveLayer(opacity);
        using (var white = new SKPaint { Color = SKColors.White, IsAntialias = true })
            maskCanvas.DrawRect(SKRect.Create((float)item.Layer.Width, (float)item.Layer.Height), white);
        MaskRenderer.Apply(maskCanvas, item.Layer, Metrics);
        ApplyTrackMatte(maskCanvas, project, frame, item, SKMatrix.Identity, depth);
        maskCanvas.Restore();
        using var picture = recorder.EndRecording();
        using var mask = SKImageFilter.CreatePicture(picture, bounds);
        using var affected = SKImageFilter.CreateBlendMode(SKBlendMode.DstIn, filter, mask);
        using var unchanged = SKImageFilter.CreateBlendMode(SKBlendMode.DstOut, null, mask);
        // Premultiplied lerp: filtered * coverage + original * (1 - coverage), including alpha.
        return SKImageFilter.CreateBlendMode(SKBlendMode.Plus, unchanged, affected);
    }

    private void DrawLayer(SKCanvas canvas, MotionProject project, CompositionFrame frame, RenderLayer item, int depth, bool matte)
    {
        var layer = item.Layer; var time = frame.Time;
        if (depth > Budget.MaximumNesting) throw new InvalidOperationException("Matte render nesting exceeded.");
        if (time < layer.InPoint || time >= layer.OutPoint || item.Opacity <= 0 || layer.Kind is LayerKind.Null or LayerKind.Audio) return;
        if (layer.Kind == LayerKind.Adjustment) throw new NotSupportedException("Adjustment layers cannot be used as a matte source.");
        var saved = canvas.Save(); var compMatrix = canvas.TotalMatrix;
        try
        {
            var matrix = Matrix(item.World); canvas.Concat(in matrix);
            var masks = MaskRenderer.HasMasks(layer);
            var simple = item.Opacity >= 1 && (matte || layer.Blend == LayerBlend.Normal)
                && layer.Kind != LayerKind.Composition && layer.MatteId is null && !masks && !layer.Effects.Any(e => e.Enabled);
            if (EnableOptimizations && simple && layer.Kind is LayerKind.Solid or LayerKind.Rectangle or LayerKind.Ellipse or LayerKind.Star or LayerKind.Path)
            {
                var geometry = _resources.Get(layer).GetGeometry(layer, Metrics);
                var bounds = geometry.Bounds; bounds.Inflate((float)layer.StrokeWidth / 2 + 1, (float)layer.StrokeWidth / 2 + 1);
                if (canvas.QuickReject(bounds)) { Metrics.CulledLayers++; return; }
            }
            Metrics.LayerDraws++;
            if (EnableOptimizations && simple)
            {
                Metrics.DirectDraws++; DrawContent(canvas, project, layer, time, depth, item.SourceTime); return;
            }
            SKImageFilter? filter = null;
            try
            {
                filter = EnableOptimizations ? _resources.Get(layer).GetFilter(layer, time, Metrics) : EffectPipeline.Build(layer.Effects, time);
                using var paint = new SKPaint { IsAntialias = true, ImageFilter = filter,
                    Color = SKColors.White.WithAlpha((byte)Math.Round(item.Opacity * 255)), BlendMode = matte ? SKBlendMode.SrcOver : Blend(layer.Blend) };
                canvas.SaveLayer(paint); Metrics.IsolationLayers++;
                DrawContent(canvas, project, layer, time, depth, item.SourceTime);
                MaskRenderer.Apply(canvas, layer, Metrics);
                ApplyTrackMatte(canvas, project, frame, item, compMatrix, depth);
                canvas.Restore();
            }
            finally { if (!EnableOptimizations) filter?.Dispose(); }
        }
        finally { canvas.RestoreToCount(saved); }
    }

    private void ApplyTrackMatte(SKCanvas canvas, MotionProject project, CompositionFrame frame, RenderLayer item, SKMatrix compositionMatrix, int depth)
    {
        if (item.Layer.MatteId is not { } id || frame.Find(id) is not { } source) return;
        var inverted = item.Layer.Matte is TrackMatte.AlphaInverted or TrackMatte.LumaInverted;
        var isLuma = item.Layer.Matte is TrackMatte.Luma or TrackMatte.LumaInverted;
        using var luma = isLuma ? SKColorFilter.CreateLumaColor() : null;
        using var paint = new SKPaint { BlendMode = inverted ? SKBlendMode.DstOut : SKBlendMode.DstIn, ColorFilter = luma };
        canvas.SaveLayer(paint); Metrics.IsolationLayers++;
        canvas.SetMatrix(compositionMatrix);
        DrawLayer(canvas, project, frame, source, depth + 1, true);
        canvas.Restore();
    }
    private void DrawContent(SKCanvas canvas, MotionProject project, Layer layer, double time, int depth, double sourceTime)
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
            var source = layer.SourceId is { } sourceId ? _compositions.GetValueOrDefault(sourceId) : null;
            if (source is not null && sourceTime >= 0 && sourceTime < source.Duration)
            {
                var count = canvas.Save(); canvas.Scale((float)layer.Width / source.Width, (float)layer.Height / source.Height);
                RenderComposition(canvas, project, source, sourceTime, depth + 1); canvas.RestoreToCount(count);
            }
        }
        else if (layer.Kind is LayerKind.Image or LayerKind.Video)
        {
            var asset = layer.SourceId is { } sourceId ? _assets.GetValueOrDefault(sourceId) : null;
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
            using var uncached = EnableOptimizations ? null : PathGeometry.LayerPath(layer);
            var path = uncached ?? _resources.Get(layer).GetGeometry(layer, Metrics);
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
    public void ClearResources() { ClearImages(); _resources.Clear(); }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; ClearResources(); _frames.Clear(); _compositions.Clear(); _assets.Clear();
    }
    public static SKMatrix Matrix(Matrix3x2 m) => new() { ScaleX = m.M11, SkewX = m.M21, TransX = m.M31, SkewY = m.M12, ScaleY = m.M22, TransY = m.M32, Persp2 = 1 };
    public static SKBlendMode Blend(LayerBlend blend) => blend switch
    {
        LayerBlend.Multiply => SKBlendMode.Multiply, LayerBlend.Screen => SKBlendMode.Screen, LayerBlend.Add => SKBlendMode.Plus,
        LayerBlend.Overlay => SKBlendMode.Overlay, LayerBlend.SoftLight => SKBlendMode.SoftLight, LayerBlend.HardLight => SKBlendMode.HardLight,
        LayerBlend.Difference => SKBlendMode.Difference, LayerBlend.Darken => SKBlendMode.Darken, LayerBlend.Lighten => SKBlendMode.Lighten,
        LayerBlend.ColorDodge => SKBlendMode.ColorDodge, LayerBlend.ColorBurn => SKBlendMode.ColorBurn, LayerBlend.Exclusion => SKBlendMode.Exclusion,
        LayerBlend.Hue => SKBlendMode.Hue, LayerBlend.Saturation => SKBlendMode.Saturation,
        LayerBlend.Color => SKBlendMode.Color, LayerBlend.Luminosity => SKBlendMode.Luminosity, _ => SKBlendMode.SrcOver
    };
}
