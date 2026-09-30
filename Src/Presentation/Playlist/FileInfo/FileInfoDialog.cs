using GodAmp.Media;
using GodAmp.Presentation.Formatting;
using Godot;

namespace GodAmp.Presentation.Playlist.FileInfo;

/// <summary>Presents read-only metadata from an existing queue entry.</summary>
public partial class FileInfoDialog : AcceptDialog
{
    [Export] private Label _title = null!;
    [Export] private Label _artist = null!;
    [Export] private Label _album = null!;
    [Export] private Label _trackNumber = null!;
    [Export] private Label _duration = null!;
    [Export] private Label _audio = null!;
    [Export] private TextEdit _path = null!;

    /// <summary>Updates values without performing I/O or changing dialog visibility.</summary>
    /// <param name="track">The current metadata for the inspected occurrence.</param>
    public void Display(Track track)
    {
        _title.Text = track.Name;
        _artist.Text = track.Artist;
        _album.Text = track.Album;
        _title.TooltipText = _title.Text;
        _artist.TooltipText = _artist.Text;
        _album.TooltipText = _album.Text;
        _trackNumber.Text = track.TrackNumber > 0 ? track.TrackNumber.ToString() : "";
        _duration.Text = TrackTime.Format(track.Duration);
        string channels = track.Channels switch
        {
            1 => "Mono", 2 => "Stereo", > 2 => $"{track.Channels} channels", _ => "Unknown channels"
        };
        string bitrate = track.BitrateKbps > 0 ? $"{track.BitrateKbps} kbps" : "Unknown bitrate";
        string sampleRate = track.SampleRateHz > 0 ? $"{track.SampleRateHz} Hz" : "Unknown sample rate";
        _audio.Text = $"{bitrate} / {sampleRate} / {channels}";
        _audio.TooltipText = _audio.Text;
        _path.Text = track.SourcePath;
    }
}
