namespace GodAmp.Audio.Processing;

/// <summary>Persisted audio preferences; band gains follow the engine's ten-band EQ order.</summary>
public sealed record AudioSettings(float Volume, float Balance, float PreampDb, bool EqualizerEnabled, float[] BandGains)
{
    /// <summary>Initial slider position, corresponding to 30 dB of output attenuation.</summary>
    public const float DefaultVolume = 0.5f;
    public const int EqualizerBandCount = 10;
}
