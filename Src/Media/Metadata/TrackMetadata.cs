namespace GodAmp.Media.Metadata;

/// <summary>Detached source metadata that can be read on a worker and applied on the scene thread.</summary>
internal sealed record TrackMetadata(string Path, string Name, string Artist, string Album,
    float Duration, int TrackNumber, int Bitrate, int SampleRate, int Channels, bool UseFileName, string PlaylistTitle = "")
{
    /// <summary>Creates a playback resource from this metadata snapshot.</summary>
    /// <returns>A new track without an open source stream or shared mutable metadata.</returns>
    public Track CreateTrack() => new()
    {
        SourcePath = Path, Name = Name, Artist = Artist, Album = Album, Duration = Duration,
        TrackNumber = TrackNumber, BitrateKbps = Bitrate, SampleRateHz = SampleRate,
        Channels = Channels, UseFileName = UseFileName, PlaylistTitle = PlaylistTitle
    };
}
