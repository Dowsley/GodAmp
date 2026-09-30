namespace GodAmp.Audio.Processing;

/// <summary>Persisted audio preferences; band gains follow the engine's ten-band EQ order.</summary>
public sealed record AudioSettings(float Volume, float Balance, float PreampDb, bool EqualizerEnabled, float[] BandGains)
{
    public const float DefaultVolume = 0.8f;
    public const int EqualizerBandCount = 10;
}
