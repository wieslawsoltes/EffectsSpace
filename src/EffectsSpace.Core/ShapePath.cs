namespace EffectsSpace.Core;

public sealed class ShapePath
{
    public bool Closed { get; set; } = true;
    public List<PathNode> Nodes { get; set; } = [];
    public static ShapePath Rectangle(double width, double height) => new()
    {
        Nodes = [new() { Point = new(0, 0) }, new() { Point = new(width, 0) }, new() { Point = new(width, height) }, new() { Point = new(0, height) }]
    };
}
