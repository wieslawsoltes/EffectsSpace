namespace EffectsSpace.Core;

public sealed class MotionProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled project";
    public string ActiveCompositionId { get; set; } = "";
    public List<Composition> Compositions { get; set; } = [];
    public List<MediaAsset> Assets { get; set; } = [];
    public static MotionProject Empty()
    {
        var comp = new Composition();
        return new() { ActiveCompositionId = comp.Id, Compositions = [comp] };
    }
}
