namespace EffectsSpace.Core;

public sealed record EffectParameter(string Key, string Name, double Default, double Minimum, double Maximum, double Step = 1);
public sealed record EffectDefinition(EffectKind Kind, string Name, string Category, params EffectParameter[] Parameters);

public static class EffectCatalog
{
    public static IReadOnlyList<EffectDefinition> All { get; } =
    [
        new(EffectKind.GaussianBlur, "Gaussian Blur", "Blur & Sharpen", new("Radius", "Blurriness", 12, 0, 128)),
        new(EffectKind.Glow, "Glow", "Stylize", new("Radius", "Glow Radius", 32, 0, 128), new("Strength", "Glow Intensity", 80, 0, 100)),
        new(EffectKind.DropShadow, "Drop Shadow", "Perspective", new("Radius", "Softness", 18, 0, 128), new("Distance", "Distance", 20, 0, 400), new("Angle", "Direction", 135, -360, 360), new("Opacity", "Opacity", 65, 0, 100)),
        new(EffectKind.Exposure, "Exposure", "Color Correction", new("Exposure", "Exposure", 0.5, -8, 8, 0.1)),
        new(EffectKind.BrightnessContrast, "Brightness & Contrast", "Color Correction", new("Brightness", "Brightness", 10, -100, 100), new("Contrast", "Contrast", 10, -100, 100)),
        new(EffectKind.Saturation, "Hue / Saturation", "Color Correction", new("Hue", "Master Hue", 0, -180, 180), new("Saturation", "Master Saturation", 0, -100, 200)),
        new(EffectKind.Tint, "Tint", "Color Correction", new("Amount", "Amount to Tint", 100, 0, 100)),
        new(EffectKind.Invert, "Invert", "Channel"),
        new(EffectKind.Posterize, "Posterize", "Stylize", new("Levels", "Levels", 6, 2, 32)),
        new(EffectKind.FractalNoise, "Fractal Noise", "Noise & Grain", new("Scale", "Scale", 160, 8, 1000), new("Complexity", "Complexity", 4, 1, 8), new("Evolution", "Evolution", 0, 0, 3600)),
        new(EffectKind.Vignette, "Vignette", "Stylize", new("Amount", "Amount", 60, 0, 100))
    ];
    public static EffectDefinition Get(EffectKind kind) => All.First(e => e.Kind == kind);
    public static LayerEffect Create(EffectKind kind) => new()
    {
        Kind = kind,
        Color = kind == EffectKind.DropShadow ? "#000000" : "#65D6FF",
        Parameters = Get(kind).Parameters.ToDictionary(p => p.Key, p => new Channel(p.Default), StringComparer.Ordinal)
    };
}
