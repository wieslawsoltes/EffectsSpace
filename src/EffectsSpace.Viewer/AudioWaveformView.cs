using EffectsSpace.Controls;
using EffectsSpace.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace EffectsSpace.Viewer;

/// <summary>Reusable source waveform with exact peak buckets, cancellable incremental construction and no per-frame sample decoding.</summary>
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
            var minimum = new float[buckets]; var maximum = new float[buckets];
            for (var bucket = 0; bucket < buckets; bucket++)
            {
                long start = source.FrameCount * bucket / buckets, end = source.FrameCount * (bucket + 1) / buckets;
                float min = 0, max = 0;
                for (long frame = start; frame < end; frame++)
                {
                    for (var channel = 0; channel < source.Format.Channels; channel++)
                    { var value = source.ReadFrame(frame, channel); min = Math.Min(min, value); max = Math.Max(max, value); }
                    if ((frame & 16383) == 0) { await Task.Yield(); if (generation != _generation) return; }
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
        for (var x = 0; x < pixels; x++)
        {
            int first = x * _minimum.Length / pixels, end = Math.Max(first + 1, (x + 1) * _minimum.Length / pixels);
            float min = 0, max = 0;
            for (var i = first; i < Math.Min(end, _minimum.Length); i++) { min = Math.Min(min, _minimum[i]); max = Math.Max(max, _maximum[i]); }
            canvas.DrawLine(x, center - Math.Clamp(max, -1, 1) * scale, x, center - Math.Clamp(min, -1, 1) * scale, pen);
        }
        var duration = _source.Duration;
        if (duration > 0)
        {
            var cursor = (float)((Playhead - _source.StartTime) / duration * width);
            if (cursor >= 0 && cursor < width) { pen.Color = SKColor.Parse(Studio.Accent); canvas.DrawLine(cursor, 0, cursor, height - 18, pen); }
        }
        Studio.DrawText(canvas, $"0s     {_source.Format.SampleRate / 1000d:0.#} kHz / {_source.Format.Channels} ch     {duration:0.###}s", 7, height - 5, 9, Studio.Muted);
    }
    public new void Dispose() { _generation++; _source = null; _minimum = []; _maximum = []; base.Dispose(); }
}
