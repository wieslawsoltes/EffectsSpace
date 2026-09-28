namespace EffectsSpace.Media;

/// <summary>Caller-owned stereo sampler with an 8 KiB decoded window. Instances are single-reader, not thread-safe.</summary>
/// <remarks>No source buffer is copied in full. Sequential reads, reverse playback and seeks share the same bounded window.</remarks>
public sealed class PcmResampler
{
    private const int WindowFrames = 1024;
    private readonly PcmSource _source;
    private readonly float[] _window = new float[WindowFrames * 2];
    private long _windowStart = long.MinValue;
    private int _band = -1;
    private SincKernel? _kernel;
    public AudioResamplingQuality Quality { get; }
    public long WindowLoads { get; private set; }
    public long FilteredFrames { get; private set; }
    public long DirectFrames { get; private set; }

    public PcmResampler(PcmSource source, AudioResamplingQuality quality = AudioResamplingQuality.BandLimited)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(quality)) throw new ArgumentOutOfRangeException(nameof(quality));
        _source = source;
        Quality = quality;
    }

    private ReadOnlySpan<float> Window(long first, int count)
    {
        if (_windowStart == long.MinValue || first < _windowStart || first - _windowStart > WindowFrames - count)
        {
            // A centered margin avoids alternating window reloads around reverse-playback boundaries.
            _windowStart = first - (WindowFrames - count) / 2;
            _source.ReadStereoFrames(_windowStart, _window);
            WindowLoads++;
        }
        return _window.AsSpan(checked((int)(first - _windowStart) * 2), count * 2);
    }

    public void Sample(double sourceTime, double sourceSecondsPerSecond, int outputSampleRate, out float left, out float right)
    {
        if (!double.IsFinite(sourceTime) || !double.IsFinite(sourceSecondsPerSecond) || outputSampleRate is < 8000 or > 192000)
            throw new ArgumentOutOfRangeException(nameof(sourceTime));
        left = right = 0;
        double position = (sourceTime - _source.StartTime) * _source.Format.SampleRate;
        if (position < 0 || position >= _source.FrameCount || Math.Abs(sourceSecondsPerSecond) < 1e-12) return;
        long frame = (long)Math.Floor(position);
        double fraction = position - frame;
        double step = Math.Abs(sourceSecondsPerSecond) * _source.Format.SampleRate / outputSampleRate;
        if (Quality == AudioResamplingQuality.Linear)
        {
            var samples = Window(frame, 2);
            float f = (float)fraction;
            left = samples[0] + ((frame + 1 < _source.FrameCount ? samples[2] : samples[0]) - samples[0]) * f;
            right = samples[1] + ((frame + 1 < _source.FrameCount ? samples[3] : samples[1]) - samples[1]) * f;
            DirectFrames++;
            return;
        }
        if (Math.Abs(step - 1) < 1e-12 && (fraction < 1e-8 || fraction > 1 - 1e-8))
        {
            var samples = Window(frame + (fraction > .5 ? 1 : 0), 1);
            left = samples[0];
            right = samples[1];
            DirectFrames++;
            return;
        }
        int band = SincKernel.Band(step);
        if (_kernel is null || _band != band)
        {
            _kernel = SincKernel.Get(band);
            _band = band;
        }
        var kernel = _kernel;
        var source = Window(frame - (kernel.Taps / 2 - 1), kernel.Taps);
        double phase = fraction * SincKernel.Phases;
        int index = Math.Min(SincKernel.Phases - 1, (int)phase);
        float blend = (float)(phase - index);
        int offset = index * kernel.Taps;
        double l = 0, r = 0;
        for (int tap = 0; tap < kernel.Taps; tap++)
        {
            float a = kernel.Weights[offset + tap];
            float weight = a + (kernel.Weights[offset + kernel.Taps + tap] - a) * blend;
            l += source[tap * 2] * weight;
            r += source[tap * 2 + 1] * weight;
        }
        left = (float)l;
        right = (float)r;
        FilteredFrames++;
    }
}
