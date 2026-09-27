namespace EffectsSpace.Core;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double scale) => new(a.X * scale, a.Y * scale);
    public double Length => Math.Sqrt(X * X + Y * Y);
}
