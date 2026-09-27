using EffectsSpace.Animation;
using EffectsSpace.Core;
using SkiaSharp;

namespace EffectsSpace.Skia;

public static class EffectPipeline
{
    public static float Parameter(LayerEffect effect, string key, double time, double fallback = 0)
    {
        var definition = EffectCatalog.Get(effect.Kind).Parameters.FirstOrDefault(p => p.Key == key);
        var value = effect.Parameters.TryGetValue(key, out var c) ? CurveEvaluator.Evaluate(c, time) : definition?.Default ?? fallback;
        return (float)(definition is null ? value : Math.Clamp(value, definition.Minimum, definition.Maximum));
    }
    public static SKImageFilter? Build(IEnumerable<LayerEffect> effects, double time)
    {
        SKImageFilter? chain = null;
        foreach (var e in effects.Where(e => e.Enabled))
        {
            var next = Filter(e, time); if (next is null) continue;
            if (chain is null) chain = next;
            else { var combined = SKImageFilter.CreateCompose(next, chain); chain.Dispose(); next.Dispose(); chain = combined; }
        }
        return chain;
    }
    private static SKImageFilter? Filter(LayerEffect e, double time)
    {
        float P(string key, double fallback = 0) => Parameter(e, key, time, fallback);
        SKImageFilter Color(float[] matrix) { using var filter = SKColorFilter.CreateColorMatrix(matrix); return SKImageFilter.CreateColorFilter(filter); }
        switch (e.Kind)
        {
            case EffectKind.GaussianBlur: return SKImageFilter.CreateBlur(P("Radius"), P("Radius"));
            case EffectKind.Glow: return SKImageFilter.CreateDropShadow(0, 0, P("Radius"), P("Radius"), SKColor.Parse(e.Color).WithAlpha((byte)(P("Strength") * 2.55f)));
            case EffectKind.DropShadow:
                var angle = P("Angle") * MathF.PI / 180; var distance = P("Distance");
                return SKImageFilter.CreateDropShadow(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, P("Radius"), P("Radius"), SKColor.Parse(e.Color).WithAlpha((byte)(P("Opacity") * 2.55f)));
            case EffectKind.Exposure:
                var exposure = MathF.Pow(2, P("Exposure"));
                return Color([exposure,0,0,0,0, 0,exposure,0,0,0, 0,0,exposure,0,0, 0,0,0,1,0]);
            case EffectKind.BrightnessContrast:
                var contrast = 1 + P("Contrast") / 100; var offset = P("Brightness") / 100 + .5f * (1 - contrast);
                return Color([contrast,0,0,0,offset, 0,contrast,0,0,offset, 0,0,contrast,0,offset, 0,0,0,1,0]);
            case EffectKind.Saturation:
                var s = 1 + P("Saturation") / 100; var r = .2126f * (1 - s); var g = .7152f * (1 - s); var b = .0722f * (1 - s);
                using (var saturation = Color([r+s,g,b,0,0, r,g+s,b,0,0, r,g,b+s,0,0, 0,0,0,1,0]))
                {
                    var a = P("Hue") * MathF.PI / 180; var co = MathF.Cos(a); var si = MathF.Sin(a);
                    using var hue = Color([
                        .213f + co*.787f-si*.213f, .715f-co*.715f-si*.715f, .072f-co*.072f+si*.928f,0,0,
                        .213f-co*.213f+si*.143f, .715f+co*.285f+si*.140f, .072f-co*.072f-si*.283f,0,0,
                        .213f-co*.213f-si*.787f, .715f-co*.715f+si*.715f, .072f+co*.928f+si*.072f,0,0,
                        0,0,0,1,0]);
                    return SKImageFilter.CreateCompose(hue, saturation);
                }
            case EffectKind.Tint:
                var color = SKColor.Parse(e.Color); var t = P("Amount") / 100; var inv = 1 - t;
                var cr = color.Red / 255f * t; var cg = color.Green / 255f * t; var cb = color.Blue / 255f * t;
                return Color([inv+cr*.2126f,cr*.7152f,cr*.0722f,0,0, cg*.2126f,inv+cg*.7152f,cg*.0722f,0,0, cb*.2126f,cb*.7152f,inv+cb*.0722f,0,0, 0,0,0,1,0]);
            case EffectKind.Invert: return Color([-1,0,0,0,1, 0,-1,0,0,1, 0,0,-1,0,1, 0,0,0,1,0]);
            case EffectKind.Posterize:
                var levels = Math.Max(2, (int)P("Levels")); var table = new byte[256]; var alpha = new byte[256];
                for (var i = 0; i < 256; i++) { alpha[i] = (byte)i; table[i] = (byte)Math.Round(Math.Round(i / 255d * (levels - 1)) / (levels - 1) * 255); }
                using (var filter = SKColorFilter.CreateTable(alpha, table, table, table)) return SKImageFilter.CreateColorFilter(filter);
            default: return null; // FractalNoise is a fill generator; Vignette is a source-atop overlay.
        }
    }
}
