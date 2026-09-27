using GodAmp.Data;
using Godot;

namespace GodAmp.Core;

public partial class TrackPlayer : AudioStreamPlayer
{
    public Track? CurrentTrack { get; private set; }

    /// <inheritdoc />
    public override void _ExitTree() => ClearCurrentTrack();

    /// <summary>Loads a track into the shared player and resets its playback position.</summary>
    /// <param name="track">Track whose audio stream becomes active.</param>
    /// <param name="autoplay">Whether playback starts immediately.</param>
    public void SetCurrentTrack(Track track, bool autoplay = true)
    {
        CurrentTrack = track;
        StreamPaused = false;
        Stream = CurrentTrack.Stream;
        Seek(0.0f);
        Playing = autoplay;
    }

    /// <summary>Stops playback and releases the player's current track and audio stream.</summary>
    public void ClearCurrentTrack()
    {
        Stop();
        StreamPaused = false;
        CurrentTrack = null;
        Stream = null;
    }
}
