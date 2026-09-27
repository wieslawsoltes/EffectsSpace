using EffectsSpace.Core;

namespace EffectsSpace.Editing;

/// <summary>Original artwork constructed entirely from editable layers and keyframes.</summary>
public static class SampleProject
{
    public static MotionProject Create()
    {
        var c = new Composition { Name = "01 · Orbital / Main", Duration = 8, WorkEnd = 8 };
        var p = new MotionProject { Name = "ORBITAL — Motion study", ActiveCompositionId = c.Id, Compositions = [c] };
        Layer Add(LayerKind kind, string name, double x, double y, double w, double h, string fill)
        {
            var l = new Layer { Kind = kind, Name = name, Width = w, Height = h, Fill = fill, OutPoint = c.Duration };
            l.Transform.X.Value = x; l.Transform.Y.Value = y; l.Transform.AnchorX.Value = w / 2; l.Transform.AnchorY.Value = h / 2;
            c.Layers.Insert(0, l); return l;
        }
        var background = Add(LayerKind.Solid, "Background / Midnight", 960, 540, 1920, 1080, "#10131C"); background.GradientEnd = "#202743"; background.Locked = true; background.Label = "#687C91";
        var haze = Add(LayerKind.Ellipse, "Atmosphere / soft violet", 1480, 520, 620, 620, "#5646D1"); haze.Transform.Opacity.Value = 28; var blur = EffectCatalog.Create(EffectKind.GaussianBlur); blur.Parameters["Radius"].Value = 90; haze.Effects.Add(blur);
        var orbit = new Composition { Name = "02 · Orbital system", Width = 1000, Height = 1000, Duration = 8, WorkEnd = 8 };
        p.Compositions.Add(orbit);
        for (var i = 0; i < 4; i++)
        {
            var size = 420 + i * 125;
            var ring = new Layer { Name = "Orbit " + (i + 1), Kind = LayerKind.Ellipse, Width = size, Height = size, FillEnabled = false, Stroke = i % 2 == 0 ? "#8184FE" : "#69D7CE", StrokeWidth = i == 3 ? 2 : 3, OutPoint = 8, Label = "#8BD5C7" };
            ring.Transform.X.Value = ring.Transform.Y.Value = 500; ring.Transform.AnchorX.Value = ring.Transform.AnchorY.Value = size / 2;
            ring.Transform.ScaleX.Value = 72 + i * 8; ring.Transform.Rotation.Value = -30 + i * 14;
            ring.Transform.Rotation.Expression = $"value + time * {4 + i * 2}"; orbit.Layers.Add(ring);
        }
        var dot = new Layer { Name = "Satellite / orbiting", Kind = LayerKind.Ellipse, Width = 38, Height = 38, Fill = "#B9F6D2", OutPoint = 8, Label = "#9ACAAB" };
        dot.Transform.AnchorX.Value = dot.Transform.AnchorY.Value = 19;
        dot.Transform.X.Expression = "500 + cos(time * 0.75) * 320"; dot.Transform.Y.Expression = "500 + sin(time * 0.75) * 320";
        var glow = EffectCatalog.Create(EffectKind.Glow); glow.Parameters["Radius"].Value = 25; dot.Effects.Add(glow); orbit.Layers.Insert(0, dot);
        var system = Add(LayerKind.Composition, "Orbital system", 1410, 500, 1000, 1000, "#FFFFFF"); system.SourceId = orbit.Id; system.Label = "#92B3E8";
        Animate(system.Transform.ScaleX, (0, 50), (1.8, 100), (7.9, 108)); Animate(system.Transform.ScaleY, (0, 50), (1.8, 100), (7.9, 108));
        var rule = Add(LayerKind.Rectangle, "Accent rule", 320, 251, 272, 4, "#A7F2D0"); rule.CornerRadius = 0; rule.Label = "#9ACAAB";
        var eyebrow = Add(LayerKind.Text, "MOTION / STUDIES", 430, 210, 500, 48, "#A7F2D0"); eyebrow.Text = "MOTION  /  STUDIES"; eyebrow.FontSize = 27; eyebrow.Label = "#D78383";
        var title = Add(LayerKind.Text, "ORBITAL", 670, 460, 980, 210, "#F2F1FF"); title.Text = "ORBITAL"; title.FontSize = 170; title.Label = "#D78383";
        Animate(title.Transform.Y, (0, 590), (0.8, 460), (6.3, 460), (7.9, 400)); Animate(title.Transform.Opacity, (0, 0), (0.7, 100), (6.7, 100), (7.9, 0));
        var subtitle = Add(LayerKind.Text, "A little space. A lot of motion.", 700, 615, 1040, 90, "#A0A8C3"); subtitle.Text = "A little space. A lot of motion."; subtitle.FontSize = 43; subtitle.Label = "#D78383";
        Animate(subtitle.Transform.Opacity, (0.3, 0), (1.5, 100), (6.8, 100), (7.9, 0));
        var badge = Add(LayerKind.Rectangle, "Chapter chip", 281, 782, 198, 53, "#A7F2D0"); badge.CornerRadius = 26;
        var badgeText = Add(LayerKind.Text, "EXPERIMENT 001", 281, 785, 169, 31, "#141D24"); badgeText.Text = "EXPERIMENT 001"; badgeText.FontSize = 19;
        var footer = Add(LayerKind.Text, "Composition signature", 500, 976, 635, 35, "#707B97"); footer.Text = "EFFECTSSPACE     /     ORIGINAL MOTION DESIGN"; footer.FontSize = 20;
        c.Markers = [new() { Time = 0, Name = "INTRO" }, new() { Time = 2.4, Name = "HERO" }, new() { Time = 6.4, Name = "OUTRO" }];
        return p;
    }
    private static void Animate(Channel c, params (double Time, double Value)[] keys) { foreach (var (t, v) in keys) c.SetKey(t, v); }
}
