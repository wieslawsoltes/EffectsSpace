using EffectsSpace.Core;
using SkiaSharp;

namespace EffectsSpace.Skia;

public static class PathGeometry
{
    public static SKPath Build(ShapePath shape)
    {
        var path = new SKPath(); if (shape.Nodes.Count == 0) return path;
        var first = shape.Nodes[0]; path.MoveTo((float)first.Point.X, (float)first.Point.Y);
        void Segment(PathNode a, PathNode b)
        {
            var p = a.Point + a.OutHandle; var q = b.Point + b.InHandle;
            if (a.OutHandle.Length + b.InHandle.Length < 1e-9) path.LineTo((float)b.Point.X, (float)b.Point.Y);
            else path.CubicTo((float)p.X, (float)p.Y, (float)q.X, (float)q.Y, (float)b.Point.X, (float)b.Point.Y);
        }
        for (var i = 1; i < shape.Nodes.Count; i++) Segment(shape.Nodes[i - 1], shape.Nodes[i]);
        if (shape.Closed) { Segment(shape.Nodes[^1], first); path.Close(); }
        return path;
    }
    public static SKPath LayerPath(Layer layer)
    {
        if (layer.Kind == LayerKind.Path) return Build(layer.Path);
        var path = new SKPath(); var r = SKRect.Create((float)layer.Width, (float)layer.Height);
        if (layer.Kind == LayerKind.Ellipse) path.AddOval(r);
        else if (layer.Kind == LayerKind.Star)
        {
            var cx = r.MidX; var cy = r.MidY; var radius = Math.Min(cx, cy);
            for (var i = 0; i < layer.StarPoints * 2; i++)
            {
                var a = i * Math.PI / layer.StarPoints - Math.PI / 2; var d = radius * (i % 2 == 0 ? 1 : layer.InnerRadius);
                var x = cx + (float)(Math.Cos(a) * d); var y = cy + (float)(Math.Sin(a) * d);
                if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }
            path.Close();
        }
        else if (layer.Kind == LayerKind.Rectangle) path.AddRoundRect(r, (float)layer.CornerRadius, (float)layer.CornerRadius);
        else path.AddRect(r);
        return path;
    }
}
