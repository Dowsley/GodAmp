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
    public AudioStream Stream = null!;
    public bool UseFileName; // We don't have relevant artist information
}