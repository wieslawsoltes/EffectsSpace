namespace EffectsSpace.Core;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(Vec2 point) => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
}
