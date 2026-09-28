namespace EffectsSpace.Media;

/// <summary>Immutable, DC-normalized fractional-delay bank. Cached banks are independent of source buffers.</summary>
internal sealed class SincKernel
{
    public const int Phases = 256;
    private const int MaximumBanks = 12;
    private static readonly object Sync = new();
    private static readonly Dictionary<int, (SincKernel Kernel, long Stamp)> Cache = [];
    private static long _stamp;
    public int Taps { get; }
    public float[] Weights { get; }

    private SincKernel(int band)
    {
        double step = Math.Pow(2, band / 8d);
        Taps = Math.Min(512, 64 * (int)Math.Ceiling(step));
        Weights = new float[(Phases + 1) * Taps];
        double radius = Taps / 2d;
        double cutoff = .94 / step;
        for (int phase = 0; phase <= Phases; phase++)
        {
            double fraction = phase / (double)Phases;
            double sum = 0;
            int offset = phase * Taps;
            for (int tap = 0; tap < Taps; tap++)
            {
                double x = tap - (Taps / 2 - 1) - fraction;
                double window = Math.Abs(x) >= radius ? 0 : .42 + .5 * Math.Cos(Math.PI * x / radius) + .08 * Math.Cos(2 * Math.PI * x / radius);
                double sinc = Math.Abs(x) < 1e-14 ? cutoff : Math.Sin(Math.PI * cutoff * x) / (Math.PI * x);
                float weight = (float)(sinc * window);
                Weights[offset + tap] = weight;
                sum += weight;
            }
            for (int tap = 0; tap < Taps; tap++) Weights[offset + tap] = (float)(Weights[offset + tap] / sum);
        }
    }

    public static int Band(double step)
    {
        if (!double.IsFinite(step) || step < 0 || step > 32 + 1e-10)
            throw new NotSupportedException("Band-limited audio supports at most 32 source frames per output frame. Reduce varispeed or select Linear explicitly.");
        // Round the rate upward: the cutoff never exceeds the requested output Nyquist frequency.
        return Math.Clamp((int)Math.Ceiling(Math.Log2(Math.Max(1, step)) * 8 - 1e-10), 0, 40);
    }

    public static SincKernel Get(int band)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(band, out var found))
            {
                Cache[band] = (found.Kernel, ++_stamp);
                return found.Kernel;
            }
            var kernel = new SincKernel(band);
            if (Cache.Count >= MaximumBanks) Cache.Remove(Cache.MinBy(p => p.Value.Stamp).Key);
            Cache.Add(band, (kernel, ++_stamp));
            return kernel;
        }
    }
}
