using EffectsSpace.Core;

namespace EffectsSpace.Timeline;

public sealed record TimelineRow(Layer Layer, string? Property, double Y, double Height)
{
    public bool IsProperty => Property is not null;
}
