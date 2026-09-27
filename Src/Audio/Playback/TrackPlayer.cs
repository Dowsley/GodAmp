using GodAmp.Data;
using Godot;

namespace GodAmp.Audio.Playback;

public partial class TrackPlayer : AudioStreamPlayer
{
    public Track? CurrentTrack { get; private set; }

    /// <inheritdoc />
    public override void _ExitTree() => ClearCurrentTrack();

    /// <summary>Loads a track into the shared player and resets its playback position.</summary>
    /// <param name="track">Metadata belonging to the stream.</param>
    /// <param name="stream">Unique stream whose ownership transfers to this player.</param>
    /// <param name="autoplay">Whether playback starts immediately.</param>
    public void SetCurrentTrack(Track track, AudioStream stream, bool autoplay = true)
    {
        ClearCurrentTrack();
        CurrentTrack = track;
        StreamPaused = false;
        Stream = stream;
        Seek(0.0f);
        Playing = autoplay;
    }

    /// <summary>Stops playback and releases the player's current track and audio stream.</summary>
    public void ClearCurrentTrack()
    {
        AudioStream? stream = Stream;
        Stop();
        StreamPaused = false;
        CurrentTrack = null;
        Stream = null;
        stream?.Dispose();
    }
}
