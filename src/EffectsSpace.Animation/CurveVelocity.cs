using EffectsSpace.Core;

namespace EffectsSpace.Animation;

/// <summary>Signed value velocity, in property units per second. A false result represents a discontinuity.</summary>
public static class CurveVelocity
{
    public static bool TryEvaluate(Channel channel, double time, out double velocity, int index = 1)
    {
        velocity = 0;
        if (!double.IsFinite(time)) return false;
        var expression = channel.Expression.Trim();
        if (expression.Length > 0)
        {
            // Expressions may be discontinuous and are not symbolically differentiable in this grammar.
            // Their displayed velocity is explicitly a finite-difference estimate.
            const double h = 0.0001;
            var a = CurveEvaluator.Evaluate(channel, time - h, index);
            var b = CurveEvaluator.Evaluate(channel, time + h, index);
            velocity = (b - a) / (2 * h);
            return double.IsFinite(velocity) && Math.Abs(velocity) <= 1e12;
        }
        var keys = channel.Keys;
        if (keys.Count < 2 || time < keys[0].Time || time > keys[^1].Time) return true;
        var low = 0; var high = keys.Count - 1;
        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            if (keys[mid].Time <= time) low = mid; else high = mid;
        }
        if (low > 0 && Math.Abs(time - keys[low].Time) < 1e-10 &&
            keys[low - 1].Interpolation == Interpolation.Hold && keys[low - 1].Value != keys[low].Value) return false;
        var left = keys[low]; var right = keys[high];
        if (left.Interpolation == Interpolation.Hold)
            return time < right.Time || left.Value == right.Value;
        var slope = (right.Value - left.Value) / (right.Time - left.Time);
        if (left.Interpolation == Interpolation.Linear || slope == 0) { velocity = slope; return true; }
        var x = Math.Clamp((time - left.Time) / (right.Time - left.Time), 0, 1);
        var t = Solve(x, left.X1, left.X2);
        var dx = Derivative(t, left.X1, left.X2); var dy = Derivative(t, left.Y1, left.Y2);
        if (Math.Abs(dx) < 1e-11)
        {
            if (Math.Abs(dy) > 1e-11) return false;
            dx = SecondDerivative(t, left.X1, left.X2); dy = SecondDerivative(t, left.Y1, left.Y2);
            if (Math.Abs(dx) < 1e-11)
            {
                if (Math.Abs(dy) > 1e-11) return false;
                dx = 6 * (1 + 3 * left.X1 - 3 * left.X2);
                dy = 6 * (1 + 3 * left.Y1 - 3 * left.Y2);
            }
        }
        if (Math.Abs(dx) < 1e-11) return false;
        velocity = slope * dy / dx;
        return double.IsFinite(velocity) && Math.Abs(velocity) <= 1e12;
    }

    private static double Solve(double x, double a, double b)
    {
        if (x == 0 || x == 1) return x;
        var low = 0d; var high = 1d;
        for (var i = 0; i < 40; i++)
        {
            var t = (low + high) * .5; var u = 1 - t;
            var value = 3 * u * u * t * a + 3 * u * t * t * b + t * t * t;
            if (value < x) low = t; else high = t;
        }
        return (low + high) * .5;
    }
    private static double Derivative(double t, double a, double b) =>
        3 * ((1 - t) * (1 - t) * a + 2 * (1 - t) * t * (b - a) + t * t * (1 - b));
    private static double SecondDerivative(double t, double a, double b) =>
        6 * ((1 - t) * (b - 2 * a) + t * (1 - 2 * b + a));
}
