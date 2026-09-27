using System.Numerics;
using EffectsSpace.Core;

namespace EffectsSpace.Animation;

public static class TransformEvaluator
{
    public static Matrix3x2 Local(Layer layer, double time, int index = 1)
    {
        var t = layer.Transform;
        float E(Channel c) => (float)CurveEvaluator.Evaluate(c, time, index);
        return Matrix3x2.CreateTranslation(-E(t.AnchorX), -E(t.AnchorY))
            * Matrix3x2.CreateScale(E(t.ScaleX) / 100f, E(t.ScaleY) / 100f)
            * Matrix3x2.CreateRotation(E(t.Rotation) * MathF.PI / 180f)
            * Matrix3x2.CreateTranslation(E(t.X), E(t.Y));
    }
    public static Matrix3x2 World(Composition composition, Layer layer, double time)
    {
        var matrix = Local(layer, time, composition.Layers.IndexOf(layer) + 1);
        var visited = new HashSet<string> { layer.Id };
        while (layer.ParentId is { } parentId)
        {
            if (!visited.Add(parentId)) throw new InvalidOperationException("Parent cycle detected.");
            layer = composition.Layers.FirstOrDefault(l => l.Id == parentId) ?? throw new InvalidOperationException("Missing parent.");
            matrix *= Local(layer, time, composition.Layers.IndexOf(layer) + 1);
        }
        return matrix;
    }
    public static Vec2 ToWorld(Composition composition, Layer layer, double time, Vec2 point)
    {
        var p = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), World(composition, layer, time));
        return new(p.X, p.Y);
    }
    public static Vec2? ToLocal(Composition composition, Layer layer, double time, Vec2 point)
    {
        if (!Matrix3x2.Invert(World(composition, layer, time), out var inverse)) return null;
        var p = Vector2.Transform(new Vector2((float)point.X, (float)point.Y), inverse);
        return new(p.X, p.Y);
    }
}
