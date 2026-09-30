namespace GodAmp.Media;

/// <summary>Defines the display-title policy shared by queue ordering, playlist files and views.</summary>
public static class TrackTitle
{
    /// <summary>Formats a track's display title without a queue position.</summary>
    /// <param name="track">Metadata and filename-fallback preference.</param>
    /// <returns>The playlist title, or source metadata with a filename fallback.</returns>
    public static string Format(Track track) => !string.IsNullOrWhiteSpace(track.PlaylistTitle)
        ? track.PlaylistTitle : track.UseFileName ? track.Name : $"{track.Artist} - {track.Name}";

    /// <summary>Formats the playlist title with its position when metadata is available.</summary>
    /// <param name="track">Metadata and filename-fallback preference.</param>
    /// <param name="trackNumber">One-based queue position.</param>
    /// <returns>The numbered display title, or an unnumbered filename fallback.</returns>
    public static string FormatNumbered(Track track, int trackNumber) => track.UseFileName && string.IsNullOrWhiteSpace(track.PlaylistTitle)
        ? Format(track) : $"{trackNumber}. {Format(track)}";
}
