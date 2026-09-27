using EffectsSpace.Core;
using EffectsSpace.Documents;

namespace EffectsSpace.Editing;

/// <summary>History shares immutable media payloads instead of repeatedly base64-encoding them.</summary>
internal sealed record ProjectSnapshot(string Json, IReadOnlyDictionary<string, MediaAsset> Assets)
{
    public static ProjectSnapshot Capture(MotionProject project)
    {
        var shell = new MotionProject
        {
            SchemaVersion = project.SchemaVersion, Name = project.Name,
            ActiveCompositionId = project.ActiveCompositionId, Compositions = project.Compositions,
            Assets = project.Assets.Select(a => new MediaAsset { Id = a.Id, Name = a.Name, MimeType = a.MimeType, Width = a.Width, Height = a.Height, Duration = a.Duration }).ToList()
        };
        return new(ProjectJson.Save(shell), project.Assets.ToDictionary(a => a.Id));
    }
    public MotionProject Restore()
    {
        var project = ProjectJson.Load(Json);
        project.Assets = project.Assets.Select(a => Assets.TryGetValue(a.Id, out var original) ? original : a).ToList();
        return project;
    }
}
