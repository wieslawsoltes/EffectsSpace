namespace EffectsSpace.Core;

public enum LayerKind { Solid, Rectangle, Ellipse, Star, Path, Text, Image, Video, Audio, Null, Composition }
public enum LayerBlend { Normal, Multiply, Screen, Add, Overlay, SoftLight, HardLight, Difference, Darken, Lighten, ColorDodge, ColorBurn, Exclusion }
public enum TrackMatte { Alpha, AlphaInverted }

public sealed class Layer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Layer";
    public LayerKind Kind { get; set; } = LayerKind.Rectangle;
    public bool Enabled { get; set; } = true;
    public bool Solo { get; set; }
    public bool Locked { get; set; }
    public bool Shy { get; set; }
    public string Label { get; set; } = "#AA91D2";
    public string? ParentId { get; set; }
    public string? SourceId { get; set; }
    public string? MatteId { get; set; }
    public TrackMatte Matte { get; set; }
    public LayerBlend Blend { get; set; }
    public double InPoint { get; set; }
    public double OutPoint { get; set; } = 8;
    public double StartTime { get; set; }
    public double Stretch { get; set; } = 1;
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 200;
    public AnimatedTransform Transform { get; set; } = new();
    public bool FillEnabled { get; set; } = true;
    public string Fill { get; set; } = "#7D67FF";
    public string? GradientEnd { get; set; }
    public string Stroke { get; set; } = "#FFFFFF";
    public double StrokeWidth { get; set; }
    public double CornerRadius { get; set; } = 24;
    public int StarPoints { get; set; } = 5;
    public double InnerRadius { get; set; } = 0.45;
    public string Text { get; set; } = "Your text";
    public double FontSize { get; set; } = 96;
    public string FontFamily { get; set; } = "Inter";
    public ShapePath Path { get; set; } = new();
    public List<LayerMask> Masks { get; set; } = [];
    public List<LayerEffect> Effects { get; set; } = [];
    public double SourceTime(double compositionTime) => (compositionTime - StartTime) / Stretch;
    public bool ActiveAt(double time) => Enabled && time >= InPoint && time < OutPoint;
}
