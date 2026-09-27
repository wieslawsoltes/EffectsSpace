namespace EffectsSpace.Workbench;

public sealed class RenderQueueItem
{
    public string Name { get; init; } = "Composition";
    public string Format { get; init; } = "PNG sequence";
    public string Status { get; set; } = "Queued";
    public double Progress { get; set; }
    public string? Error { get; set; }
}
