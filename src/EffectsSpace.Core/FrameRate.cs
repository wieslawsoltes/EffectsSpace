namespace EffectsSpace.Core;

/// <summary>A rational frame rate. All timeline editing is quantized through this type.</summary>
public readonly record struct FrameRate(int Numerator, int Denominator)
{
    public double FramesPerSecond => Numerator / (double)Denominator;
    public double Seconds(long frame) => frame * (double)Denominator / Numerator;
    public long Frame(double seconds) => checked((long)Math.Round(seconds * FramesPerSecond, MidpointRounding.AwayFromZero));
    public double Snap(double seconds) => Seconds(Frame(seconds));
    public string Timecode(double seconds)
    {
        // Deliberately non-drop-frame. NTSC drop-frame labels require a different counter.
        var nominal = Math.Max(1, (int)Math.Round(FramesPerSecond));
        var frames = Math.Max(0, Frame(seconds));
        return $"{frames / (nominal * 3600):00}:{frames / (nominal * 60) % 60:00}:{frames / nominal % 60:00}:{frames % nominal:00}";
    }
    public override string ToString() => Denominator == 1 ? $"{Numerator} fps" : $"{FramesPerSecond:0.###} fps";
}
