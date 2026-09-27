namespace EffectsSpace.Core;

public enum MaskMode { Add, Subtract, Intersect, None }
public sealed class LayerMask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Mask 1";
    public bool Enabled { get; set; } = true;
    public MaskMode Mode { get; set; }
    public bool Inverted { get; set; }
    public double Feather { get; set; }
    public double Opacity { get; set; } = 100;
    /// <summary>Signed local-pixel radius of raster dilation/erosion before feathering.</summary>
    public double Expansion { get; set; }
    public ShapePath Path { get; set; } = new();
}
