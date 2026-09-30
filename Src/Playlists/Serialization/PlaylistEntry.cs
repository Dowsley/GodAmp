namespace GodAmp.Playlists.Serialization;

/// <summary>One playlist occurrence, with optional cached display metadata.</summary>
/// <param name="Path">Source path or URL; the file layer resolves relative paths after parsing.</param>
/// <param name="Title">Playlist display title, independent of source tags.</param>
/// <param name="Duration">Known duration in seconds, or null for unknown duration.</param>
public sealed record PlaylistEntry(string Path, string? Title = null, float? Duration = null);
