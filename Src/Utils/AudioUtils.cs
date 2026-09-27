using GodAmp.Data;

namespace GodAmp.Utils;

public static class AudioUtils
{
    public const int SpectrumAnalyzerAudioEffectIndex = 0;
    public const int AmplifyAudioEffectIndex = 1;
    public const int Eq10AudioEffectIndex = 2;
    public const int PannerAudioEffectIndex = 3;
    /// <summary>Master-bus capture used by the classic oscilloscope.</summary>
    public const int OscilloscopeAudioEffectIndex = 4;

    public static string GetFullTrackTitle(Track track, int trackNumber) => track.UseFileName
        ? track.Name
        : $"{trackNumber}. {track.Artist} - {track.Name}";
}
