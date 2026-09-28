using EffectsSpace.Controls;
using EffectsSpace.Media;
using Microsoft.UI.Xaml;

namespace EffectsSpace.Workbench;

public sealed partial class StudioWorkbench
{
    private bool _showAudioDelivery;
    private WaveEncoding _waveEncoding = WaveEncoding.Pcm16;
    private int _waveSampleRate = 48000;
    private bool _waveDither;
    private AudioResamplingQuality _audioQuality = AudioResamplingQuality.BandLimited;

    private void OpenAudioDelivery()
    {
        _showAudioDelivery = true;
        _rightPanel.Select("Media");
        RefreshMedia();
        Focus(FocusState.Programmatic);
    }

    private void RefreshAudioDelivery()
    {
        _mediaPanel.Children.Clear();
        _audioGainField = _audioPanField = null;
        _mediaPanel.Children.Add(Heading("AUDIO DELIVERY"));
        _mediaPanel.Children.Add(Button("Back to Media", OpenMedia, IconKind.Previous));
        _mediaPanel.Children.Add(Note("Delivery settings belong to this workspace session, not the project or undo history."));
        _mediaPanel.Children.Add(Heading("WAVE format"));
        foreach (var (name, encoding) in new[]
        {
            ("WAVE PCM 16-bit", WaveEncoding.Pcm16),
            ("WAVE PCM 24-bit", WaveEncoding.Pcm24),
            ("WAVE Float 32-bit", WaveEncoding.Float32)
        })
        {
            var button = Button(name, () => ConfigureDelivery(() =>
            {
                _waveEncoding = encoding;
                if (encoding == WaveEncoding.Float32) _waveDither = false;
            }));
            button.Active = _waveEncoding == encoding;
            button.IsEnabled = !IsRendering;
            _mediaPanel.Children.Add(button);
        }
        _mediaPanel.Children.Add(Heading("WAVE sample rate"));
        var rates = Studio.Row();
        foreach (int rate in new[] { 44100, 48000, 96000 })
        {
            var button = Button($"{rate / 1000d:0.#} kHz", () => ConfigureDelivery(() => _waveSampleRate = rate));
            button.Active = _waveSampleRate == rate;
            button.IsEnabled = !IsRendering;
            rates.Children.Add(button);
        }
        _mediaPanel.Children.Add(rates);
        var dither = Button("TPDF Dither", () => ConfigureDelivery(() => _waveDither = !_waveDither));
        dither.Active = _waveDither;
        dither.IsEnabled = !IsRendering && _waveEncoding != WaveEncoding.Float32;
        _mediaPanel.Children.Add(dither);
        _mediaPanel.Children.Add(Note(_waveEncoding == WaveEncoding.Float32
            ? "Float preserves finite headroom above 0 dBFS; it does not apply a limiter or dither."
            : "Integer delivery saturates at full scale. Optional TPDF dither is indexed by sample, so block size does not change the file."));
        _mediaPanel.Children.Add(Heading("Preview / AVI / WAVE resampling"));
        foreach (var (name, quality) in new[]
        {
            ("Band-limited resampling", AudioResamplingQuality.BandLimited),
            ("Linear resampling", AudioResamplingQuality.Linear)
        })
        {
            var button = Button(name, () => ConfigureDelivery(() =>
            {
                if (_audioQuality == quality) return;
                Pause();
                _preparedAudio = null;
                _audioQuality = quality;
            }));
            button.Active = _audioQuality == quality;
            button.IsEnabled = !IsRendering;
            _mediaPanel.Children.Add(button);
        }
        _mediaPanel.Children.Add(Note("Band-limited uses a rate-adaptive windowed-sinc filter. It changes pitch with varispeed and is not pitch-preserving stretch. Maximum source/output sample step: 32. Linear is a lower-cost, unfiltered compatibility option."));
        _mediaPanel.Children.Add(Button("Render WAVE with Settings", () => _ = RunAsync(ExportAudioAsync), IconKind.Audio));
        _mediaPanel.Children.Add(Note("AVI remains stereo PCM16/48 kHz. PNG has alpha; AVI has no alpha. Native preview is still silent."));
    }

    private void ConfigureDelivery(Action change)
    {
        if (IsRendering) throw new InvalidOperationException("Finish or cancel the current export before changing delivery settings.");
        change();
        RefreshAudioDelivery();
        Focus(FocusState.Programmatic);
        ShowStatus($"WAVE: {_waveEncoding} / {_waveSampleRate} Hz / {(_waveDither ? "TPDF" : "no dither")} · {_audioQuality}");
    }
}
