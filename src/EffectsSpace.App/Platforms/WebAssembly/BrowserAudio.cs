using System.Runtime.InteropServices.JavaScript;

namespace EffectsSpace.App;

internal static partial class BrowserAudio
{
    [JSImport("globalThis.effectsSpaceAudio.unlock")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Unlock();
    [JSImport("globalThis.effectsSpaceAudio.load")]
    [return: JSMarshalAs<JSType.Promise<JSType.String>>]
    internal static partial Task<string> Load(string waveBase64);
    [JSImport("globalThis.effectsSpaceAudio.play")]
    internal static partial void Play(double offset, bool loop);
    [JSImport("globalThis.effectsSpaceAudio.stop")]
    internal static partial void Stop();
    [JSImport("globalThis.effectsSpaceAudio.position")]
    internal static partial double Position();
}
