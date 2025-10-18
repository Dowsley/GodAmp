using GodAmp.Data;
using Godot;

namespace GodAmp.Player;

public partial class TrackPlayer : AudioStreamPlayer
{
    public Track CurrentTrack;

    public void SetCurrentTrack(Track track, bool autoplay = true)
    {
        CurrentTrack = track;
        Stream = CurrentTrack.Stream;
        Seek(0.0f);
        Playing = autoplay;
    }
}