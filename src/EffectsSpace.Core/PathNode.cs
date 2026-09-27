namespace EffectsSpace.Core;

public sealed class PathNode
{
    public Vec2 Point { get; set; }
    /// <summary>Bezier handles relative to Point; zero handles produce a straight segment.</summary>
    public Vec2 InHandle { get; set; }
    public Vec2 OutHandle { get; set; }
}
