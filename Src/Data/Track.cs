using Godot;

namespace GodAmp.Data;

[GlobalClass]
public partial class Track : Resource
{
    public string SourcePath;
    public string Name;
    public string Artist;
    public string Album;
    public float Duration;
    public int TrackNumber;
    public int BitrateKbps;
    public int SampleRateHz;
    public AudioStream Stream;
    public bool UseFileName; // We don't have relevant artist information
}