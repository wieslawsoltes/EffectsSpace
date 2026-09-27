using EffectsSpace.Core;
using SkiaSharp;

namespace EffectsSpace.Skia;

/// <summary>
/// Bounded, exact-content-checked native resources. Reference identity is only a lookup key:
/// in-place path edits and live effect-parameter edits are compared before every reuse.
/// </summary>
internal sealed class LayerResourceCache : IDisposable
{
    internal sealed class Entry : IDisposable
    {
        private GeometryState? _geometryState;
        private (Vec2 Point, Vec2 In, Vec2 Out)[] _nodes = [];
        private readonly List<EffectState> _effectStates = [];
        private bool _hasEffects;
        public SKPath? Geometry { get; private set; }
        public SKImageFilter? Filter { get; private set; }
        public long Stamp { get; set; }
        public long EstimatedBytes => 256 + _nodes.LongLength * 96 + _effectStates.Sum(s => 128L + s.Values.LongLength * 4);

        public SKPath GetGeometry(Layer layer, RenderMetrics metrics)
        {
            var state = new GeometryState(layer.Kind, layer.Width, layer.Height, layer.CornerRadius,
                layer.StarPoints, layer.InnerRadius, layer.Path.Closed);
            bool same = state == _geometryState && Geometry is not null;
            if (same && layer.Kind == LayerKind.Path)
            {
                same = _nodes.Length == layer.Path.Nodes.Count;
                for (var i = 0; same && i < _nodes.Length; i++)
                {
                    var node = layer.Path.Nodes[i];
                    same = _nodes[i] == (node.Point, node.InHandle, node.OutHandle);
                }
            }
            if (same) { metrics.GeometryHits++; return Geometry!; }
            var next = PathGeometry.LayerPath(layer);
            Geometry?.Dispose(); Geometry = next; _geometryState = state;
            _nodes = layer.Kind == LayerKind.Path
                ? layer.Path.Nodes.Select(n => (n.Point, n.InHandle, n.OutHandle)).ToArray() : [];
            metrics.GeometryBuilds++; return next;
        }

        public SKImageFilter? GetFilter(Layer layer, double time, RenderMetrics metrics)
        {
            var same = _hasEffects && layer.Effects.Count == _effectStates.Count;
            for (var i = 0; same && i < layer.Effects.Count; i++) same = _effectStates[i].Matches(layer.Effects[i], time);
            if (same) { metrics.FilterHits++; return Filter; }
            var next = EffectPipeline.Build(layer.Effects, time);
            Filter?.Dispose(); Filter = next; _effectStates.Clear();
            foreach (var effect in layer.Effects) _effectStates.Add(EffectState.Capture(effect, time));
            _hasEffects = true; metrics.FilterBuilds++; return next;
        }
        public void Dispose() { Geometry?.Dispose(); Filter?.Dispose(); Geometry = null; Filter = null; }
    }

    private readonly record struct GeometryState(LayerKind Kind, double Width, double Height, double Radius, int Points, double Inner, bool Closed);
    private sealed record EffectState(EffectKind Kind, bool Enabled, string Color, float[] Values)
    {
        public static EffectState Capture(LayerEffect effect, double time) => new(effect.Kind, effect.Enabled, effect.Color,
            EffectCatalog.Get(effect.Kind).Parameters.Select(p => EffectPipeline.Parameter(effect, p.Key, time)).ToArray());
        public bool Matches(LayerEffect effect, double time)
        {
            if (effect.Kind != Kind || effect.Enabled != Enabled || effect.Color != Color) return false;
            var parameters = EffectCatalog.Get(effect.Kind).Parameters;
            if (parameters.Length != Values.Length) return false;
            for (var i = 0; i < Values.Length; i++) if (Values[i] != EffectPipeline.Parameter(effect, parameters[i].Key, time)) return false;
            return true;
        }
    }

    private readonly Dictionary<Layer, Entry> _entries = new(ReferenceEqualityComparer.Instance);
    private long _stamp;
    public int MaximumEntries { get; set; } = 512;
    public long MaximumEstimatedBytes { get; set; } = 16 * 1024 * 1024;
    public int Count => _entries.Count;
    public Entry Get(Layer layer)
    {
        if (!_entries.TryGetValue(layer, out var entry)) _entries.Add(layer, entry = new Entry());
        entry.Stamp = ++_stamp; return entry;
    }
    public void Trim()
    {
        long bytes = _entries.Values.Sum(e => e.EstimatedBytes);
        while (_entries.Count > Math.Max(0, MaximumEntries) || bytes > MaximumEstimatedBytes)
        {
            var oldest = _entries.MinBy(p => p.Value.Stamp);
            bytes -= oldest.Value.EstimatedBytes; oldest.Value.Dispose(); _entries.Remove(oldest.Key);
        }
    }
    public void Clear() { foreach (var entry in _entries.Values) entry.Dispose(); _entries.Clear(); }
    public void Dispose() => Clear();
}
