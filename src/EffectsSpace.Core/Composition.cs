namespace EffectsSpace.Core;

public sealed class Composition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Composition 1";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public FrameRate FrameRate { get; set; } = new(30, 1);
    public double Duration { get; set; } = 8;
    public double WorkStart { get; set; }
    public double WorkEnd { get; set; } = 8;
    public string Background { get; set; } = "#111318";
    /// <summary>Front-to-back stacking order.</summary>
    public List<Layer> Layers { get; set; } = [];
    public List<CompositionMarker> Markers { get; set; } = [];
    public long FrameCount => Math.Max(1, FrameRate.Frame(Duration));
    public double LastFrameTime => FrameRate.Seconds(FrameCount - 1);
}
