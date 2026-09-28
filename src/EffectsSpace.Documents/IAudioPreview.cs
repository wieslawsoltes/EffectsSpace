namespace EffectsSpace.Documents;

/// <summary>Optional platform audio output. Prepared PCM WAVE is bounded by the host; the output clock drives the UI.</summary>
public interface IAudioPreview
{
    Task UnlockAsync();
    Task LoadAsync(byte[] pcmWave);
    void Play(double offsetSeconds, bool loop);
    void Stop();
    double PositionSeconds { get; }
}
