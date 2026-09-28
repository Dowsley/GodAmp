using GodAmp.Data;

namespace GodAmp.Utils;

public static class AudioUtils
{
    /// <summary>Formats a track's display title without a queue position.</summary>
    /// <param name="track">Metadata and filename-fallback preference.</param>
    /// <returns>The filename or artist and track name.</returns>
    public static string GetTrackTitle(Track track) => track.UseFileName
        ? track.Name : $"{track.Artist} - {track.Name}";

    /// <summary>Formats the playlist title with its position when metadata is available.</summary>
    /// <param name="track">Metadata and filename-fallback preference.</param>
    /// <param name="trackNumber">One-based queue position.</param>
    /// <returns>The filename or numbered artist and track name.</returns>
    public static string GetFullTrackTitle(Track track, int trackNumber) => track.UseFileName
        ? GetTrackTitle(track) : $"{trackNumber}. {GetTrackTitle(track)}";
}
