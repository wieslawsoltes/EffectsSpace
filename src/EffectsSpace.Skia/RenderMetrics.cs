namespace EffectsSpace.Skia;

/// <summary>CPU submission counters; these are not GPU timer-query measurements.</summary>
public sealed class RenderMetrics
{
    public int CompositionEvaluations { get; internal set; }
    public int TransformEvaluations { get; internal set; }
    public int LayerDraws { get; internal set; }
    public int DirectDraws { get; internal set; }
    public int CulledLayers { get; internal set; }
    public int IsolationLayers { get; internal set; }
    public int GeometryHits { get; internal set; }
    public int GeometryBuilds { get; internal set; }
    public int FilterHits { get; internal set; }
    public int FilterBuilds { get; internal set; }
    public int AdjustmentLayers { get; internal set; }
    public double SubmissionMilliseconds { get; internal set; }
}
