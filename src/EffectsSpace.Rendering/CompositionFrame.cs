using System.Numerics;
using EffectsSpace.Animation;
using EffectsSpace.Core;

namespace EffectsSpace.Rendering;

/// <summary>
/// An evaluation snapshot for one composition/time pair. Shared parent transforms are evaluated once,
/// with original stacking indices preserved for expressions. No document state is cached across frames.
/// </summary>
public sealed class CompositionFrame
{
    private readonly Dictionary<string, int> _indices;
    private readonly Matrix3x2[] _world;
    private readonly byte[] _state;
    private readonly int _maximumDepth;
    public Composition Composition { get; }
    public double Time { get; }
    public IReadOnlyList<RenderLayer> Layers { get; }
    public int TransformEvaluations { get; private set; }

    public CompositionFrame(Composition composition, double time, bool includeGuides = false, int maximumDepth = 32)
    {
        if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
        Composition = composition; Time = time; _maximumDepth = maximumDepth;
        _indices = new Dictionary<string, int>(composition.Layers.Count, StringComparer.Ordinal);
        _world = new Matrix3x2[composition.Layers.Count]; _state = new byte[composition.Layers.Count];
        for (var i = 0; i < composition.Layers.Count; i++) _indices.Add(composition.Layers[i].Id, i);
        var solo = composition.Layers.Any(l => l.Enabled && l.Solo && (includeGuides || !l.Guide));
        var layers = new List<RenderLayer>(composition.Layers.Count);
        for (var i = composition.Layers.Count - 1; i >= 0; i--)
        {
            var layer = composition.Layers[i];
            if (!layer.ActiveAt(time) || (solo && !layer.Solo) || (!includeGuides && layer.Guide)
                || layer.Kind is LayerKind.Null or LayerKind.Audio) continue;
            layers.Add(Evaluate(i));
        }
        Layers = layers;
    }

    public RenderLayer? Find(string id) => _indices.TryGetValue(id, out var index) ? Evaluate(index) : null;

    private RenderLayer Evaluate(int index)
    {
        var layer = Composition.Layers[index];
        return new(layer, World(index, 0), Math.Clamp(CurveEvaluator.Evaluate(layer.Transform.Opacity, Time, index + 1), 0, 100) / 100,
            LayerTime.Evaluate(layer, Time, index + 1), index + 1);
    }

    private Matrix3x2 World(int index, int depth)
    {
        if (depth > _maximumDepth) throw new InvalidOperationException("Parent transform nesting exceeded.");
        if (_state[index] == 2) return _world[index];
        if (_state[index] == 1) throw new InvalidOperationException("Parent cycle detected.");
        _state[index] = 1;
        var layer = Composition.Layers[index];
        var result = TransformEvaluator.Local(layer, Time, index + 1); TransformEvaluations++;
        if (layer.ParentId is { } parent)
        {
            if (!_indices.TryGetValue(parent, out var parentIndex)) throw new InvalidOperationException("Missing parent: " + parent);
            result *= World(parentIndex, depth + 1);
        }
        _state[index] = 2; return _world[index] = result;
    }
}
