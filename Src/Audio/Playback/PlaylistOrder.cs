namespace GodAmp.Audio.Playback;

/// <summary>Commands that change the visible queue order independently of shuffle playback.</summary>
public enum PlaylistOrder
{
    Title = 0,
    FileName = 1,
    Path = 2,
    Reverse = 3,
    Randomize = 4
}
