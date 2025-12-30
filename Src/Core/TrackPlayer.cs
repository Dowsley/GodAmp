using GodAmp.Data;
using Godot;

namespace GodAmp.Core;

public partial class TrackPlayer : AudioStreamPlayer
{
    public Track? CurrentTrack = null;

    public void SetCurrentTrack(Track track, bool autoplay = true)
    {
        CurrentTrack = track;
        Stream = CurrentTrack.Stream;
        Seek(0.0f);
        Playing = autoplay;
    }
}