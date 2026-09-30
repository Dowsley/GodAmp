using System;
using GodAmp.Presentation.Components;

namespace GodAmp.Presentation.MainPanel;

/// <summary>Displays elapsed playback time using the compact panel's bitmap text font.</summary>
public partial class CompactPlaybackClock : BitmapLabel
{
    private int _displayedSecond = -1;

    /// <summary>Refreshes the compact time text when the displayed second changes.</summary>
    /// <param name="positionSeconds">Elapsed playback position in seconds.</param>
    public void UpdateDisplay(double positionSeconds)
    {
        var time = TimeSpan.FromSeconds(positionSeconds);
        int wholeSeconds = (int)time.TotalSeconds;
        if (_displayedSecond == wholeSeconds)
            return;

        _displayedSecond = wholeSeconds;
        Text = $"{(int)time.TotalMinutes,3}:{time.Seconds:00}";
    }
}
