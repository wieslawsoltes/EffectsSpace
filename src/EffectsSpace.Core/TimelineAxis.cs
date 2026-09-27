namespace EffectsSpace.Core;

/// <summary>Device-independent timeline geometry shared by rendering, input and tests.</summary>
public readonly record struct TimelineAxis(double Origin, double PixelsPerSecond, double ScrollSeconds)
{
    public double ToX(double seconds) => Origin + (seconds - ScrollSeconds) * PixelsPerSecond;
    public double ToTime(double x) => ScrollSeconds + (x - Origin) / PixelsPerSecond;
    public double SnapX(double x, FrameRate rate) => rate.Snap(ToTime(x));
    public RectD ClipBounds(Layer layer, double y, double height) => new(ToX(layer.InPoint), y, Math.Max(1, (layer.OutPoint - layer.InPoint) * PixelsPerSecond), height);
}
