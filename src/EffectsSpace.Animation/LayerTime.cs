using EffectsSpace.Core;

namespace EffectsSpace.Animation;

public static class LayerTime
{
    /// <summary>Source seconds are not clamped: out-of-range media is transparent rather than silently frozen.</summary>
    public static double Evaluate(Layer layer, double compositionTime, int index = 1) =>
        layer.TimeRemapEnabled
            ? CurveEvaluator.Evaluate(layer.TimeRemap, compositionTime, index)
            : layer.SourceTime(compositionTime);
}
