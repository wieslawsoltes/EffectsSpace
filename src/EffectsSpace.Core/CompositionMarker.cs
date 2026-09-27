namespace EffectsSpace.Core;

public sealed class CompositionMarker
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double Time { get; set; }
    public string Name { get; set; } = "Marker";
    public string Color { get; set; } = "#E8BC72";
}
