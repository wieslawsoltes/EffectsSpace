namespace EffectsSpace.Media;

public enum AudioResamplingQuality
{
    /// <summary>Legacy, inexpensive interpolation without downsampling antialias filtering.</summary>
    Linear,
    /// <summary>Rate-adaptive Blackman-windowed sinc, phase-interpolated and bounded to 32 source frames per output frame.</summary>
    BandLimited
}
