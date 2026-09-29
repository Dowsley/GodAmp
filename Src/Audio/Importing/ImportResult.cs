using System.Collections.Generic;
using GodAmp.Data;

namespace GodAmp.Audio.Importing;

/// <summary>Plain metadata transferred from a worker before constructing Godot resources.</summary>
internal sealed record ImportedTrack(string Path, string Name, string Artist, string Album,
    float Duration, int TrackNumber, int Bitrate, int SampleRate, int Channels, bool UseFileName, string PlaylistTitle = "")
{
    public Track CreateTrack() => new()
    {
        SourcePath = Path, Name = Name, Artist = Artist, Album = Album, Duration = Duration,
        TrackNumber = TrackNumber, BitrateKbps = Bitrate, SampleRateHz = SampleRate,
        Channels = Channels, UseFileName = UseFileName, PlaylistTitle = PlaylistTitle
    };
}

internal sealed record ImportResult(IReadOnlyList<ImportedTrack> Tracks, IReadOnlyList<AudioIssue> Issues, bool EmptyPlaylist);
public sealed record ImportProgress(int Completed, int Total, string Path);
