using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace EffectsSpace.Controls;

public enum IconKind { Select, Hand, Zoom, Rotate, Anchor, Rectangle, Ellipse, Star, Pen, Text, Play, Pause, Stop, First, Last, Previous, Next, Graph, Keyframe, Stopwatch, Eye, Lock, Folder, Composition, Import, Save, Undo, Redo, Add, Delete, Split, Grid, Guides, Checker, Menu, Settings, Search, Link, Render, Close, Chevron, Diamond, Audio }

public sealed class StudioIcon : SKCanvasElement
{
    public IconKind Kind { get; set; }
    public string Color { get; set; } = Studio.TextColor;
    protected override void RenderOverride(SKCanvas canvas, Size area) => Draw(canvas, Kind, 0, 0, (float)Math.Min(area.Width, area.Height), Color);
    public static void Draw(SKCanvas canvas, IconKind kind, float x, float y, float size, string color = Studio.TextColor)
    {
        var saved = canvas.Save(); canvas.Translate(x, y); canvas.Scale(size / 20);
        using var p = new SKPaint { Color = SKColor.Parse(color), IsAntialias = true, StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        void Line(float ax, float ay, float bx, float by) => canvas.DrawLine(ax, ay, bx, by, p);
        void Path(string data, bool fill = false) { using var path = SKPath.ParseSvgPathData(data); p.Style = fill ? SKPaintStyle.Fill : SKPaintStyle.Stroke; canvas.DrawPath(path, p); p.Style = SKPaintStyle.Stroke; }
        switch (kind)
        {
            case IconKind.Select: Path("M4 2 L16 11 L10 12 L8 18 Z"); break;
            case IconKind.Hand: Path("M5 11 L5 7 Q5 5 7 7 L7 3 Q9 1 10 3 L10 7 L11 2 Q13 1 13 4 L13 8 L15 5 Q17 5 17 7 L16 14 Q15 18 10 18 L7 18 L2 12 Q2 10 4 11 Z"); break;
            case IconKind.Zoom: canvas.DrawCircle(8, 8, 5, p); Line(12,12,18,18); Line(5,8,11,8); Line(8,5,8,11); break;
            case IconKind.Rotate: Path("M4 6 A7 7 0 1 1 3 12 M4 2 L4 6 L8 6"); break;
            case IconKind.Anchor: canvas.DrawCircle(10, 10, 4, p); Line(10,1,10,6); Line(10,14,10,19); Line(1,10,6,10); Line(14,10,19,10); break;
            case IconKind.Rectangle: canvas.DrawRect(3,4,14,12,p); break;
            case IconKind.Ellipse: canvas.DrawOval(new SKRect(2,3,18,17),p); break;
            case IconKind.Star: Path("M10 2 L12.5 7 L18 8 L14 12 L15 18 L10 15 L5 18 L6 12 L2 8 L7.5 7 Z"); break;
            case IconKind.Pen: Path("M3 17 L7 5 L17 2 L14 12 Z M3 17 L9 11"); canvas.DrawCircle(10,10,2,p); break;
            case IconKind.Text: Line(3,4,17,4); Line(10,4,10,17); Line(6,17,14,17); Line(3,4,3,7); Line(17,4,17,7); break;
            case IconKind.Play: Path("M6 3 L17 10 L6 17 Z",true); break;
            case IconKind.Pause: p.Style = SKPaintStyle.Fill; canvas.DrawRect(5,4,3,12,p); canvas.DrawRect(12,4,3,12,p); break;
            case IconKind.Stop: p.Style=SKPaintStyle.Fill; canvas.DrawRect(5,5,10,10,p); break;
            case IconKind.First: Line(3,4,3,16); Path("M15 4 L6 10 L15 16 Z",true); break;
            case IconKind.Last: Line(17,4,17,16); Path("M5 4 L14 10 L5 16 Z",true); break;
            case IconKind.Previous: Line(5,4,5,16); Path("M15 4 L7 10 L15 16 Z"); break;
            case IconKind.Next: Line(15,4,15,16); Path("M5 4 L13 10 L5 16 Z"); break;
            case IconKind.Graph: Line(3,2,3,17); Line(3,17,18,17); Path("M4 15 C7 15 5 5 10 5 S15 12 18 3"); break;
            case IconKind.Keyframe: case IconKind.Diamond: Path("M10 3 L17 10 L10 17 L3 10 Z", kind == IconKind.Keyframe); break;
            case IconKind.Stopwatch: canvas.DrawCircle(10,11,6,p); Line(10,11,10,7); Line(7,2,13,2); Line(10,2,10,5); break;
            case IconKind.Eye: Path("M1 10 Q10 0 19 10 Q10 20 1 10 Z"); canvas.DrawCircle(10,10,3,p); break;
            case IconKind.Lock: canvas.DrawRoundRect(new SKRect(4,8,16,18),2,2,p); Path("M6 8 L6 5 Q10 -1 14 5 L14 8"); break;
            case IconKind.Folder: Path("M2 5 L8 5 L10 7 L18 7 L18 17 L2 17 Z"); break;
            case IconKind.Composition: canvas.DrawRect(2,3,16,14,p); Line(2,7,18,7); Line(2,13,18,13); Line(6,3,6,7); Line(14,13,14,17); break;
            case IconKind.Import: Path("M10 2 L10 12 M6 8 L10 12 L14 8 M3 12 L3 18 L17 18 L17 12"); break;
            case IconKind.Save: Path("M3 2 L15 2 L18 5 L18 18 L2 18 L2 2 Z M6 2 L6 8 L14 8 L14 2 M6 18 L6 12 L14 12 L14 18"); break;
            case IconKind.Undo: Path("M3 7 L9 7 Q17 7 17 13 L17 16 M3 7 L7 3 M3 7 L7 11"); break;
            case IconKind.Redo: Path("M17 7 L11 7 Q3 7 3 13 L3 16 M17 7 L13 3 M17 7 L13 11"); break;
            case IconKind.Add: Line(10,3,10,17); Line(3,10,17,10); break;
            case IconKind.Delete: Path("M5 6 L6 18 L14 18 L15 6 M3 5 L17 5 M7 5 L7 2 L13 2 L13 5 M8 8 L8 15 M12 8 L12 15"); break;
            case IconKind.Split: Line(10,2,10,18); Path("M3 4 L7 4 L7 16 L3 16 M17 4 L13 4 L13 16 L17 16"); break;
            case IconKind.Grid: canvas.DrawRect(2,2,16,16,p); Line(7,2,7,18); Line(13,2,13,18); Line(2,7,18,7); Line(2,13,18,13); break;
            case IconKind.Guides: canvas.DrawRect(3,3,14,14,p); canvas.DrawRect(6,6,8,8,p); break;
            case IconKind.Checker: for(var row=0;row<3;row++) for(var col=0;col<3;col++) {p.Style=(row+col)%2==0?SKPaintStyle.Fill:SKPaintStyle.Stroke; canvas.DrawRect(3+col*5,3+row*5,5,5,p);} break;
            case IconKind.Menu: Line(3,5,17,5); Line(3,10,17,10); Line(3,15,17,15); break;
            case IconKind.Settings: canvas.DrawCircle(10,10,6,p); canvas.DrawCircle(10,10,2,p); for(var i=0;i<8;i++){var a=i*MathF.PI/4;Line(10+MathF.Cos(a)*6,10+MathF.Sin(a)*6,10+MathF.Cos(a)*9,10+MathF.Sin(a)*9);} break;
            case IconKind.Search: canvas.DrawCircle(8,8,5,p); Line(12,12,18,18); break;
            case IconKind.Link: Path("M8 6 L11 3 Q16 0 18 5 Q19 8 14 11 M12 14 L9 17 Q4 20 2 15 Q1 12 6 9 M6 14 L14 6"); break;
            case IconKind.Render: canvas.DrawRect(2,3,16,14,p); Path("M8 7 L13 10 L8 13 Z",true); break;
            case IconKind.Close: Line(5,5,15,15); Line(5,15,15,5); break;
            case IconKind.Chevron: Path("M7 4 L13 10 L7 16"); break;
            case IconKind.Audio: Path("M3 7 L7 7 L12 3 L12 17 L7 13 L3 13 Z M15 6 Q19 10 15 14"); break;
        }
        canvas.RestoreToCount(saved);
    }
}
