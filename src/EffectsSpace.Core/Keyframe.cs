namespace EffectsSpace.Core;

public enum Interpolation { Linear, Hold, Bezier }

public sealed class Keyframe
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double Time { get; set; }
    public double Value { get; set; }
    public Interpolation Interpolation { get; set; }
    public double X1 { get; set; } = 0.333333333333;
    public double Y1 { get; set; }
    public double X2 { get; set; } = 0.666666666667;
    public double Y2 { get; set; } = 1;
}
