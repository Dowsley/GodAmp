using GodAmp.Data;

namespace GodAmp.Utils;

public static class AudioUtils
{
    public static string GetFullTrackTitle(Track track, int trackNumber) => track.UseFileName
        ? track.Name
        : $"{trackNumber}. {track.Artist} - {track.Name}";
}
