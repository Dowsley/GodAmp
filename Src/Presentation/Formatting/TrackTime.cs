using System;

namespace GodAmp.Presentation.Formatting;

/// <summary>Formats track durations for the player's minute-and-second displays.</summary>
public static class TrackTime
{
    /// <summary>Formats elapsed or total duration without wrapping minutes at one hour.</summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <param name="minuteDigits">Minimum number of digits in the minute field.</param>
    /// <returns>Minutes and a two-digit seconds field separated by a colon.</returns>
    public static string Format(float seconds, int minuteDigits = 1)
    {
        var time = TimeSpan.FromSeconds(seconds);
        string minFmt = $"D{minuteDigits}";
        string minutes = ((int)time.TotalMinutes).ToString(minFmt);
        string secondsPart = time.Seconds.ToString("D2");
        return $"{minutes}:{secondsPart}";
    }
}
