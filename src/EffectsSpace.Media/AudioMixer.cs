using EffectsSpace.Animation;
using EffectsSpace.Core;

namespace EffectsSpace.Media;

/// <summary>Prepared nested-composition audio plan. Block mixing allocates no managed objects for ordinary unexpressed tracks.</summary>
/// <remarks>The caller must rebuild after editing the project. Linear resampling changes pitch with playback speed.</remarks>
public sealed class AudioMixer
{
    private sealed class Gate(Layer layer, int index)
    {
        public readonly double In = layer.InPoint, Out = layer.OutPoint, Start = layer.StartTime, Stretch = layer.Stretch;
        public readonly Channel? Remap = layer.TimeRemapEnabled ? layer.TimeRemap : null;
        public readonly Channel Gain = layer.AudioGain, Pan = layer.AudioPan;
        public readonly int Index = index;
        public readonly bool StaticGain = layer.AudioGain.Keys.Count == 0 && string.IsNullOrWhiteSpace(layer.AudioGain.Expression);
        public readonly bool StaticPan = layer.AudioPan.Keys.Count == 0 && string.IsNullOrWhiteSpace(layer.AudioPan.Expression);
        public readonly double ConstantGain = GainValue(layer.AudioGain.Value);
        public readonly double ConstantPan = Math.Clamp(layer.AudioPan.Value / 100, -1, 1);

        public bool Map(ref double time, ref double left, ref double right)
        {
            if (time < In || time >= Out) return false;
            var gain = StaticGain ? ConstantGain : GainValue(CurveEvaluator.Evaluate(Gain, time, Index));
            var pan = StaticPan ? ConstantPan : Math.Clamp(CurveEvaluator.Evaluate(Pan, time, Index) / 100, -1, 1);
            left = Math.Clamp(left * gain * (pan > 0 ? 1 - pan : 1), 0, 1e6);
            right = Math.Clamp(right * gain * (pan < 0 ? 1 + pan : 1), 0, 1e6);
            // A held source time must be silence, not a sustained DC sample.
            if (Remap is not null && (!CurveVelocity.TryEvaluate(Remap, time, out var velocity, Index) || Math.Abs(velocity) < 1e-12)) return false;
            time = Remap is null ? (time - Start) / Stretch : CurveEvaluator.Evaluate(Remap, time, Index);
            return double.IsFinite(time);
        }
        private static double GainValue(double db) => db <= -96 ? 0 : Math.Pow(10, Math.Clamp(db, -96, 24) / 20);
    }

    private sealed record Voice(PcmSource Source, Gate[] Gates);
    private readonly Voice[] _voices;
    private readonly double _duration;
    public int VoiceCount => _voices.Length;
    public bool HasAudio => _voices.Length > 0;

    public AudioMixer(MotionProject project, Composition composition, MediaCatalog? catalog = null, bool includeGuides = false)
    {
        ArgumentNullException.ThrowIfNull(project); ArgumentNullException.ThrowIfNull(composition);
        catalog ??= new MediaCatalog();
        var assets = project.Assets.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var compositions = project.Compositions.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var voices = new List<Voice>(); var ancestors = new List<Gate>(); var visited = new HashSet<string>(StringComparer.Ordinal);
        _duration = composition.Duration;
        void Visit(Composition current, int depth)
        {
            if (depth > 32 || !visited.Add(current.Id)) throw new InvalidDataException("Audio composition nesting is cyclic or too deep.");
            var guides = depth == 0 && includeGuides;
            var solo = current.Layers.Any(l => l.Enabled && (guides || !l.Guide) && l.Solo);
            for (var i = 0; i < current.Layers.Count; i++)
            {
                var layer = current.Layers[i];
                if (!layer.Enabled || !layer.AudioEnabled || (!guides && layer.Guide) || (solo && !layer.Solo)) continue;
                if (layer.Kind is not (LayerKind.Audio or LayerKind.Video or LayerKind.Composition)) continue;
                ancestors.Add(new Gate(layer, i + 1));
                if (layer.Kind == LayerKind.Composition)
                {
                    if (layer.SourceId is null || !compositions.TryGetValue(layer.SourceId, out var nested)) throw new InvalidDataException("Missing audio source composition.");
                    Visit(nested, depth + 1);
                }
                else
                {
                    if (layer.SourceId is null || !assets.TryGetValue(layer.SourceId, out var asset)) throw new InvalidDataException("Missing audio media asset.");
                    var media = catalog.Get(asset);
                    if (media.Audio is { FrameCount: > 0 } source)
                    {
                        if (voices.Count >= 512) throw new InvalidOperationException("Audio plan exceeds 512 simultaneous source instances.");
                        voices.Add(new(source, ancestors.ToArray()));
                    }
                }
                ancestors.RemoveAt(ancestors.Count - 1);
            }
            visited.Remove(current.Id);
        }
        Visit(composition, 0); _voices = voices.ToArray();
    }

    /// <summary>Mix stereo float samples using absolute integer output-frame indices. Values are not normalized or clipped.</summary>
    public void Mix(double startTime, long firstFrame, Span<float> stereo, int sampleRate = 48000)
    {
        if (!double.IsFinite(startTime) || firstFrame < 0 || stereo.Length % 2 != 0 || sampleRate is < 8000 or > 192000)
            throw new ArgumentOutOfRangeException(nameof(startTime));
        stereo.Clear();
        var count = stereo.Length / 2;
        if (firstFrame > long.MaxValue - count) throw new ArgumentOutOfRangeException(nameof(firstFrame));
        foreach (var voice in _voices)
        {
            for (var i = 0; i < count; i++)
            {
                var time = startTime + (firstFrame + i) / (double)sampleRate;
                if (time < 0 || time >= _duration) continue;
                double left = 1, right = 1; var active = true;
                foreach (var gate in voice.Gates)
                    if (!gate.Map(ref time, ref left, ref right)) { active = false; break; }
                if (!active) continue;
                stereo[i * 2] += voice.Source.Sample(time, 0) * (float)left;
                stereo[i * 2 + 1] += voice.Source.Sample(time, 1) * (float)right;
            }
        }
    }

    public async Task<byte[]> WaveAsync(double start, double duration, int sampleRate = 48000,
        long maximumBytes = 256L * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(start) || !double.IsFinite(duration) || start < 0 || duration <= 0 || sampleRate is < 8000 or > 192000)
            throw new ArgumentOutOfRangeException(nameof(duration));
        long total = checked((long)Math.Floor(duration * sampleRate + 1e-8));
        if (maximumBytes < 44 || total > (maximumBytes - 44) / 4) throw new InvalidOperationException("WAVE output exceeds the configured byte budget.");
        using var stream = new MemoryStream(); WaveFile.WriteHeader(stream, sampleRate, 2, total);
        var block = new float[4096 * 2];
        for (long frame = 0; frame < total; frame += 4096)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(4096, total - frame);
            Mix(start, frame, block.AsSpan(0, count * 2), sampleRate);
            WaveFile.WriteSamples(stream, block.AsSpan(0, count * 2)); await Task.Yield();
        }
        return stream.ToArray();
    }
}
