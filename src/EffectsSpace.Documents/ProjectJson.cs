using System.Text.Json;
using System.Text.Json.Serialization;
using EffectsSpace.Core;

namespace EffectsSpace.Documents;

public static class ProjectJson
{
    public const int MaximumCharacters = 96 * 1024 * 1024;
    public static JsonSerializerOptions Options { get; } = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, MaxDepth = 64, IgnoreReadOnlyProperties = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }
    public static string Save(MotionProject project) => JsonSerializer.Serialize(project, Options);
    public static MotionProject Load(string json)
    {
        if (json.Length > MaximumCharacters) throw new InvalidDataException("Project exceeds the 96 MiB JSON limit.");
        var project = JsonSerializer.Deserialize<MotionProject>(json, Options) ?? throw new InvalidDataException("Empty project.");
        ProjectValidator.Validate(project);
        return project;
    }
    public static MotionProject Clone(MotionProject project) => Load(Save(project));
    public static Layer CloneLayer(Layer layer) => JsonSerializer.Deserialize<Layer>(JsonSerializer.Serialize(layer, Options), Options)!;
}
