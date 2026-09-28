using EffectsSpace.Controls;
using EffectsSpace.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace EffectsSpace.Viewer;

/// <summary>Exact source peak buckets, cancellable block decoding and no per-frame waveform sampling.</summary>
public sealed class AudioWaveformView : SKCanvasElement
{
    private PcmSource? _source;
    private float[] _minimum = [], _maximum = [];
    private int _generation;
    private double _playhead;
    public bool IsReady { get; private set; }
    public string? Error { get; private set; }
    public double Playhead { get => _playhead; set { if (_playhead == value) return; _playhead = value; Invalidate(); } }

    public void SetSource(PcmSource? source)
    {
        if (ReferenceEquals(source, _source)) return;
        _source = source; _minimum = []; _maximum = []; IsReady = false; Error = null;
        var generation = ++_generation; Invalidate();
        if (source is not null) Build(source, generation);
    }

    private async void Build(PcmSource source, int generation)
    {
        try
        {
            const int buckets = 1024;
            const int blockFrames = 4096;
            var minimum = new float[buckets]; var maximum = new float[buckets];
            var buffer = new float[blockFrames * 2];
            long framesSinceYield = 0;
            for (int bucket = 0; bucket < buckets; bucket++)
            {
                long start = source.FrameCount * bucket / buckets, end = source.FrameCount * (bucket + 1) / buckets;
                float min = 0, max = 0;
                for (long frame = start; frame < end;)
                {
                    int count = (int)Math.Min(blockFrames, end - frame);
                    source.ReadStereoFrames(frame, buffer.AsSpan(0, count * 2));
                    for (int i = 0; i < count * 2; i++) { min = Math.Min(min, buffer[i]); max = Math.Max(max, buffer[i]); }
                    frame += count; framesSinceYield += count;
                    if (framesSinceYield >= 16384)
                    {
                        framesSinceYield = 0;
                        await Task.Yield();
                        if (generation != _generation) return;
                    }
                }
                minimum[bucket] = min; maximum[bucket] = max;
            }
            if (generation != _generation) return;
            _minimum = minimum; _maximum = maximum; IsReady = true; Invalidate();
        }
        catch (Exception ex) { if (generation == _generation) { _minimum = []; _maximum = []; IsReady = false; Error = ex.Message; Invalidate(); } }
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        var width = (float)area.Width; var height = (float)area.Height;
        Studio.Rect(canvas, 0, 0, width, height, "#191E24");
        if (_source is null || !IsReady)
        { Studio.DrawText(canvas, Error ?? (_source is null ? "No audio stream" : "Building waveform…"), 10, 26, 10, Studio.Muted); return; }
        var center = (height - 19) / 2; var scale = Math.Max(1, center - 5);
        using var pen = new SKPaint { Color = SKColor.Parse("#87C6B6"), StrokeWidth = 1 };
        var pixels = Math.Max(1, (int)width);
        for (int x = 0; x < pixels; x++)
        {
            int first = x * _minimum.Length / pixels, end = Math.Max(first + 1, (x + 1) * _minimum.Length / pixels);
            float min = 0, max = 0;
            for (int i = first; i < Math.Min(end, _minimum.Length); i++) { min = Math.Min(min, _minimum[i]); max = Math.Max(max, _maximum[i]); }
            canvas.DrawLine(x, center - Math.Clamp(max, -1, 1) * scale, x, center - Math.Clamp(min, -1, 1) * scale, pen);
        }
        double duration = _source.Duration;
        if (duration > 0)
        {
            float cursor = (float)((Playhead - _source.StartTime) / duration * width);
            if (cursor >= 0 && cursor < width) { pen.Color = SKColor.Parse(Studio.Accent); canvas.DrawLine(cursor, 0, cursor, height - 18, pen); }
        }
        Studio.DrawText(canvas, $"0s     {_source.Format.SampleRate / 1000d:0.#} kHz / {_source.Format.Channels} ch     {duration:0.###}s", 7, height - 5, 9, Studio.Muted);
    }
    public new void Dispose() { _generation++; _source = null; _minimum = []; _maximum = []; base.Dispose(); }
}
