using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using EffectsSpace.Core;

namespace EffectsSpace.Documents;

public static partial class ProjectValidator
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)] private static partial Regex ColorPattern();
    public static void Validate(MotionProject project)
    {
        void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        void Finite(double n, string name, double max = 1e9) => Require(double.IsFinite(n) && Math.Abs(n) <= max, $"Invalid {name}.");
        void Color(string? c) => Require(c is not null && ColorPattern().IsMatch(c), "Colors must use #RRGGBB notation.");
        var keys = 0; var layers = 0; var ids = new HashSet<string>(StringComparer.Ordinal);
        void Id(string? id) => Require(!string.IsNullOrWhiteSpace(id) && id.Length <= 128 && ids.Add(id), "Missing or duplicate identifier.");
        void Channel(Channel? channel)
        {
            Require(channel is not null && channel.Keys is not null && channel.Expression is not null, "Invalid channel.");
            Finite(channel!.Value, "channel value"); Require(channel.Expression.Length <= 1024, "Expression is too long.");
            var previous = double.NegativeInfinity;
            foreach (var key in channel.Keys)
            {
                Require(key is not null, "Null keyframe."); Id(key!.Id); Finite(key.Time, "key time", 86400); Finite(key.Value, "key value");
                Require(key.Time >= 0 && key.Time > previous, "Keyframes must be sorted and unique in time."); previous = key.Time;
                Require(Enum.IsDefined(key.Interpolation) && key.X1 >= 0 && key.X1 <= 1 && key.X2 >= 0 && key.X2 <= 1, "Invalid interpolation handles.");
                Finite(key.Y1, "Bezier handle", 100); Finite(key.Y2, "Bezier handle", 100); Require(++keys <= 100000, "Too many keyframes.");
            }
        }
        void Path(ShapePath? path)
        {
            Require(path is not null && path.Nodes is not null && path.Nodes.Count <= 50000, "Invalid path.");
            foreach (var node in path!.Nodes)
            {
                Require(node is not null, "Null path node.");
                foreach (var p in new[] { node!.Point, node.InHandle, node.OutHandle }) { Finite(p.X, "path coordinate"); Finite(p.Y, "path coordinate"); }
            }
        }
        Require(project.SchemaVersion == 1, "Unsupported project schema version.");
        Require(project.Compositions is { Count: > 0 and <= 128 } && project.Assets is { Count: <= 256 }, "Invalid project collections.");
        Require(project.Name is { Length: <= 4096 }, "Invalid project name.");
        long assetBytes = 0;
        foreach (var asset in project.Assets)
        {
            Require(asset is not null, "Null asset."); Id(asset!.Id);
            Require(asset.Data is not null && asset.Data.Length <= 32 * 1024 * 1024, "An asset exceeds 32 MiB.");
            assetBytes += asset.Data!.Length; Require(assetBytes <= 64 * 1024 * 1024, "Embedded assets exceed 64 MiB.");
            Require(asset.MimeType is "image/png" or "image/jpeg" or "image/webp" or "video/mp4" or "video/webm" or "audio/wav" or "audio/mpeg" or "audio/ogg" or "audio/webm", "Unsupported media type.");
            Require(asset.Width >= 0 && asset.Height >= 0 && asset.Width <= 8192 && asset.Height <= 8192, "Invalid media dimensions."); Finite(asset.Duration, "asset duration", 86400);
        }
        foreach (var comp in project.Compositions)
        {
            Require(comp is not null, "Null composition."); Id(comp!.Id);
            Require(comp.Name is { Length: <= 4096 }, "Invalid composition name.");
            Require(comp.Width is >= 1 and <= 8192 && comp.Height is >= 1 and <= 8192, "Composition dimensions must be 1–8192 pixels.");
            Require(comp.FrameRate.Numerator is > 0 and <= 240000 && comp.FrameRate.Denominator is > 0 and <= 10000 && comp.FrameRate.FramesPerSecond is >= 1 and <= 240, "Invalid frame rate.");
            Finite(comp.Duration, "composition duration", 3600); Require(comp.Duration >= comp.FrameRate.Seconds(1), "Composition must contain at least one frame.");
            Finite(comp.WorkStart, "work area"); Finite(comp.WorkEnd, "work area"); Require(comp.WorkStart >= 0 && comp.WorkStart < comp.WorkEnd && comp.WorkEnd <= comp.Duration, "Invalid work area.");
            Color(comp.Background); Require(comp.Layers is not null && comp.Markers is not null, "Invalid composition collections.");
            foreach (var layer in comp.Layers!)
            {
                Require(layer is not null, "Null layer."); Id(layer!.Id); Require(++layers <= 5000, "Too many layers.");
                Require(layer.Name is { Length: <= 4096 } && layer.Text is { Length: <= 100000 }, "Invalid layer text.");
                Require(Enum.IsDefined(layer.Kind) && Enum.IsDefined(layer.Blend) && Enum.IsDefined(layer.Matte), "Invalid layer type.");
                Finite(layer.InPoint, "in point", 86400); Finite(layer.OutPoint, "out point", 86400); Finite(layer.StartTime, "start time", 86400);
                Require(layer.InPoint >= 0 && layer.OutPoint > layer.InPoint && layer.OutPoint <= comp.Duration + 1e-7, "Invalid layer interval.");
                Finite(layer.Stretch, "time stretch", 1000); Require(Math.Abs(layer.Stretch) >= 0.001, "Time stretch cannot be zero.");
                Finite(layer.Width, "layer width", 32768); Finite(layer.Height, "layer height", 32768); Require(layer.Width > 0 && layer.Height > 0, "Invalid layer dimensions.");
                Finite(layer.FontSize, "font size", 4096); Require(layer.FontSize > 0, "Font size must be positive.");
                Finite(layer.StrokeWidth, "stroke width", 4096); Require(layer.StrokeWidth >= 0, "Invalid stroke width.");
                Finite(layer.CornerRadius, "corner radius", 32768); Finite(layer.InnerRadius, "star radius", 1); Require(layer.InnerRadius > 0 && layer.StarPoints is >= 3 and <= 128, "Invalid star.");
                Color(layer.Fill); Color(layer.Stroke); Color(layer.Label); if (layer.GradientEnd is not null) Color(layer.GradientEnd);
                Require(layer.Transform is not null && layer.Masks is { Count: <= 64 } && layer.Effects is { Count: <= 64 }, "Invalid layer collections.");
                foreach (var (_, channel) in layer.Transform!.Channels()) Channel(channel);
                Channel(layer.TimeRemap);
                if (layer.TimeRemapEnabled)
                    Require(layer.Kind is LayerKind.Composition or LayerKind.Video or LayerKind.Audio, "Time remapping requires a time-based source.");
                Path(layer.Path);
                foreach (var mask in layer.Masks!) { Require(mask is not null, "Null mask."); Id(mask!.Id); Require(Enum.IsDefined(mask.Mode), "Invalid mask mode."); Finite(mask.Feather, "mask feather", 512); Require(mask.Feather >= 0, "Invalid feather.");
                    Finite(mask.Opacity, "mask opacity", 100); Require(mask.Opacity >= 0, "Invalid mask opacity.");
                    Finite(mask.Expansion, "mask expansion", 512); Path(mask.Path); }
                foreach (var effect in layer.Effects!)
                {
                    Require(effect is not null && effect.Parameters is { Count: <= 32 }, "Invalid effect."); Id(effect!.Id); Require(Enum.IsDefined(effect.Kind), "Invalid effect kind."); Color(effect.Color);
                    foreach (var channel in effect.Parameters.Values) Channel(channel);
                }
                if (layer.Kind == LayerKind.Adjustment)
                {
                    Require(layer.Blend == LayerBlend.Normal, "Adjustment layers require Normal blending.");
                    Require(!layer.Effects.Any(e => e.Enabled && e.Kind is EffectKind.FractalNoise or EffectKind.Vignette),
                        "Fill generators cannot be applied to an adjustment layer.");
                }
                if (layer.Kind == LayerKind.Composition) Require(project.Compositions.Any(c => c.Id == layer.SourceId), "Missing source composition.");
                if (layer.Kind is LayerKind.Image or LayerKind.Video or LayerKind.Audio) Require(project.Assets.Any(a => a.Id == layer.SourceId), "Missing media asset.");
            }
            foreach (var marker in comp.Markers!) { Require(marker is not null, "Null marker."); Id(marker!.Id); Finite(marker.Time, "marker time"); Require(marker.Time >= 0 && marker.Time < comp.Duration, "Marker outside composition."); Color(marker.Color); }
            var local = comp.Layers!.ToDictionary(l => l.Id);
            foreach (var layer in comp.Layers)
            {
                if (layer.ParentId is { } parent) Require(local.ContainsKey(parent), "Missing parent layer.");
                if (layer.MatteId is { } matte)
                {
                    Require(local.ContainsKey(matte), "Missing track matte.");
                    Require(local[matte].Kind != LayerKind.Adjustment, "Adjustment layers cannot be matte sources.");
                }
            }
            CheckCycles(local.Keys, id => new[] { local[id].ParentId, local[id].MatteId }.OfType<string>(), "Layer parent/matte cycle");
        }
        Require(project.Compositions.Any(c => c.Id == project.ActiveCompositionId), "Missing active composition.");
        var compositions = project.Compositions.ToDictionary(c => c.Id);
        CheckCycles(compositions.Keys, id => compositions[id].Layers.Where(l => l.Kind == LayerKind.Composition).Select(l => l.SourceId!), "Composition cycle");
    }

    private static void CheckCycles(IEnumerable<string> nodes, Func<string, IEnumerable<string>> edges, string message)
    {
        var done = new HashSet<string>(); var active = new HashSet<string>();
        void Visit(string id, int depth)
        {
            if (depth > 32) throw new InvalidDataException("Dependency nesting exceeds 32 levels.");
            if (done.Contains(id)) return;
            if (!active.Add(id)) throw new InvalidDataException(message);
            foreach (var next in edges(id)) Visit(next, depth + 1);
            active.Remove(id); done.Add(id);
        }
        foreach (var id in nodes) Visit(id, 0);
    }
}
