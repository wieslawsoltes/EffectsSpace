using EffectsSpace.Core;
using SkiaSharp;

namespace EffectsSpace.Skia;

/// <summary>Independent raster coverage for each mask. Feather/expansion never leak into a different mask.</summary>
internal static class MaskRenderer
{
    public static bool HasMasks(Layer layer) => layer.Masks.Any(m => m.Enabled && m.Mode != MaskMode.None);

    public static void Apply(SKCanvas canvas, Layer layer, RenderMetrics metrics)
    {
        if (!HasMasks(layer)) return;
        var save = canvas.Save();
        try
        {
            using var composite = new SKPaint { BlendMode = SKBlendMode.DstIn };
            canvas.SaveLayer(composite); metrics.IsolationLayers++;
            var first = true;
            foreach (var mask in layer.Masks)
            {
                if (!mask.Enabled || mask.Mode == MaskMode.None) continue;
                if (first && mask.Mode is MaskMode.Subtract or MaskMode.Intersect)
                {
                    using var white = new SKPaint { Color = SKColors.White };
                    canvas.DrawRect(SKRect.Create((float)layer.Width, (float)layer.Height), white);
                }
                var mode = mask.Mode switch { MaskMode.Subtract => SKBlendMode.DstOut, MaskMode.Intersect => SKBlendMode.DstIn, _ => SKBlendMode.SrcOver };
                using var combine = new SKPaint { BlendMode = mode, Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(mask.Opacity, 0, 100) * 2.55)) };
                canvas.SaveLayer(combine); metrics.IsolationLayers++;
                DrawCoverage(canvas, layer, mask, metrics);
                canvas.Restore(); first = false;
            }
            canvas.Restore();
        }
        finally { canvas.RestoreToCount(save); }
    }

    private static void DrawCoverage(SKCanvas canvas, Layer layer, LayerMask mask, RenderMetrics metrics)
    {
        using var path = PathGeometry.Build(mask.Path);
        using var morphology = mask.Expansion > 0
            ? SKImageFilter.CreateDilate((float)mask.Expansion, (float)mask.Expansion)
            : mask.Expansion < 0 ? SKImageFilter.CreateErode((float)-mask.Expansion, (float)-mask.Expansion) : null;
        using var feather = mask.Feather > 0 ? SKImageFilter.CreateBlur((float)mask.Feather, (float)mask.Feather, morphology) : null;
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White, ImageFilter = feather ?? morphology };
        if (!mask.Inverted) { canvas.DrawPath(path, paint); return; }
        // Invert after expanding and feathering. Complement coverage is bounded by the layer rectangle.
        using var white = new SKPaint { Color = SKColors.White };
        canvas.DrawRect(SKRect.Create((float)layer.Width, (float)layer.Height), white);
        using var subtract = new SKPaint { BlendMode = SKBlendMode.DstOut };
        canvas.SaveLayer(subtract); metrics.IsolationLayers++;
        canvas.DrawPath(path, paint); canvas.Restore();
    }
}
