namespace EffectsSpace.Core;

/// <summary>A stable property address. Effect paths use effect identifiers, never mutable stack indices.</summary>
public sealed record AnimatedProperty(string Path, string Name, Channel Channel, double Minimum, double Maximum,
    EffectKind? EffectKind = null, int EffectOccurrence = 0, string? Parameter = null);

public static class LayerChannels
{
    public static IEnumerable<AnimatedProperty> Enumerate(Layer layer)
    {
        foreach (var (name, channel) in layer.Transform.Channels())
            yield return new(name, DisplayName(name), channel, name == "Opacity" ? 0 : -1e9, name == "Opacity" ? 100 : 1e9);
        if (layer.TimeRemapEnabled)
            yield return new("TimeRemap", "Time Remap", layer.TimeRemap, -86400, 86400);
        if (layer.Kind is LayerKind.Audio or LayerKind.Video or LayerKind.Composition)
        {
            yield return new("AudioGain", "Audio Gain (dB)", layer.AudioGain, -96, 24);
            yield return new("AudioPan", "Audio Balance", layer.AudioPan, -100, 100);
        }
        var occurrences = new Dictionary<EffectKind, int>();
        foreach (var effect in layer.Effects)
        {
            var occurrence = occurrences.GetValueOrDefault(effect.Kind);
            occurrences[effect.Kind] = occurrence + 1;
            var definition = EffectCatalog.Get(effect.Kind);
            foreach (var parameter in definition.Parameters)
                if (effect.Parameters.TryGetValue(parameter.Key, out var channel))
                    yield return new(EffectPath(effect.Id, parameter.Key), definition.Name + " · " + parameter.Name,
                        channel, parameter.Minimum, parameter.Maximum, effect.Kind, occurrence, parameter.Key);
        }
    }

    public static string EffectPath(string effectId, string parameter) => $"fx/{effectId}/{parameter}";
    public static AnimatedProperty? Find(Layer layer, string path)
    {
        var channel = path switch
        {
            "X" => layer.Transform.X, "Y" => layer.Transform.Y,
            "AnchorX" => layer.Transform.AnchorX, "AnchorY" => layer.Transform.AnchorY,
            "ScaleX" => layer.Transform.ScaleX, "ScaleY" => layer.Transform.ScaleY,
            "Rotation" => layer.Transform.Rotation, "Opacity" => layer.Transform.Opacity,
            "TimeRemap" when layer.TimeRemapEnabled => layer.TimeRemap, _ => null
        };
        if (channel is not null)
            return new(path, DisplayName(path), channel,
                path == "Opacity" ? 0 : path == "TimeRemap" ? -86400 : -1e9,
                path == "Opacity" ? 100 : path == "TimeRemap" ? 86400 : 1e9);
        return Enumerate(layer).FirstOrDefault(p => p.Path == path);
    }
    public static Channel Get(Layer layer, string path) => Find(layer, path)?.Channel
        ?? throw new ArgumentException("The selected property no longer exists: " + path, nameof(path));
    public static string DisplayName(string property) => property switch
    {
        "X" => "Position X", "Y" => "Position Y", "AnchorX" => "Anchor X", "AnchorY" => "Anchor Y",
        "ScaleX" => "Scale X", "ScaleY" => "Scale Y", "TimeRemap" => "Time Remap", "AudioGain" => "Audio Gain (dB)", "AudioPan" => "Audio Balance", _ => property
    };
}
