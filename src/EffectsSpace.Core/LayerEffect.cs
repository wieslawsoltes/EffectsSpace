namespace EffectsSpace.Core;

public enum EffectKind { GaussianBlur, Glow, DropShadow, Exposure, BrightnessContrast, Saturation, Tint, Invert, Posterize, FractalNoise, Vignette }
public sealed class LayerEffect
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public EffectKind Kind { get; set; }
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "#65D6FF";
    public Dictionary<string, Channel> Parameters { get; set; } = new(StringComparer.Ordinal);
}
