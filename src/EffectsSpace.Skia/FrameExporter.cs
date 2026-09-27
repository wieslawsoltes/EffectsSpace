using System.IO.Compression;
using System.Text.Json;
using EffectsSpace.Core;
using EffectsSpace.Rendering;
using SkiaSharp;

namespace EffectsSpace.Skia;

public sealed class FrameExporter(SkiaCompositor compositor)
{
    public const int MaximumSequenceBytes = 256 * 1024 * 1024;
    public byte[] Png(MotionProject project, Composition composition, double time, int width = 0)
    {
        width = width <= 0 ? composition.Width : width;
        var height = Math.Max(1, (int)Math.Round(width * (double)composition.Height / composition.Width));
        compositor.Budget.Check(width, height);
        using var color = SKColorSpace.CreateSrgb();
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, color)) ?? throw new InvalidOperationException("Could not allocate output surface.");
        surface.Canvas.Clear(SKColors.Transparent); surface.Canvas.Scale(width / (float)composition.Width, height / (float)composition.Height);
        compositor.Render(surface.Canvas, project, composition, time, includeGuides: false);
        using var image = surface.Snapshot(); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
    public async Task<byte[]> PngSequenceAsync(MotionProject project, Composition composition, int width, IProgress<double>? progress = null, Func<double, Task>? prepareFrame = null, CancellationToken cancellationToken = default)
    {
        var times = RenderPlanner.ExportTimes(composition).ToArray(); using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            for (var i = 0; i < times.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (prepareFrame is not null) await prepareFrame(times[i]);
                var bytes = Png(project, composition, times[i], width);
                if (output.Length + bytes.Length > MaximumSequenceBytes) throw new InvalidOperationException("Sequence exceeds 256 MiB. Reduce resolution or work area.");
                var entry = zip.CreateEntry($"frames/frame-{i:000000}.png", CompressionLevel.NoCompression);
                using (var stream = entry.Open()) await stream.WriteAsync(bytes, cancellationToken);
                progress?.Report((i + 1d) / times.Length); await Task.Yield();
            }
            var manifest = zip.CreateEntry("sequence.json");
            using var writer = new StreamWriter(manifest.Open());
            await writer.WriteAsync(JsonSerializer.Serialize(new { composition = composition.Name, frameRateNumerator = composition.FrameRate.Numerator, frameRateDenominator = composition.FrameRate.Denominator, start = composition.WorkStart, endExclusive = composition.WorkEnd, frames = times.Length, width, alpha = true, colorSpace = "sRGB", bitDepth = 8 }));
        }
        return output.ToArray();
    }
}
