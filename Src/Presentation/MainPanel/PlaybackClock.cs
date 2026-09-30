using System;
using Godot;

namespace GodAmp.Presentation.MainPanel;

/// <summary>Displays elapsed playback digits and animates their shared opacity.</summary>
public partial class PlaybackClock : Control
{
    [Export] private double _blinkIntervalSeconds = 1;
    [Export(PropertyHint.Range, "0,1")] private float _dimmedAlpha = 0.5f;
    [Export] private Label _minutesTens = null!;
    [Export] private Label _minutesOnes = null!;
    [Export] private Label _secondsTens = null!;
    [Export] private Label _secondsOnes = null!;

    private int _displayedSecond = -1;
    private double _blinkElapsed;
    private bool _dimmed;

    /// <summary>Updates the digits and advances blinking while playback is inactive.</summary>
    /// <param name="positionSeconds">Elapsed playback position in seconds.</param>
    /// <param name="playing">Whether playback is active; active playback keeps the digits opaque.</param>
    /// <param name="delta">Seconds elapsed since the previous update.</param>
    public void UpdateDisplay(double positionSeconds, bool playing, double delta)
    {
        var time = TimeSpan.FromSeconds(positionSeconds);
        int wholeSeconds = (int)time.TotalSeconds;
        if (_displayedSecond != wholeSeconds)
        {
            _displayedSecond = wholeSeconds;
            int minutes = (int)time.TotalMinutes;
            _minutesTens.Text = (minutes / 10).ToString();
            _minutesOnes.Text = (minutes % 10).ToString();
            _secondsTens.Text = (time.Seconds / 10).ToString();
            _secondsOnes.Text = (time.Seconds % 10).ToString();
        }

        if (playing || _blinkIntervalSeconds <= 0)
        {
            _blinkElapsed = 0;
            _dimmed = false;
        }
        else
        {
            _blinkElapsed += delta;
            if (_blinkElapsed >= _blinkIntervalSeconds)
            {
                long intervals = (long)(_blinkElapsed / _blinkIntervalSeconds);
                if (intervals % 2 != 0)
                    _dimmed = !_dimmed;
                _blinkElapsed %= _blinkIntervalSeconds;
            }
        }

        float alpha = _dimmed ? _dimmedAlpha : 1;
        if (Modulate.A != alpha)
            Modulate = new Color(Modulate, alpha);
    }
}
