using Godot;

namespace GodAmp.Data;

[GlobalClass]
public partial class Track : Resource
{
    public string SourcePath = null!;
    public string Name = null!;
    public string Artist = null!;
    public string Album = "";
    public float Duration;
    public int TrackNumber;
    public int BitrateKbps;
    public int SampleRateHz;
    /// <summary>Source audio channel count; zero when metadata does not report it.</summary>
    public int Channels;
    /// <summary>Whether presentation uses the filename because the source has no title tag.</summary>
    public bool UseFileName;

    /// <summary>Copies display and audio metadata while retaining this resource and its source path.</summary>
    /// <param name="source">Fresh metadata read from the same source.</param>
    public void UpdateMetadata(Track source)
    {
        Name = source.Name;
        Artist = source.Artist;
        Album = source.Album;
        Duration = source.Duration;
        TrackNumber = source.TrackNumber;
        BitrateKbps = source.BitrateKbps;
        SampleRateHz = source.SampleRateHz;
        Channels = source.Channels;
        UseFileName = source.UseFileName;
    }
}
