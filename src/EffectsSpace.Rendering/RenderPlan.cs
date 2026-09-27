using System.Numerics;
using EffectsSpace.Core;

namespace EffectsSpace.Rendering;

public sealed record RenderLayer(Layer Layer, Matrix3x2 World, double Opacity, double SourceTime, int Index);
public sealed record RenderPlan(Composition Composition, double Time, IReadOnlyList<RenderLayer> Layers);
public readonly record struct RenderBudget(int MaximumDimension = 8192, long MaximumPixels = 33554432, int MaximumNesting = 32)
{
    public static RenderBudget Default => new(8192, 33554432, 32);
    public void Check(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaximumDimension || height > MaximumDimension || (long)width * height > MaximumPixels)
            throw new InvalidOperationException("Render size exceeds the configured pixel budget.");
    }
}
