using EffectsSpace.Core;
using EffectsSpace.Media;
using SkiaSharp;

namespace EffectsSpace.Skia;

/// <summary>Original synthetic source footage. The AVI contains real JPEG frames and interleaved PCM samples.</summary>
public static class MediaStudy
{
    public static MotionProject Create(SKTypeface? typeface = null)
    {
        const int width = 320, height = 180, frames = 48, sampleRate = 48000;
        var rate = new FrameRate(24, 1);
        using var output = new MemoryStream(); using var writer = new MjpegAviWriter(output, width, height, rate, frames, sampleRate);
        using var surface = SKSurface.Create(new SKImageInfo(width, height)) ?? throw new InvalidOperationException("Could not allocate the footage sample.");
        using var fill = new SKPaint { IsAntialias = true };
        using var font = new SKFont(typeface ?? SKTypeface.Default, 12);
        var audio = new float[4000];
        for (var frame = 0; frame < frames; frame++)
        {
            var t = rate.Seconds(frame); var canvas = surface.Canvas; canvas.Clear(SKColor.Parse("#121C2B"));
            fill.Color = SKColor.Parse("#283A52");
            for (var x = 0; x <= width; x += 20) canvas.DrawLine(x,0,x,height,fill);
            for (var y = 0; y <= height; y += 20) canvas.DrawLine(0,y,width,y,fill);
            fill.Color = SKColor.Parse("#79E4C0"); canvas.DrawCircle(160 + (float)Math.Cos(t * Math.PI) * 94,90 + (float)Math.Sin(t * Math.PI) * 43,19,fill);
            fill.Color = SKColor.Parse("#BCA7F4"); canvas.DrawRect(18,145,(float)(frame + 1) / frames * 284,3,fill);
            fill.Color = SKColors.White; canvas.DrawText($"CLOCKWORK  /  FRAME {frame:000}",18,25,SKTextAlign.Left,font,fill);
            var first = writer.AudioFrameBoundary(frame); var last = writer.AudioFrameBoundary(frame + 1);
            for (var i = 0; i < last - first; i++)
            {
                var seconds = (first + i) / (double)sampleRate;
                var envelope = Math.Min(1, seconds * 40) * Math.Min(1, (2 - seconds) * 40) * (.4 + .6 * Math.Exp(-(seconds % .5) * 12));
                audio[i * 2] = (float)(.18 * envelope * Math.Sin(seconds * 2 * Math.PI * 220));
                audio[i * 2 + 1] = (float)(.18 * envelope * Math.Sin(seconds * 2 * Math.PI * 330));
            }
            using var image = surface.Snapshot(); using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 88);
            writer.WriteFrame(jpeg.AsSpan(), audio.AsSpan(0,checked((int)(last-first)*2)));
        }
        writer.Complete();
        var asset = new MediaAsset { Name = "Clockwork.avi", MimeType = "video/x-msvideo", Width = width, Height = height, Duration = 2, Data = output.ToArray() };
        var layer = new Layer { Name = "Clockwork footage", Kind = LayerKind.Video, SourceId = asset.Id, Width = 640, Height = 360, OutPoint = 2, Label = "#87C6B6" };
        var comp = new Composition { Name = "Clockwork / Media", Width = 640, Height = 360, FrameRate = rate, Duration = 2, WorkEnd = 2, Layers = [layer], Background = "#121C2B" };
        return new MotionProject { Name = "CLOCKWORK — Media study", ActiveCompositionId = comp.Id, Compositions = [comp], Assets = [asset] };
    }
}
