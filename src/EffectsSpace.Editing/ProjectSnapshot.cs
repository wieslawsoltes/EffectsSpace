using EffectsSpace.Core;
using EffectsSpace.Documents;

namespace EffectsSpace.Editing;

/// <summary>History copies metadata but shares immutable media payloads instead of repeatedly base64-encoding them.</summary>
internal sealed record ProjectSnapshot(string Json, IReadOnlyDictionary<string, MediaAsset> Assets)
{
    private static MediaAsset Copy(MediaAsset asset, byte[] data) => new()
    { Id = asset.Id, Name = asset.Name, MimeType = asset.MimeType, Width = asset.Width, Height = asset.Height, Duration = asset.Duration, Data = data };

    public static ProjectSnapshot Capture(MotionProject project)
    {
        var assets = project.Assets.ToDictionary(a => a.Id, a => Copy(a, a.Data));
        var shell = new MotionProject
        {
            SchemaVersion = project.SchemaVersion, Name = project.Name,
            ActiveCompositionId = project.ActiveCompositionId, Compositions = project.Compositions,
            Assets = assets.Values.Select(a => Copy(a, [])).ToList()
        };
        return new(ProjectJson.Save(shell), assets);
    }

    public bool SameContentAs(ProjectSnapshot other) => Json == other.Json && Assets.Count == other.Assets.Count
        && Assets.All(p => other.Assets.TryGetValue(p.Key, out var asset) && ReferenceEquals(p.Value.Data, asset.Data));

    public MotionProject Restore()
    {
        var project = ProjectJson.Load(Json);
        project.Assets = project.Assets.Select(a => Assets.TryGetValue(a.Id, out var original) ? Copy(original, original.Data) : a).ToList();
        return project;
    }
}
