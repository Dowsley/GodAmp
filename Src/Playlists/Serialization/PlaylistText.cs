using System;
using System.Globalization;

namespace GodAmp.Playlists.Serialization;

/// <summary>Shared scalar conventions for M3U and PLS cached metadata.</summary>
internal static class PlaylistText
{
    /// <summary>Reads nonnegative seconds; negative, invalid and nonfinite durations are unknown.</summary>
    /// <param name="value">Invariant numeric text from a playlist duration field.</param>
    /// <returns>Known seconds, or null for unknown duration.</returns>
    public static float? ParseDuration(string? value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && float.IsFinite(seconds) && seconds >= 0
            ? seconds : null;

    /// <summary>Formats whole seconds using the playlist convention of -1 for unknown duration.</summary>
    /// <param name="duration">Optional duration in seconds.</param>
    /// <returns>Invariant whole seconds, or the unknown-duration marker.</returns>
    public static string FormatDuration(float? duration) => duration is { } seconds && float.IsFinite(seconds) && seconds >= 0
        ? Math.Floor(seconds).ToString(CultureInfo.InvariantCulture) : "-1";

    /// <summary>Flattens a display title to one playlist line without altering punctuation.</summary>
    /// <param name="title">Optional display title.</param>
    /// <returns>A single-line title, or an empty string when absent.</returns>
    public static string FormatTitle(string? title) => (title ?? "").Replace('\r', ' ').Replace('\n', ' ');
}
