using System.Collections.ObjectModel;
using EffectsSpace.Core;

namespace EffectsSpace.Editing;

public sealed record KeyframeValue(double Time, double Value, Interpolation Interpolation, double X1, double Y1, double X2, double Y2)
{
    public static KeyframeValue Capture(Keyframe key, double origin = 0) => new(key.Time - origin, key.Value, key.Interpolation, key.X1, key.Y1, key.X2, key.Y2);
    public Keyframe Create(double time) => new() { Time = time, Value = Value, Interpolation = Interpolation, X1 = X1, Y1 = Y1, X2 = X2, Y2 = Y2 };
}

public sealed record ClipboardTrack(int LayerSlot, string Property, EffectKind? EffectKind, int EffectOccurrence,
    string? Parameter, ReadOnlyCollection<KeyframeValue> Keys);

/// <summary>Session-local immutable clipboard. It never serializes or executes arbitrary clipboard text.</summary>
public sealed class KeyframeClipboard
{
    public ReadOnlyCollection<ClipboardTrack> Tracks { get; }
    public int LayerCount { get; }
    public int KeyCount => Tracks.Sum(t => t.Keys.Count);
    internal KeyframeClipboard(IEnumerable<ClipboardTrack> tracks, int layerCount)
    { Tracks = Array.AsReadOnly(tracks.ToArray()); LayerCount = layerCount; }
}

public sealed record KeyframeTarget(Layer Layer, AnimatedProperty Property, Keyframe Key);
