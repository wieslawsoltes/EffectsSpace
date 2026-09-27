namespace EffectsSpace.Core;

public sealed class AnimatedTransform
{
    public Channel X { get; set; } = new();
    public Channel Y { get; set; } = new();
    public Channel AnchorX { get; set; } = new();
    public Channel AnchorY { get; set; } = new();
    public Channel ScaleX { get; set; } = new(100);
    public Channel ScaleY { get; set; } = new(100);
    public Channel Rotation { get; set; } = new();
    public Channel Opacity { get; set; } = new(100);
    public IEnumerable<(string Name, Channel Channel)> Channels()
    {
        yield return ("X", X); yield return ("Y", Y);
        yield return ("AnchorX", AnchorX); yield return ("AnchorY", AnchorY);
        yield return ("ScaleX", ScaleX); yield return ("ScaleY", ScaleY);
        yield return ("Rotation", Rotation); yield return ("Opacity", Opacity);
    }
    public Channel Get(string name) => Channels().FirstOrDefault(p => p.Name == name).Channel ?? throw new ArgumentException($"Unknown property: {name}");
}
