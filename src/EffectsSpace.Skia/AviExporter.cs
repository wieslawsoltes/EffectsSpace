using EffectsSpace.Core;
using EffectsSpace.Media;
using EffectsSpace.Rendering;
using SkiaSharp;

namespace EffectsSpace.Skia;

/// <summary>Portable Motion JPEG AVI export with rational frame timing and optional mixed PCM audio.</summary>
public sealed class AviExporter(SkiaCompositor compositor)
{
    public async Task<byte[]> ExportAsync(MotionProject project, Composition composition, int width = 0, int quality = 90,
        bool audio = true, IProgress<double>? progress = null, CancellationToken cancellationToken = default,
        AudioResamplingQuality resamplingQuality = AudioResamplingQuality.Linear)
    {
        width = width <= 0 ? composition.Width : width;
        if (quality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(quality));
        var height = Math.Max(1, checked((int)Math.Round(width * (double)composition.Height / composition.Width)));
        compositor.Budget.Check(width, height);
        var times = RenderPlanner.ExportTimes(composition).ToArray();
        var mixer = audio ? new AudioMixer(project, composition, quality: resamplingQuality) : null;
        var sampleRate = mixer?.HasAudio == true ? 48000 : 0;
        using var output = new MemoryStream();
        using var writer = new MjpegAviWriter(output, width, height, composition.FrameRate, times.Length, sampleRate);
        using var color = SKColorSpace.CreateSrgb();
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, color))
            ?? throw new InvalidOperationException("Could not allocate the video export surface.");
        var audioBuffer = sampleRate == 0 ? [] : new float[(int)Math.Ceiling(sampleRate / composition.FrameRate.FramesPerSecond) * 2];
        for (var i = 0; i < times.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            surface.Canvas.ResetMatrix(); surface.Canvas.Clear(SKColor.Parse(composition.Background));
            surface.Canvas.Scale(width / (float)composition.Width, height / (float)composition.Height);
            compositor.Render(surface.Canvas, project, composition, times[i], includeGuides: false);
            using var image = surface.Snapshot();
            using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality) ?? throw new InvalidOperationException("JPEG encoding failed.");
            var begin = writer.AudioFrameBoundary(i); var end = writer.AudioFrameBoundary(i + 1);
            var samples = checked((int)(end - begin) * 2);
            if (samples > 0) mixer!.Mix(times[0], begin, audioBuffer.AsSpan(0, samples), sampleRate);
            writer.WriteFrame(jpeg.AsSpan(), audioBuffer.AsSpan(0, samples));
            progress?.Report((i + 1d) / times.Length); await Task.Yield();
        }
        cancellationToken.ThrowIfCancellationRequested(); writer.Complete(); return output.ToArray();
    }
}
