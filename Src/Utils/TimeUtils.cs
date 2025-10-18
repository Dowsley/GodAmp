using System;

namespace GodAmp.Utils;

public static class TimeUtils
{
    public static string FormatAsTrackTime(float seconds, int minuteDigits = 1)
    {
        var time = TimeSpan.FromSeconds(seconds);
        string minFmt = $"D{minuteDigits}";
        string minutes = ((int)time.TotalMinutes).ToString(minFmt);
        string secondsPart = time.Seconds.ToString("D2");  // always two-digit seconds
        return $"{minutes}:{secondsPart}";
    }
}