using EffectsSpace.Core;

namespace EffectsSpace.Animation;

public static class CurveEvaluator
{
    public static double Evaluate(Channel channel, double time, int index = 1)
    {
        var t = time;
        if (channel.Expression.Trim() == "loopOut()" && channel.Keys.Count > 1 && time > channel.Keys[^1].Time)
        {
            var first = channel.Keys[0].Time;
            var span = channel.Keys[^1].Time - first;
            t = first + ((time - first) % span + span) % span;
        }
        var value = EvaluateKeys(channel, t);
        if (string.IsNullOrWhiteSpace(channel.Expression) || channel.Expression.Trim() == "loopOut()") return value;
        return ScalarExpression.TryEvaluate(channel.Expression, time, value, index, out var result, out _) ? result : value;
    }

    public static double EvaluateKeys(Channel channel, double time)
    {
        var keys = channel.Keys;
        if (keys.Count == 0) return channel.Value;
        if (time <= keys[0].Time) return keys[0].Value;
        if (time >= keys[^1].Time) return keys[^1].Value;
        var low = 0; var high = keys.Count - 1;
        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            if (keys[mid].Time <= time) low = mid; else high = mid;
        }
        var a = keys[low]; var b = keys[high];
        if (a.Interpolation == Interpolation.Hold) return a.Value;
        var progress = (time - a.Time) / (b.Time - a.Time);
        if (a.Interpolation == Interpolation.Bezier) progress = Bezier(progress, a.X1, a.Y1, a.X2, a.Y2);
        return a.Value + (b.Value - a.Value) * progress;
    }

    public static double Bezier(double x, double x1, double y1, double x2, double y2)
    {
        // Solve x(t), not y(x). Bisection remains stable for zero endpoint tangents.
        var low = 0d; var high = 1d;
        for (var i = 0; i < 40; i++)
        {
            var t = (low + high) * 0.5;
            if (Cubic(t, x1, x2) < x) low = t; else high = t;
        }
        return Cubic((low + high) * 0.5, y1, y2);
    }

    private static double Cubic(double t, double a, double b) => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
}
